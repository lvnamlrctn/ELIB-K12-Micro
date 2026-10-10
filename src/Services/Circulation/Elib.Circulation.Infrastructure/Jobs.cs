using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Circulation.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Circulation.Infrastructure;

/// <summary>
/// Chạy <see cref="CirculationJobs"/> mỗi giờ cho từng đơn vị đang hoạt động có phân hệ Lưu thông (monolith: Hangfire chạy
/// DueSoonReminderJob mỗi ngày, BookRequestExpiryJob mỗi giờ). Lỗi của một đơn vị không chặn đơn vị khác.
/// </summary>
public sealed partial class CirculationJobScheduler(IServiceScopeFactory scopes, TimeProvider clock, ILogger<CirculationJobScheduler> logger)
    : BackgroundService
{
    public const string Module = "CIRCULATION";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken); // để service khởi động xong (migrate, bản sao đơn vị)
        }
        catch (OperationCanceledException)
        {
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try
            {
                await RunAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunAllAsync(CancellationToken ct)
    {
        List<long> tenants;
        using (var scope = scopes.CreateScope())
        {
            using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            tenants = await scope.ServiceProvider.GetRequiredService<CirculationDbContext>().Set<TenantReplicaRecord>().AsNoTracking()
                .Where(t => t.Status == "Active").OrderBy(t => t.TenantId).Select(t => t.TenantId).ToListAsync(ct);
        }

        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, Module, ct)) continue;
            using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            try
            {
                var result = await scope.ServiceProvider.GetRequiredService<CirculationJobs>().RunAsync(ct);
                if (result.DueSoon + result.Overdue + result.ExpiredHolds > 0)
                    LogRan(logger, tenantId, result.DueSoon, result.Overdue, result.ExpiredHolds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTenantFailed(logger, tenantId, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: nhắc {DueSoon} lượt sắp đến hạn, {Overdue} lượt quá hạn, {Expired} đặt mượn hết hạn giữ")]
    private static partial void LogRan(ILogger logger, long tenantId, int dueSoon, int overdue, int expired);

    [LoggerMessage(Level = LogLevel.Error, Message = "Việc định kỳ lưu thông lỗi ở đơn vị {TenantId}")]
    private static partial void LogTenantFailed(ILogger logger, long tenantId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Việc định kỳ lưu thông lỗi")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
