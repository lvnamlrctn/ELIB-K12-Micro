using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.TenantReplica;

/// <summary>
/// Đơn vị chủ của dữ liệu dùng chung toàn hệ thống: một đơn vị thường (có admin, host riêng) mang mã dành riêng <see cref="Code"/>.
/// Quản trị nền tảng đăng nhập vào đơn vị này để tạo/sửa tài liệu số, từ điển, ISBD; mọi đơn vị khác nhận BẢN SAO chỉ đọc qua
/// <see cref="SharedSyncRunner{TDbContext}"/>.
/// </summary>
public static class SharedTenant
{
    public const string Code = "SHARED";
}

/// <summary>Tiến độ đồng bộ một bộ dữ liệu dùng chung cho một đơn vị: <see cref="Watermark"/> = mốc thay đổi lớn nhất của dữ liệu gốc đã áp dụng.</summary>
public sealed class SharedSyncState : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public string Dataset { get; set; } = "";
    public DateTimeOffset Watermark { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
}

/// <summary>
/// Một bộ dữ liệu dùng chung (từ điển, ISBD, tài liệu số…) của service. Gốc nằm ở đơn vị SHARED trong cùng DB của service; bản sao ghi vào từng đơn vị.
/// Mốc thay đổi của dữ liệu gốc = lớn nhất của (UpdatedAt ?? CreatedAt) và DeletedAt — mọi dòng gốc đã sửa/xoá có mốc &gt; mốc đã áp dụng.
/// </summary>
public interface ISharedDataset
{
    /// <summary>Tên bộ dữ liệu (khoá của <see cref="SharedSyncState.Dataset"/>).</summary>
    string Name { get; }

    /// <summary>Phân hệ license mà đơn vị đích phải có để nhận bản sao.</summary>
    string RequiredModule { get; }

    /// <summary>Mốc thay đổi lớn nhất của dữ liệu gốc; null = chưa có dữ liệu gốc. Chạy ở ngữ cảnh hệ thống.</summary>
    Task<DateTimeOffset?> MasterMarkerAsync(long sharedTenantId, CancellationToken ct);

    /// <summary>
    /// Đọc dữ liệu gốc đã đổi trong (<paramref name="since"/>, <paramref name="until"/>] — kể cả dòng đã xoá khi <paramref name="since"/> khác null.
    /// Chạy ở ngữ cảnh hệ thống; kết quả được dùng chung cho mọi đơn vị cùng mốc.
    /// </summary>
    Task<object> LoadChangesAsync(long sharedTenantId, DateTimeOffset? since, DateTimeOffset until, CancellationToken ct);

    /// <summary>Áp dữ liệu gốc đã đổi vào đơn vị — chạy trong scope riêng của đơn vị (<c>ITenantContext.Use</c>), phải idempotent.</summary>
    Task ApplyAsync(long tenantId, object changes, CancellationToken ct);
}

/// <summary>Trạng thái chạy gần nhất của đồng bộ dữ liệu dùng chung — cho màn hình đơn vị SHARED.</summary>
public sealed class SharedSyncMonitor : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly Lock _gate = new();
    private SharedSyncResult? _last;

    public SharedSyncResult? Last { get { lock (_gate) return _last; } }

    public void Record(SharedSyncResult result) { lock (_gate) _last = result; }

    /// <summary>Yêu cầu chạy ngay (nút "Đồng bộ ngay").</summary>
    public void RunNow()
    {
        try { _signal.Release(); }
        catch (SemaphoreFullException) { /* đã có yêu cầu đang chờ */ }
    }

    public void Dispose() => _signal.Dispose();

    internal Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);
}

/// <summary>Kết quả một lượt đồng bộ: số đơn vị đã áp dụng, còn chờ, lỗi.</summary>
public sealed record SharedSyncResult(DateTimeOffset At, long? SharedTenantId, int Applied, int Pending, int Failed, string? Error);

