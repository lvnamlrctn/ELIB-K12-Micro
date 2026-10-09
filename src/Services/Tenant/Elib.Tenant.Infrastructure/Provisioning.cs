using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Application;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Tenant.Infrastructure;

/// <summary>Service khác báo đã seed xong cho đơn vị.</summary>
public sealed class TenantSeededConsumer(IModuleLicenseSource licenses, ILogger<TenantSeededConsumer> logger, ProvisioningService provisioning)
    : ElibConsumer<TenantSeeded>(licenses, logger)
{
    protected override Task HandleAsync(TenantSeeded message, ConsumeContext<TenantSeeded> context)
        => provisioning.HandleSeededAsync(message, context.CancellationToken);
}

/// <summary>Quét định kỳ các đơn vị khởi tạo quá hạn. Chạy được trên nhiều replica: Version là concurrency token nên chỉ một bản ghi thắng.</summary>
public sealed partial class ProvisioningSweeper(
    IServiceScopeFactory scopes, IOptions<ProvisioningOptions> options, ILogger<ProvisioningSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
                var count = await scope.ServiceProvider.GetRequiredService<ProvisioningService>().SweepTimeoutsAsync(stoppingToken);
                if (count > 0) LogTimedOut(logger, count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSweepFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} đơn vị khởi tạo quá hạn đã chuyển sang ProvisioningFailed")]
    private static partial void LogTimedOut(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Quét khởi tạo đơn vị thất bại")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}

/// <summary>Service tenant là nguồn sự thật về license nên đọc thẳng DB của mình (service khác đọc TenantReplica).</summary>
public sealed class TenantLicenseSource(TenantQueries queries) : IModuleLicenseSource
{
    public Task<bool> IsLicensedAsync(long tenantId, string moduleCode, CancellationToken cancellationToken)
        => queries.IsLicensedAsync(tenantId, moduleCode, cancellationToken);
}
