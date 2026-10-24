using System.Collections.Concurrent;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Patron;
using Elib.Circulation.Application;
using Elib.Circulation.Application.Sip2;
using Elib.Circulation.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Circulation.Tests.Api;

/// <summary>Chạy nguyên service circulation trong process: SQLite thay PostgreSQL, bus in-memory (không outbox), xác thực bằng header.</summary>
public sealed class CirculationApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteTestDatabase _database = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public ConcurrentQueue<object> Published { get; } = new();

    /// <summary>Message consumer đã xử lý xong (không lỗi).</summary>
    public ConcurrentQueue<object> Consumed { get; } = new();
    public FakePermissionSource Permissions { get; } = new();
    public FakeLicenseSource Licenses { get; } = new();

    public FakeReplicaSources Sources { get; } = new();

    /// <summary>Mật khẩu OPAC của bạn đọc theo (đơn vị, số thẻ) — thay identity khi trạm SIP2 bắt nhập mật khẩu.</summary>
    public FakeReaderPasswords ReaderPasswords { get; } = new();

    /// <summary>Đồng hồ của service — test tua tới để có lượt quá hạn.</summary>
    public TestClock Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Tham số lưu thông theo đơn vị (đơn vị chưa đặt = mặc định).</summary>
    public ConcurrentDictionary<long, CirculationSettings> Settings { get; } = new();

    public IEnumerable<T> PublishedOf<T>() => Published.OfType<T>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CirculationDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");
        builder.UseSetting("Sip2:Enabled", "true");
        builder.UseSetting("Sip2:Address", "127.0.0.1");
        builder.UseSetting("Sip2:Port", "0");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CirculationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CirculationDbContext>>();
            services.AddDbContext<CirculationDbContext>((sp, options) => options
                .UseSqlite(_database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));
            services.AddHeaderTestAuth();
            services.RemoveAll<IPermissionSource>();
            services.AddSingleton<IPermissionSource>(Permissions);
            services.RemoveAll<IModuleLicenseSource>();
            services.AddSingleton<IModuleLicenseSource>(Licenses);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<ICirculationSettings>();
            services.AddScoped<ICirculationSettings>(sp => new FakeCirculationSettings(Settings, sp.GetRequiredService<ITenantContext>()));
            services.RemoveAll<IReplicaSources>();
            services.AddSingleton<IReplicaSources>(Sources);
            services.RemoveAll<IReaderPasswordChecker>();
            services.AddSingleton<IReaderPasswordChecker>(ReaderPasswords);
        });
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            using (var scope = Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<CirculationDbContext>().Database.EnsureCreatedAsync();
            }
            Services.GetRequiredService<IBus>().ConnectPublishObserver(new Recorder(Published));
            Services.GetRequiredService<IBus>().ConnectConsumeObserver(new ConsumeRecorder(Consumed));
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task PublishAsync<T>(T message) where T : class => Services.GetRequiredService<IBus>().Publish(message);

    /// <summary>Phát theo kiểu thật của message (mảng event nhiều kiểu).</summary>
    public Task PublishAsAsync(object message) => Services.GetRequiredService<IBus>().Publish(message, message.GetType());

    /// <summary>Chờ consumer xử lý xong event (theo EventId).</summary>
    public Task WaitConsumedAsync(Elib.Contracts.Events.IntegrationEvent e) =>
        WaitForAsync(() => Task.FromResult(Consumed.OfType<Elib.Contracts.Events.IntegrationEvent>().FirstOrDefault(c => c.EventId == e.EventId)), e.GetType().Name);

    /// <summary>Chờ consumer xử lý xong (bus in-memory chạy nền).</summary>
    public static async Task<T> WaitForAsync<T>(Func<Task<T?>> probe, string what) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await probe() is { } found) return found;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Hết thời gian chờ: {what}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _database.Dispose();
            _initLock.Dispose();
        }
    }

    private sealed class ConsumeRecorder(ConcurrentQueue<object> sink) : IConsumeObserver
    {
        public Task PreConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;

        public Task PostConsume<T>(ConsumeContext<T> context) where T : class
        {
            sink.Enqueue(context.Message);
            return Task.CompletedTask;
        }

        public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class => Task.CompletedTask;
    }

    private sealed class Recorder(ConcurrentQueue<object> sink) : IPublishObserver
    {
        public Task PrePublish<T>(PublishContext<T> context) where T : class => Task.CompletedTask;

        public Task PostPublish<T>(PublishContext<T> context) where T : class
        {
            sink.Enqueue(context.Message);
            return Task.CompletedTask;
        }

        public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class => Task.CompletedTask;
    }
}

/// <summary>Patron/holdings/catalog giả cho /internal — test đặt dữ liệu "đã có ở service gốc nhưng chưa có bản sao".</summary>
public sealed class FakeReplicaSources : IReplicaSources
{
    public ConcurrentDictionary<(long TenantId, string CardNo), ReaderChanged> Readers { get; } = new();
    public ConcurrentDictionary<(long TenantId, string Barcode), ItemChanged> Items { get; } = new();
    public ConcurrentDictionary<(long TenantId, long Mfn), BibChanged> Bibs { get; } = new();

    public Task<ReaderChanged?> ReaderAsync(long tenantId, string cardNo, CancellationToken ct) =>
        Task.FromResult(Readers.TryGetValue((tenantId, cardNo.ToUpperInvariant()), out var r) ? r : null);

    public Task<ItemChanged?> ItemAsync(long tenantId, string barcode, CancellationToken ct) =>
        Task.FromResult(Items.TryGetValue((tenantId, barcode.ToUpperInvariant()), out var i) ? i : null);

    public Task<BibChanged?> BibAsync(long tenantId, long mfn, CancellationToken ct) =>
        Task.FromResult(Bibs.TryGetValue((tenantId, mfn), out var b) ? b : null);
}

public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

public sealed class FakeReaderPasswords : IReaderPasswordChecker
{
    public ConcurrentDictionary<(long TenantId, string CardNo), string> Passwords { get; } = new();

    public Task<string> CheckAsync(long tenantId, string cardNo, string? password, CancellationToken ct) =>
        Task.FromResult(Passwords.TryGetValue((tenantId, cardNo.Trim().ToUpperInvariant()), out var expected)
            ? expected == password ? "valid" : "invalid"
            : "no-patron");
}

internal sealed class FakeCirculationSettings(ConcurrentDictionary<long, CirculationSettings> settings, ITenantContext tenant) : ICirculationSettings
{
    public Task<CirculationSettings> GetAsync(CancellationToken ct) =>
        Task.FromResult(settings.GetValueOrDefault(tenant.RequireTenantId(), CirculationSettings.Default));
}