/// <summary>
/// Đồng bộ dữ liệu dùng chung từ đơn vị SHARED sang mọi đơn vị đang hoạt động (có license phân hệ của bộ dữ liệu). Mỗi lượt: đọc một lần ở ngữ cảnh
/// hệ thống, ghi từng đơn vị trong scope riêng (RLS), tối đa <see cref="MaxTenantsPerRun"/> đơn vị/lượt để không chiếm DB; lỗi một đơn vị không chặn đơn vị khác.
/// </summary>
public sealed partial class SharedSyncRunner<TDbContext>(IServiceScopeFactory scopes, TimeProvider clock, SharedSyncMonitor monitor, ILogger<SharedSyncRunner<TDbContext>> logger)
    : BackgroundService where TDbContext : DbContext
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    public const int MaxTenantsPerRun = 40;
    private static readonly TimeSpan Overlap = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken); // để service khởi động xong (migrate, bản sao đơn vị)
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await RunOnceAsync(stoppingToken);
                    // Còn đơn vị chờ (vượt trần mỗi lượt) → chạy tiếp ngay, không đợi hết chu kỳ.
                    if (result.Pending > 0 && result.Failed == 0) continue;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogFailed(logger, ex);
                }
                await monitor.WaitAsync(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // đang tắt service
        }
    }

    public async Task<SharedSyncResult> RunOnceAsync(CancellationToken ct)
    {
        long? sharedId = null;
        int applied = 0, pending = 0, failed = 0;
        string? error = null;
        try
        {
            using var root = scopes.CreateScope();
            using var system = root.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            var db = root.ServiceProvider.GetRequiredService<TDbContext>();
            sharedId = await db.Set<TenantReplicaRecord>().AsNoTracking()
                .Where(t => t.Code == SharedTenant.Code && t.Status == "Active").Select(t => (long?)t.TenantId).FirstOrDefaultAsync(ct);
            if (sharedId is not { } shared) return Done(sharedId, 0, 0, 0, null);

            var targets = await db.Set<TenantReplicaRecord>().AsNoTracking()
                .Where(t => t.Status == "Active" && t.TenantId != shared).OrderBy(t => t.TenantId).Select(t => t.TenantId).ToListAsync(ct);
            var states = (await db.Set<SharedSyncState>().IgnoreQueryFilters([ElibQueryFilters.Tenant]).AsNoTracking().ToListAsync(ct))
                .ToDictionary(s => (s.TenantId, s.Dataset), s => s.Watermark);

            var budget = MaxTenantsPerRun;
            foreach (var dataset in root.ServiceProvider.GetServices<ISharedDataset>())
            {
                if (await dataset.MasterMarkerAsync(shared, ct) is not { } marker) continue;
                // Chỉ đơn vị đang có license phân hệ mới được kể là "chờ đồng bộ" — đơn vị không license không có dòng trạng thái nên nếu để lọt vào đây
                // thì mỗi lượt đều nạp lại toàn bộ dữ liệu gốc vô ích.
                var licenseSource = root.ServiceProvider.GetRequiredService<IModuleLicenseSource>();
                var licensed = new HashSet<long>();
                foreach (var t in targets)
                    if (await licenseSource.IsLicensedAsync(t, dataset.RequiredModule, ct)) licensed.Add(t);
                var lagging = targets.Where(t => licensed.Contains(t) && (!states.TryGetValue((t, dataset.Name), out var w) || w < marker)).ToList();
                var cache = new Dictionary<DateTimeOffset, object>(); // khoá: since (MinValue = chưa đồng bộ lần nào)
                foreach (var tenantId in lagging)
                {
                    if (budget <= 0) { pending++; continue; }
                    DateTimeOffset? since = states.TryGetValue((tenantId, dataset.Name), out var w) ? w - Overlap : null;
                    try
                    {
                        var key = since ?? DateTimeOffset.MinValue;
                        if (!cache.TryGetValue(key, out var changes)) cache[key] = changes = await dataset.LoadChangesAsync(shared, since, marker, ct);
                        if (await ApplyAsync(tenantId, dataset.Name, dataset.RequiredModule, changes, marker, ct)) { applied++; budget--; }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        failed++;
                        error = ex.Message;
                        LogTenantFailed(logger, tenantId, dataset.Name, ex);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = ex.Message;
            failed++;
            LogFailed(logger, ex);
        }
        return Done(sharedId, applied, pending, failed, error);

        SharedSyncResult Done(long? id, int a, int p, int f, string? e)
        {
            var result = new SharedSyncResult(clock.GetUtcNow(), id, a, p, f, e);
            monitor.Record(result);
            return result;
        }
    }

    private async Task<bool> ApplyAsync(long tenantId, string datasetName, string module, object changes, DateTimeOffset marker, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        if (!await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, module, ct)) return false;
        using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        var dataset = scope.ServiceProvider.GetServices<ISharedDataset>().Single(d => d.Name == datasetName);
        await dataset.ApplyAsync(tenantId, changes, ct);

        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var state = await db.Set<SharedSyncState>().FirstOrDefaultAsync(s => s.Dataset == datasetName, ct);
        if (state is null)
        {
            state = new SharedSyncState { Dataset = datasetName };
            db.Add(state);
        }
        state.Watermark = marker;
        state.SyncedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đồng bộ dữ liệu dùng chung lỗi")]
    private static partial void LogFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đồng bộ dữ liệu dùng chung lỗi cho đơn vị {TenantId}, bộ {Dataset}")]
    private static partial void LogTenantFailed(ILogger logger, long tenantId, string dataset, Exception ex);
}

public static class SharedSyncExtensions
{
    /// <summary>Gọi trong OnModelCreating của DbContext service (cùng chỗ <c>AddTenantReplica</c>); thêm migration + <c>EnableTenantRls("shared_sync_states")</c>.</summary>
    public static ModelBuilder AddSharedSync(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SharedSyncState>(e =>
        {
            e.ToTable("shared_sync_states");
            e.HasKey(s => s.Id);
            e.Property(s => s.Dataset).HasMaxLength(60).IsRequired();
            e.HasIndex(s => new { s.TenantId, s.Dataset }).IsUnique();
        });
        return modelBuilder;
    }

    /// <summary>Đăng ký bộ chạy nền + giám sát; từng bộ dữ liệu đăng ký thêm bằng <see cref="AddSharedDataset{T}"/>.</summary>
    public static IServiceCollection AddSharedSync<TDbContext>(this IServiceCollection services) where TDbContext : DbContext
    {
        services.AddSingleton<SharedSyncMonitor>();
        services.AddSingleton<SharedSyncRunner<TDbContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<SharedSyncRunner<TDbContext>>());
        return services;
    }

    public static IServiceCollection AddSharedDataset<T>(this IServiceCollection services) where T : class, ISharedDataset
    {
        services.AddScoped<ISharedDataset, T>();
        return services;
    }
}
