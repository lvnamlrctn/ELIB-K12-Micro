using System.Collections.Concurrent;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Holdings.Application;
using Elib.Holdings.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Holdings.Tests.Api;

/// <summary>Chạy nguyên service holdings trong process: SQLite thay PostgreSQL, bus in-memory (không outbox), xác thực bằng header.</summary>
public sealed class HoldingsApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteTestDatabase _database = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public ConcurrentQueue<object> Published { get; } = new();

    /// <summary>Message consumer đã xử lý xong (không lỗi).</summary>
    public ConcurrentQueue<object> Consumed { get; } = new();
    public FakePermissionSource Permissions { get; } = new();
    public FakeLicenseSource Licenses { get; } = new();

    public FakeCatalogBibs CatalogBibs { get; } = new();

    public IEnumerable<T> PublishedOf<T>() => Published.OfType<T>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:HoldingsDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<HoldingsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<HoldingsDbContext>>();
            services.AddDbContext<HoldingsDbContext>((sp, options) => options
                .UseSqlite(_database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));
            services.AddHeaderTestAuth();
            services.RemoveAll<IPermissionSource>();
            services.AddSingleton<IPermissionSource>(Permissions);
            services.RemoveAll<IModuleLicenseSource>();
            services.AddSingleton<IModuleLicenseSource>(Licenses);
            services.RemoveAll<ICatalogBibs>();
            services.AddSingleton<ICatalogBibs>(CatalogBibs);
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
                await scope.ServiceProvider.GetRequiredService<HoldingsDbContext>().Database.EnsureCreatedAsync();
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

/// <summary>Catalog giả cho /internal/tenants/{tenantId}/bibs/{mfn} — test đặt biểu ghi "đã có ở catalog".</summary>
public sealed class FakeCatalogBibs : ICatalogBibs
{
    public ConcurrentDictionary<(long TenantId, long Mfn), BibChanged> Bibs { get; } = new();
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<BibChanged?> GetAsync(long tenantId, long mfn, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(Bibs.TryGetValue((tenantId, mfn), out var bib) ? bib : null);
    }
}
