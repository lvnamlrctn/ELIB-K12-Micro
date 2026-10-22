using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Payment.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Payment.Infrastructure;

/// <summary>
/// Đóng mã thanh toán hết hạn mỗi phút cho từng đơn vị đang hoạt động có phân hệ PAYMENT (monolith: PaymentExpiryJob, 5 phút/lần — ở đây ngắn
/// hơn để mã không "sống" quá hạn lâu) và báo nguồn qua <c>PaymentExpired</c>. Lỗi của một đơn vị không chặn đơn vị khác.
/// </summary>
public sealed partial class PaymentExpiryJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<PaymentExpiryJob> logger) : BackgroundService
{
    public const string Module = "PAYMENT";

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
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
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

    public async Task<int> RunAllAsync(CancellationToken ct)
    {
        List<long> tenants;
        using (var scope = scopes.CreateScope())
        {
            using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
            tenants = await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Set<TenantReplicaRecord>().AsNoTracking()
                .Where(t => t.Status == "Active").OrderBy(t => t.TenantId).Select(t => t.TenantId).ToListAsync(ct);
        }

        var expired = 0;
        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, Module, ct)) continue;
            using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            try
            {
                var n = await scope.ServiceProvider.GetRequiredService<PaymentExpiry>().RunAsync(ct);
                if (n > 0) LogRan(logger, tenantId, n);
                expired += n;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTenantFailed(logger, tenantId, ex);
            }
        }
        return expired;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: đã đóng {Count} mã thanh toán hết hạn")]
    private static partial void LogRan(ILogger logger, long tenantId, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Đơn vị {TenantId}: đóng mã thanh toán hết hạn thất bại")]
    private static partial void LogTenantFailed(ILogger logger, long tenantId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job đóng mã thanh toán hết hạn thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}

/// <summary>
/// Dọn nhật ký webhook quá 90 ngày mỗi ngày (Sepay gửi mọi khoản tiền vào tài khoản của thư viện nên bảng lớn nhanh). Giao dịch và sổ thanh toán KHÔNG bị xoá.
/// </summary>
public sealed partial class WebhookLogPurgeJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<WebhookLogPurgeJob> logger) : BackgroundService
{
    public const int RetentionDays = 90;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddDays(-RetentionDays);
        using var scope = scopes.CreateScope();
        using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
        var removed = await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Set<Elib.Payment.Domain.WebhookLog>().IgnoreQueryFilters()
            .Where(l => l.ReceivedAt < cutoff).ExecuteDeleteAsync(ct);
        if (removed > 0) LogPurged(logger, removed);
        return removed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã xoá {Count} dòng nhật ký webhook quá {Days} ngày")]
    private static partial void LogPurged(ILogger logger, int count, int days = RetentionDays);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dọn nhật ký webhook thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
