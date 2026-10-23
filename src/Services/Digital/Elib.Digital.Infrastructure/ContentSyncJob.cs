using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Application;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Digital.Infrastructure;

/// <summary>
/// Việc nền đồng bộ nội dung tài liệu số: cứ vài chục giây tìm các đơn vị có tài liệu đang chờ (hàng đợi nằm trong CSDL nên không mất khi dịch vụ khởi động lại)
/// và xử lý vài tài liệu mỗi đơn vị mỗi lượt. Lỗi của một đơn vị không chặn đơn vị khác.
/// </summary>
public sealed partial class EbookContentSyncJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<EbookContentSyncJob> logger) : BackgroundService
{
    public const string Module = "DIGITAL";
    private const int BatchPerTenant = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), clock, stoppingToken); // để service khởi động xong (migrate, bản sao đơn vị)
        }
        catch (OperationCanceledException)
        {
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Một lượt: xử lý các tài liệu đang chờ của mọi đơn vị. Trả số tài liệu đã xử lý (test gọi trực tiếp).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        List<long> tenants;
        using (var scope = scopes.CreateScope())
        {
            using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            var stale = clock.GetUtcNow().AddMinutes(-EbookContentState.StaleMinutes);
            tenants = await scope.ServiceProvider.GetRequiredService<DigitalDbContext>().Set<EbookContentState>().AsNoTracking()
                .IgnoreQueryFilters([ElibQueryFilters.Tenant]) // quét mọi đơn vị: bộ lọc đơn vị mặc định chỉ cho đơn vị hiện tại (không có ở ngữ cảnh hệ thống)
                .Where(s => s.Status == EbookContentState.Pending || (s.Status == EbookContentState.Running && s.StartedAt != null && s.StartedAt < stale))
                .Select(s => s.TenantId).Distinct().ToListAsync(ct);
        }

        var processed = 0;
        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, Module, ct)) continue;
            using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            try
            {
                processed += await scope.ServiceProvider.GetRequiredService<EbookContentSync>().ProcessPendingAsync(BatchPerTenant, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTenantFailed(logger, tenantId, ex);
            }
        }
        return processed;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Đồng bộ nội dung tài liệu số lỗi ở đơn vị {TenantId}")]
    private static partial void LogTenantFailed(ILogger logger, long tenantId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Đồng bộ nội dung tài liệu số lỗi")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
