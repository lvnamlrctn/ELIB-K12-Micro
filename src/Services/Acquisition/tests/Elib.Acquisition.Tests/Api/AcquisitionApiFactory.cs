using System.Collections.Concurrent;
using Elib.Acquisition.Application;
using Elib.Acquisition.Infrastructure;
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

namespace Elib.Acquisition.Tests.Api;

/// <summary>Chạy nguyên service acquisition trong process: SQLite thay PostgreSQL, bus in-memory (không outbox), xác thực bằng header.</summary>
public sealed class AcquisitionApiFactory : WebApplicationFactory<Program>
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

    public FakeHoldings Holdings { get; } = new();

    public IEnumerable<T> PublishedOf<T>() => Published.OfType<T>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:AcquisitionDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AcquisitionDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AcquisitionDbContext>>();
            services.AddDbContext<AcquisitionDbContext>((sp, options) => options
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
            services.RemoveAll<IHoldingsReceiptItems>();
            services.AddSingleton<IHoldingsReceiptItems>(Holdings);
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
                await scope.ServiceProvider.GetRequiredService<AcquisitionDbContext>().Database.EnsureCreatedAsync();
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
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public Task<BibChanged?> GetAsync(long tenantId, long mfn, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(Bibs.TryGetValue((tenantId, mfn), out var bib) ? bib : null);
    }

    /// <summary>Theo trang 2 biểu ghi, khoá = MFN — để thử đọc nhiều trang.</summary>
    public Task<StatePage<BibChanged>> PageAsync(long tenantId, long after, CancellationToken ct)
    {
        var page = Bibs.Values.Where(b => b.TenantId == tenantId && b.Mfn > after).OrderBy(b => b.Mfn).Take(2).ToList();
        return Task.FromResult(new StatePage<BibChanged>(page, page.Count == 2 ? page[^1].Mfn : null));
    }
}

/// <summary>
/// Holdings giả cho /internal/tenants/{id}/receipt-items: khoá số lượng theo dòng như holdings thật (số ĐKCB của dòng ≤ số bản của dòng
/// trong trạng thái phiếu gửi kèm), ghi lại yêu cầu cuối. <see cref="Down"/> = holdings không trả lời.
/// </summary>
public sealed class FakeHoldings : IHoldingsReceiptItems
{
    private readonly Lock _lock = new();
    private readonly List<(long TenantId, Guid ReceiptPublicId, ReceiptItem Item)> _items = [];

    public bool Down { get; set; }

    public HoldingsRegisterRequest? LastRequest { get; private set; }

    public Task<IReadOnlyList<RegisteredItem>> RegisterAsync(long tenantId, HoldingsRegisterRequest request, CancellationToken ct)
    {
        EnsureUp();
        lock (_lock)
        {
            LastRequest = request;
            if (request.Receipt.Completed) throw new BusinessRuleException("RECEIPT_COMPLETED", "Phiếu nhận đã hoàn tất.");
            var line = request.Receipt.Lines.Single(l => l.LinePublicId == request.LinePublicId);
            var wanted = request.Barcode is null ? request.Quantity : 1;
            var registered = _items.Count(x => x.TenantId == tenantId && x.Item.LinePublicId == line.LinePublicId);
            if (registered + wanted > line.Quantity) throw new BusinessRuleException("RECEIPT_LINE_FULL", $"còn {line.Quantity - registered}");
            var start = _items.Count(x => x.TenantId == tenantId) + 1;
            var added = Enumerable.Range(0, wanted).Select(i => new ReceiptItem(Guid.CreateVersion7(), line.LinePublicId,
                request.Barcode ?? $"{request.Prefix}{start + i:0000}", line.Mfn, request.StoreId ?? request.Receipt.StoreId, null, "I", false, false,
                DateTimeOffset.UtcNow)).ToList();
            _items.AddRange(added.Select(i => (tenantId, request.Receipt.ReceiptPublicId, i)));
            return Task.FromResult<IReadOnlyList<RegisteredItem>>([.. added.Select(i => new RegisteredItem(i.PublicId, i.Barcode, i.Mfn, i.StoreId, null, i.Status))]);
        }
    }

    public Task<HoldingsReportPage> ReportAsync(long tenantId, HoldingsReportRequest request, CancellationToken ct)
    {
        EnsureUp();
        lock (_lock)
        {
            LastReport = request;
            var rows = Matching(tenantId, request).OrderBy(x => x.Item.Barcode, StringComparer.Ordinal).ToList();
            return Task.FromResult(new HoldingsReportPage([.. rows.Skip(request.Skip).Take(request.Take).Select(x => new HoldingsReportRow(x.Item.Barcode, x.Item.Mfn,
                x.ReceiptPublicId, x.Item.LinePublicId, x.Item.StoreId, StoreName(x.Item.StoreId), null, null, null, null, null, new DateOnly(2026, 10, 1)))], rows.Count));
        }
    }

    public Task<IReadOnlyList<HoldingsStoreCount>> StoreCountsAsync(long tenantId, HoldingsReportRequest request, CancellationToken ct)
    {
        EnsureUp();
        lock (_lock)
        {
            LastReport = request;
            return Task.FromResult<IReadOnlyList<HoldingsStoreCount>>([.. Matching(tenantId, request).GroupBy(x => x.Item.StoreId).OrderBy(g => g.Key)
                .Select(g => new HoldingsStoreCount(g.Key, StoreName(g.Key), g.Count()))]);
        }
    }

    public Task<IReadOnlyList<HoldingsStore>> StoresAsync(long tenantId, CancellationToken ct)
    {
        EnsureUp();
        return Task.FromResult<IReadOnlyList<HoldingsStore>>([new(1, "Kho chính"), new(2, "Kho phụ")]);
    }

    /// <summary>Yêu cầu báo cáo cuối gửi holdings — test kiểm tra danh sách phiếu khớp bộ lọc.</summary>
    public HoldingsReportRequest? LastReport { get; private set; }

    private IEnumerable<(long TenantId, Guid ReceiptPublicId, ReceiptItem Item)> Matching(long tenantId, HoldingsReportRequest request) =>
        _items.Where(x => x.TenantId == tenantId && (request.ReceiptPublicIds is null || request.ReceiptPublicIds.Contains(x.ReceiptPublicId))
            && (request.StoreId is null || x.Item.StoreId == request.StoreId));

    private static string? StoreName(long? id) => id switch { 1 => "Kho chính", 2 => "Kho phụ", _ => null };

    public Task<IReadOnlyList<ReceiptItem>> ListAsync(long tenantId, Guid receiptPublicId, CancellationToken ct)
    {
        EnsureUp();
        lock (_lock)
            return Task.FromResult<IReadOnlyList<ReceiptItem>>([.. _items.Where(x => x.TenantId == tenantId && x.ReceiptPublicId == receiptPublicId).Select(x => x.Item)]);
    }

    public Task RemoveAsync(long tenantId, Guid receiptPublicId, Guid itemPublicId, CancellationToken ct)
    {
        EnsureUp();
        lock (_lock)
        {
            if (_items.RemoveAll(x => x.TenantId == tenantId && x.ReceiptPublicId == receiptPublicId && x.Item.PublicId == itemPublicId) == 0)
                throw new NotFoundException("Bản sách của phiếu nhận", itemPublicId);
        }
        return Task.CompletedTask;
    }

    private void EnsureUp()
    {
        if (Down) throw new BusinessRuleException("HOLDINGS_UNAVAILABLE", "Không kết nối được phân hệ Kho (holdings).", 503);
    }
}
