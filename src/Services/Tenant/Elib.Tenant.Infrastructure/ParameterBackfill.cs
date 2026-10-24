using Elib.BuildingBlocks.Tenancy;
using Elib.Tenant.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Tenant.Infrastructure;

/// <summary>
/// Khi khởi động, bổ sung vào mọi đơn vị các tham số mới của <see cref="Elib.Tenant.Domain.ParameterCatalog"/> (đơn vị tạo trước đó chưa có dòng
/// nên màn Tham số không hiện). Idempotent, mỗi đơn vị một scope/transaction; lỗi một đơn vị không dừng các đơn vị còn lại.
/// </summary>
public sealed partial class ParameterBackfillRunner(IServiceScopeFactory scopes, ILogger<ParameterBackfillRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); // chờ migration/outbox của service ổn định
            List<long> ids;
            await using (var scope = scopes.CreateAsyncScope())
            {
                using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
                ids = await scope.ServiceProvider.GetRequiredService<ITenantDb>().Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(stoppingToken);
            }

            var added = 0;
            foreach (var id in ids)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
                    var count = await scope.ServiceProvider.GetRequiredService<TenantDefaults>().SeedParametersAsync(id, stoppingToken);
                    if (count > 0)
                    {
                        await scope.ServiceProvider.GetRequiredService<ITenantDb>().SaveChangesAsync(stoppingToken);
                        added += count;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogFailed(logger, ex, id);
                }
            }
            if (added > 0) LogDone(logger, added, ids.Count);
        }
        catch (OperationCanceledException)
        {
            // dừng dịch vụ
        }
        catch (Exception ex)
        {
            LogRunFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã bổ sung {Added} tham số mặc định cho {Tenants} đơn vị")]
    private static partial void LogDone(ILogger logger, int added, int tenants);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bổ sung tham số mặc định cho đơn vị {TenantId} thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception, long tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bổ sung tham số mặc định cho các đơn vị thất bại")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
