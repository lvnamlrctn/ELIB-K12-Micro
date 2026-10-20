using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Space.Application;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.Space.Infrastructure;

/// <summary>
/// Quét lượt đặt phòng quá hạn mỗi 5 phút cho từng đơn vị đang hoạt động có phân hệ SPACE (monolith: RoomBookingExpiryJob,
/// cùng chu kỳ). Ba nhánh độc lập: Chờ duyệt quá giờ → Từ chối tự động; Đã check-in quá giờ kết thúc → Hoàn tất (không báo);
/// Đã duyệt quá giờ check-in → Không tới, rồi đánh giá cấm tự động. Lỗi của một đơn vị không chặn đơn vị khác.
/// </summary>
public sealed partial class SpaceBookingExpiryJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SpaceBookingExpiryJob> logger) : BackgroundService
{
    public const string Module = "SPACE";
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
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), clock);
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
            tenants = await scope.ServiceProvider.GetRequiredService<SpaceDbContext>().Set<TenantReplicaRecord>().AsNoTracking()
                .Where(t => t.Status == "Active").OrderBy(t => t.TenantId).Select(t => t.TenantId).ToListAsync(ct);
        }

        foreach (var tenantId in tenants)
        {
            using var scope = scopes.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<IModuleLicenseSource>().IsLicensedAsync(tenantId, Module, ct)) continue;
            using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            try
            {
                var result = await RunForTenantAsync(scope.ServiceProvider, ct);
                if (result.Rejected + result.Completed + result.NoShow + result.Banned > 0) LogRan(logger, tenantId, result.Rejected, result.Completed, result.NoShow, result.Banned);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTenantFailed(logger, tenantId, ex);
            }
        }
    }

    public readonly record struct JobResult(int Rejected, int Completed, int NoShow, int Banned);

    /// <summary>Chạy cho đơn vị đang được chọn trong <see cref="ITenantContext"/> của <paramref name="services"/> — tách riêng để test gọi trực tiếp.</summary>
    public async Task<JobResult> RunForTenantAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<SpaceDbContext>();
        var notifier = services.GetRequiredService<SpaceReaderNotifier>();
        var settings = services.GetRequiredService<SettingsResource>();
        var now = clock.GetUtcNow();

        var rejected = await ExpirePendingAsync(db, notifier, now, ct);
        var completed = await AutoCompleteAsync(db, now, ct);
        var (noShow, noShowReaderIds) = await MarkNoShowAsync(db, notifier, now, ct);
        var banned = noShowReaderIds.Count == 0 ? 0 : await EvaluateBansAsync(db, notifier, settings, noShowReaderIds, now, ct);

        return new JobResult(rejected, completed, noShow, banned);
    }

    private static async Task<int> ExpirePendingAsync(SpaceDbContext db, SpaceReaderNotifier notifier, DateTimeOffset now, CancellationToken ct)
    {
        var bookings = await db.Set<RoomBooking>().Where(b => b.Status == RoomBookingStatus.Pending && b.StartAt < now).Take(Batch).ToListAsync(ct);
        if (bookings.Count == 0) return 0;
        foreach (var booking in bookings) booking.AutoExpireToRejected(now);
        await db.SaveChangesAsync(ct);
        foreach (var booking in bookings) await NotifyAsync(db, notifier, booking, SpaceTemplates.Expired, ct);
        await db.SaveChangesAsync(ct);
        return bookings.Count;
    }

    private static async Task<int> AutoCompleteAsync(SpaceDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var bookings = await db.Set<RoomBooking>().Where(b => b.Status == RoomBookingStatus.CheckedIn && b.EndAt < now).Take(Batch).ToListAsync(ct);
        if (bookings.Count == 0) return 0;
        foreach (var booking in bookings) booking.AutoComplete(now);
        await db.SaveChangesAsync(ct);
        return bookings.Count;
    }

    private static async Task<(int Count, List<Guid> ReaderIds)> MarkNoShowAsync(SpaceDbContext db, SpaceReaderNotifier notifier, DateTimeOffset now, CancellationToken ct)
    {
        var candidates = await db.Set<RoomBooking>().Where(b => b.Status == RoomBookingStatus.Approved && b.StartAt < now)
            .Select(b => new { b.Id, b.SpaceId, b.StartAt }).Take(Batch).ToListAsync(ct);
        if (candidates.Count == 0) return (0, []);
        var spaceIds = candidates.Select(c => c.SpaceId).Distinct().ToList();
        var graceBySpace = await db.Set<BookableSpace>().AsNoTracking().Where(s => spaceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.CheckInGraceMinutes, ct);
        var dueIds = candidates.Where(c => now > c.StartAt.AddMinutes(graceBySpace.GetValueOrDefault(c.SpaceId, BookableSpace.DefaultCheckInGraceMinutes)))
            .Select(c => c.Id).ToList();
        if (dueIds.Count == 0) return (0, []);

        var bookings = await db.Set<RoomBooking>().Where(b => dueIds.Contains(b.Id)).ToListAsync(ct);
        foreach (var booking in bookings) booking.MarkNoShow(now);
        await db.SaveChangesAsync(ct);
        foreach (var booking in bookings) await NotifyAsync(db, notifier, booking, SpaceTemplates.NoShow, ct);
        await db.SaveChangesAsync(ct);
        return (bookings.Count, [.. bookings.Select(b => b.ReaderPublicId).Distinct()]);
    }

    private static async Task<int> EvaluateBansAsync(SpaceDbContext db, SpaceReaderNotifier notifier, SettingsResource settingsResource,
        List<Guid> noShowReaderIds, DateTimeOffset now, CancellationToken ct)
    {
        var settings = await settingsResource.GetAsync(ct);
        if (settings.NoShowLimit <= 0) return 0;
        var since = now.AddDays(-settings.NoShowWindowDays);
        var banned = 0;
        foreach (var readerId in noShowReaderIds)
        {
            if (await db.Set<RoomBookingBan>().AnyAsync(b => b.ReaderPublicId == readerId && b.LiftedAt == null && b.BannedUntil > now, ct)) continue;
            var count = await db.Set<RoomBooking>().CountAsync(b => b.ReaderPublicId == readerId && b.Status == RoomBookingStatus.NoShow && b.StartAt >= since, ct);
            if (count < settings.NoShowLimit) continue;

            var reader = await db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == readerId, ct);
            if (reader is null) continue;
            var ban = RoomBookingBan.Create(readerId, reader.CardNo,
                $"Vắng mặt {count} lượt đặt phòng trong {settings.NoShowWindowDays} ngày gần đây.", RoomBookingBanSource.Auto, now.AddDays(settings.BanDays));
            db.Set<RoomBookingBan>().Add(ban);
            await db.SaveChangesAsync(ct);
            await notifier.NotifyAsync(reader, SpaceTemplates.Banned, new Dictionary<string, string>
            {
                ["reason"] = ban.Reason,
                ["banned_until"] = ban.BannedUntil.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
            }, $"{SpaceTemplates.Banned}:{ban.Id}", ct);
            await db.SaveChangesAsync(ct);
            banned++;
        }
        return banned;
    }

    private static async Task NotifyAsync(SpaceDbContext db, SpaceReaderNotifier notifier, RoomBooking booking, string template, CancellationToken ct)
    {
        var reader = await db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == booking.ReaderPublicId, ct);
        var spaceName = await db.Set<BookableSpace>().AsNoTracking().Where(s => s.Id == booking.SpaceId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        var tokens = new Dictionary<string, string>
        {
            ["space_name"] = spaceName ?? "",
            ["start_at"] = booking.StartAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            ["end_at"] = booking.EndAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        };
        await notifier.NotifyAsync(reader, template, tokens, $"{template}:{booking.Id}", ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: {Rejected} lượt tự từ chối, {Completed} lượt tự hoàn tất, {NoShow} lượt không tới, {Banned} lệnh cấm mới")]
    private static partial void LogRan(ILogger logger, long tenantId, int rejected, int completed, int noShow, int banned);

    [LoggerMessage(Level = LogLevel.Error, Message = "Việc định kỳ đặt phòng lỗi ở đơn vị {TenantId}")]
    private static partial void LogTenantFailed(ILogger logger, long tenantId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Việc định kỳ đặt phòng lỗi")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
