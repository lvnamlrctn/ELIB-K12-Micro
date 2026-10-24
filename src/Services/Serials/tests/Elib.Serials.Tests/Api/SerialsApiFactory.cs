using System.Collections.Concurrent;
using Elib.Serials.Application;
using Elib.Serials.Infrastructure;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Serials.Tests.Api;

/// <summary>Chạy nguyên service serials trong process: SQLite thay PostgreSQL, bus in-memory (không outbox), xác thực bằng header.</summary>
public sealed class SerialsApiFactory : WebApplicationFactory<Program>
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

    public FakeHoldingsSerialItems Holdings { get; } = new();

    public FakeSuppliers Suppliers { get; } = new();

    public IEnumerable<T> PublishedOf<T>() => Published.OfType<T>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SerialsDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SerialsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SerialsDbContext>>();
            services.AddDbContext<SerialsDbContext>((sp, options) => options
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
            services.RemoveAll<IHoldingsSerialItems>();
            services.AddSingleton<IHoldingsSerialItems>(Holdings);
            services.RemoveAll<ISupplierSource>();
            services.AddSingleton<ISupplierSource>(Suppliers);
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
                await scope.ServiceProvider.GetRequiredService<SerialsDbContext>().Database.EnsureCreatedAsync();
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

/// <summary>Catalog giả cho /internal/tenants/{tenantId}/bibs[/{mfn}] — test đặt biểu ghi "đã có ở catalog".</summary>
public sealed class FakeCatalogBibs : ICatalogBibs
{
    public ConcurrentDictionary<(long TenantId, long Mfn), BibChanged> Bibs { get; } = new();

    public Task<BibChanged?> GetAsync(long tenantId, long mfn, CancellationToken ct) =>
        Task.FromResult(Bibs.TryGetValue((tenantId, mfn), out var bib) ? bib : null);

    public Task<StatePage<BibChanged>> PageAsync(long tenantId, long after, CancellationToken ct)
    {
        var page = Bibs.Values.Where(b => b.TenantId == tenantId && b.Mfn > after).OrderBy(b => b.Mfn).Take(2).ToList();
        return Task.FromResult(new StatePage<BibChanged>(page, page.Count == 2 ? page[^1].Mfn : null));
    }
}

/// <summary>
/// Holdings giả cho /internal/tenants/{id}/serial-items: Register idempotent theo BindingPublicId, trùng số ĐKCB → lỗi nghiệp vụ;
/// <see cref="Down"/> = holdings không trả lời; <see cref="BlockRemove"/> = bản đã lưu thông (không gỡ được).
/// </summary>
public sealed class FakeHoldingsSerialItems : IHoldingsSerialItems
{
    private readonly Lock _lock = new();
    private readonly Dictionary<(long TenantId, Guid Binding), SerialItemRegistered> _items = [];

    public bool Down { get; set; }

    public bool BlockRemove { get; set; }

    public int RegisterCalls { get; private set; }

    /// <summary>Chạy ngay sau khi holdings đã tạo ĐKCB — test dùng để làm serials lưu lỗi (kiểm gỡ bù).</summary>
    public Action<long, SerialItemRegisterRequest>? AfterRegister { get; set; }

    public int Count(long tenantId)
    {
        lock (_lock) return _items.Count(x => x.Key.TenantId == tenantId);
    }

    public int RemoveCalls { get; private set; }

    public bool Has(long tenantId, Guid bindingPublicId)
    {
        lock (_lock) return _items.ContainsKey((tenantId, bindingPublicId));
    }

    public Task<SerialItemRegistered> RegisterAsync(long tenantId, SerialItemRegisterRequest request, CancellationToken ct)
    {
        EnsureUp();
        lock (_lock)
        {
            RegisterCalls++;
            if (_items.TryGetValue((tenantId, request.BindingPublicId), out var existing)) return Task.FromResult(existing);
            if (_items.Any(x => x.Key.TenantId == tenantId && x.Value.Barcode == request.AccessionNo))
                throw new BusinessRuleException("BARCODE_EXISTS", $"Số ĐKCB {request.AccessionNo} đã có.", 409);
            var item = new SerialItemRegistered(Guid.CreateVersion7(), request.AccessionNo, request.Mfn, request.StoreId, request.StoreId is null ? null : "Kho chính");
            _items[(tenantId, request.BindingPublicId)] = item;
            AfterRegister?.Invoke(tenantId, request);
            return Task.FromResult(item);
        }
    }

    public Task RemoveAsync(long tenantId, Guid bindingPublicId, CancellationToken ct)
    {
        EnsureUp();
        lock (_lock)
        {
            RemoveCalls++;
            if (BlockRemove) throw new BusinessRuleException("ITEM_ON_LOAN", "Bản đang được mượn.", 409);
            _items.Remove((tenantId, bindingPublicId));
        }
        return Task.CompletedTask;
    }

    private void EnsureUp()
    {
        if (Down) throw new BusinessRuleException("HOLDINGS_UNAVAILABLE", "Không kết nối được phân hệ Kho (holdings).", 503);
    }
}

public sealed class FakeSuppliers : ISupplierSource
{
    public ConcurrentDictionary<long, IReadOnlyList<SupplierOption>> ByTenant { get; } = new();

    public Task<IReadOnlyList<SupplierOption>> ListAsync(long tenantId, CancellationToken ct) =>
        Task.FromResult(ByTenant.TryGetValue(tenantId, out var list) ? list : []);
}
