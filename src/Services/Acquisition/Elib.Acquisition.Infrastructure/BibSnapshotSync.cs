using Elib.Acquisition.Application;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Acquisition.Infrastructure;

/// <summary>
/// Bản sao biểu ghi có từ trước khi lưu loại biểu ghi + khoá tìm không dấu (<see cref="BibSnapshot.Synced"/> = false): lúc khởi động chép lại
/// từ catalog cho từng đơn vị còn dòng chưa đồng bộ (BackfillAsync ghi đè cả khi cùng version). Dòng catalog không trả về (đã xoá hẳn…)
/// thì tự tính khoá từ nhan đề/tác giả/NXB đang có. Catalog chưa lên / lỗi → đơn vị đó giữ nguyên, thử lại vài lần rồi lần khởi động sau.
/// </summary>
public sealed partial class BibSnapshotSync(IServiceScopeFactory scopes, TimeProvider clock, ILogger<BibSnapshotSync> logger) : BackgroundService
{
    private static readonly TimeSpan[] Retries = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            int pending;
            try
            {
                pending = await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                LogFailed(logger, ex);
                pending = -1;
            }
            if (pending == 0 || attempt >= Retries.Length) return;
            await Task.Delay(Retries[attempt], clock, stoppingToken);
        }
    }

    /// <summary>Đồng bộ mọi đơn vị còn dòng chưa đồng bộ. Trả số đơn vị chưa xong (0 = xong hết).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        List<long> tenants;
        using (var scope = scopes.CreateScope())
        {
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            tenants = await scope.ServiceProvider.GetRequiredService<ICrudDbContext>().Set<BibSnapshot>().IgnoreQueryFilters(["Tenant"])
                .Where(b => !b.Synced).Select(b => b.TenantId).Distinct().ToListAsync(ct);
        }

        var failed = 0;
        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            try
            {
                var count = await scope.ServiceProvider.GetRequiredService<BibSnapshots>().BackfillAsync(ct);
                var db = scope.ServiceProvider.GetRequiredService<ICrudDbContext>();
                var rest = await db.Set<BibSnapshot>().Where(b => !b.Synced).ToListAsync(ct);
                foreach (var b in rest)
                {
                    b.TitleKey = Cut(BibSnapshot.Fold(b.Title), 1000);
                    b.AuthorKey = Cut(BibSnapshot.Fold(b.Author), 500);
                    b.PublisherKey = Cut(BibSnapshot.Fold(b.Publisher), 500);
                    b.Synced = true;
                }
                await db.SaveChangesAsync(ct);
                LogSynced(logger, tenantId, count, rest.Count);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                failed++;
                LogTenantFailed(logger, ex, tenantId);
            }
        }
        return failed;
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: đồng bộ bản sao biểu ghi (loại tài liệu, khoá tìm không dấu) — {Count} từ catalog, {Local} tự tính")]
    private static partial void LogSynced(ILogger logger, long tenantId, int count, int local);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đơn vị {TenantId}: catalog chưa trả lời — đồng bộ bản sao biểu ghi để lần sau")]
    private static partial void LogTenantFailed(ILogger logger, Exception exception, long tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Đồng bộ bản sao biểu ghi lúc khởi động thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
