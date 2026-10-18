using System.Collections.Concurrent;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Patron;
using Elib.Digital.Application;
using Elib.Digital.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Digital.Tests.Api;

/// <summary>Chạy nguyên service digital trong process: SQLite thay PostgreSQL, bus in-memory (không outbox), xác thực bằng header.</summary>
public sealed class DigitalApiFactory : WebApplicationFactory<Program>
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
    public FakeCatalogBibLookup CatalogBibs { get; } = new();
    public FakeMediaFileAccess Media { get; } = new();
    public FakeMediaUploadTicket Uploads { get; } = new();

    /// <summary>Đồng hồ của service — test tua tới để có lượt hết hạn.</summary>
    public TestClock Clock { get; } = new(DateTimeOffset.UtcNow);

    public IEnumerable<T> PublishedOf<T>() => Published.OfType<T>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DigitalDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DigitalDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DigitalDbContext>>();
            services.AddDbContext<DigitalDbContext>((sp, options) => options
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
            services.RemoveAll<IReplicaSources>();
            services.AddSingleton<IReplicaSources>(Sources);
            services.RemoveAll<ICatalogBibLookup>();
            services.AddSingleton<ICatalogBibLookup>(CatalogBibs);
            services.RemoveAll<IMediaFileAccess>();
            services.AddSingleton<IMediaFileAccess>(Media);
            services.RemoveAll<IMediaUploadTicket>();
            services.AddSingleton<IMediaUploadTicket>(Uploads);
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
                await scope.ServiceProvider.GetRequiredService<DigitalDbContext>().Database.EnsureCreatedAsync();
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

    public Task PublishAsAsync(object message) => Services.GetRequiredService<IBus>().Publish(message, message.GetType());

    /// <summary>Chờ consumer xử lý xong event (theo EventId).</summary>
    public Task WaitConsumedAsync(Elib.Contracts.Events.IntegrationEvent e) =>
        WaitForAsync(() => Task.FromResult(Consumed.OfType<Elib.Contracts.Events.IntegrationEvent>().FirstOrDefault(c => c.EventId == e.EventId)), e.GetType().Name);

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

/// <summary>Patron giả cho /internal — test đặt dữ liệu "đã có ở patron nhưng chưa có bản sao".</summary>
public sealed class FakeReplicaSources : IReplicaSources
{
    public ConcurrentDictionary<(long TenantId, string CardNo), ReaderChanged> Readers { get; } = new();

    public Task<ReaderChanged?> ReaderAsync(long tenantId, string cardNo, CancellationToken ct) =>
        Task.FromResult(Readers.TryGetValue((tenantId, cardNo.ToUpperInvariant()), out var r) ? r : null);
}

/// <summary>Catalog giả cho ô chọn liên kết sách in — test đặt sẵn danh sách biểu ghi "tìm được".</summary>
public sealed class FakeCatalogBibLookup : ICatalogBibLookup
{
    public List<BibLinkOption> Results { get; } = [];

    public Task<IReadOnlyList<BibLinkOption>> SearchAsync(string? keyword, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BibLinkOption>>(Results);
}

/// <summary>Media giả cho URL file — trả một URL ký cố định (test chỉ cần biết digital đã tự kiểm quyền trước khi gọi tới đây).</summary>
public sealed class FakeMediaFileAccess : IMediaFileAccess
{
    public ConcurrentQueue<Guid> Requested { get; } = new();

    public Task<EbookFileUrl> SignedUrlAsync(Guid mediaFileId, CancellationToken ct)
    {
        Requested.Enqueue(mediaFileId);
        return Task.FromResult(new EbookFileUrl($"https://media.test/signed/{mediaFileId:N}", DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}

/// <summary>Vé upload giả cho nộp tài liệu bạn đọc — trả vé cố định, CompleteAsync luôn thành công trừ khi test đặt trước lỗi.</summary>
public sealed class FakeMediaUploadTicket : IMediaUploadTicket
{
    public ConcurrentQueue<string> RequestedPurposes { get; } = new();
    public ConcurrentQueue<Guid> Completed { get; } = new();
    public bool RejectComplete { get; set; }

    public Task<MediaUploadTicket> RequestAsync(string purpose, string fileName, string contentType, long size, CancellationToken ct)
    {
        RequestedPurposes.Enqueue(purpose);
        return Task.FromResult(new MediaUploadTicket(Guid.CreateVersion7(), "https://media.test/upload/fake", DateTimeOffset.UtcNow.AddMinutes(10), 50 * 1024 * 1024));
    }

    public Task CompleteAsync(Guid fileId, CancellationToken ct)
    {
        if (RejectComplete) throw new BusinessRuleException("MEDIA_TYPE_INVALID", "Nội dung file không hợp lệ (giả lập test).");
        Completed.Enqueue(fileId);
        return Task.CompletedTask;
    }
}

public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
