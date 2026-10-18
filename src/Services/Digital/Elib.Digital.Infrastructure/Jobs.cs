using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Digital.Application;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Digital.Infrastructure;

/// <summary>
/// Quét lượt mượn tài liệu số hết hạn offline (LRC: EbookLoanExpiryJob) mỗi giờ cho từng đơn vị đang hoạt động có phân hệ DIGITAL.
/// Chuyển trạng thái AutoExpired, đẩy hàng đợi đặt trước của tài liệu bị ảnh hưởng, rồi báo bạn đọc (NotificationRequested).
/// Lỗi của một đơn vị không chặn đơn vị khác.
/// </summary>
public sealed partial class EbookLoanExpiryJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<EbookLoanExpiryJob> logger) : BackgroundService
{
    public const string Module = "DIGITAL";
    private const int Batch = 500;

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
            tenants = await scope.ServiceProvider.GetRequiredService<DigitalDbContext>().Set<TenantReplicaRecord>().AsNoTracking()
                .Where(t => t.Status == "Active").OrderBy(t => t.TenantId).Select(t => t.TenantId).ToListAsync(ct);
        }

        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, Module, ct)) continue;
            using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            try
            {
                var (expired, promoted) = await RunForTenantAsync(scope.ServiceProvider, ct);
                if (expired + promoted > 0) LogRan(logger, tenantId, expired, promoted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTenantFailed(logger, tenantId, ex);
            }
        }
    }

    /// <summary>Chạy cho đơn vị đang được chọn trong <see cref="ITenantContext"/> của <paramref name="services"/> — tách riêng để test gọi trực tiếp.</summary>
    public async Task<(int Expired, int Promoted)> RunForTenantAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<DigitalDbContext>();
        var notifier = services.GetRequiredService<EbookReaderNotifier>();
        var allocator = services.GetRequiredService<EbookReservationAllocator>();
        var now = clock.GetUtcNow();

        var expiredLoans = await db.Set<EbookItemLoan>().Where(l => l.Status == EbookLoanStatus.Active && l.ExpiresAt != null && l.ExpiresAt < now)
            .Take(Batch).ToListAsync(ct);
        if (expiredLoans.Count == 0) return (0, 0);
        foreach (var loan in expiredLoans) loan.AutoExpire();
        await db.SaveChangesAsync(ct);

        var readerIds = expiredLoans.Select(l => l.ReaderPublicId).Distinct().ToList();
        var readers = await db.Set<PatronReplica>().AsNoTracking().Where(r => readerIds.Contains(r.ReaderPublicId)).ToDictionaryAsync(r => r.ReaderPublicId, ct);
        var itemIds = expiredLoans.Select(l => l.EbookItemId).Distinct().ToList();
        var titles = await db.Set<EbookItem>().AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Title, ct);

        foreach (var loan in expiredLoans)
        {
            await notifier.NotifyAsync(readers.GetValueOrDefault(loan.ReaderPublicId), EbookTemplates.LoanExpired, new Dictionary<string, string>
            {
                ["title"] = titles.GetValueOrDefault(loan.EbookItemId) ?? "",
                ["expired_at"] = now.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            }, $"ebook-loan-expired:{loan.Id}", ct);
        }
        await db.SaveChangesAsync(ct); // Publish trên đã đưa vào outbox — lưu cùng để gửi được.

        var promoted = 0;
        foreach (var itemId in itemIds) promoted += await allocator.PromoteAsync(itemId, ct);
        return (expiredLoans.Count, promoted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: tự hết hạn {Expired} lượt mượn tài liệu số, đẩy {Promoted} đặt trước lên sẵn sàng")]
    private static partial void LogRan(ILogger logger, long tenantId, int expired, int promoted);

    [LoggerMessage(Level = LogLevel.Error, Message = "Việc định kỳ tài liệu số lỗi ở đơn vị {TenantId}")]
    private static partial void LogTenantFailed(ILogger logger, long tenantId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Việc định kỳ tài liệu số lỗi")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
