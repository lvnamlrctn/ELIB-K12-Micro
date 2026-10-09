using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Quét định kỳ các đặt phòng học nhóm (map.RoomBooking), mirror đúng khuôn BookRequestExpiryJob:
/// 1) Pending quá StartAt mà chưa được duyệt → tự chuyển Rejected (6), giải phóng phòng, báo "hết hạn".
/// 2) Approved quá StartAt + CheckInGraceMinutes mà chưa check-in → NoShow (7), giải phóng phòng, báo "vắng mặt" và xét
///    khoá đặt phòng (RoomBookingBanService) khi vắng quá ROOM_BOOKING_NOSHOW_LIMIT lần.
/// 3) CheckedIn quá EndAt → chuyển Completed (4) — chỉ dọn trạng thái, không gửi email.
/// Chạy 5 phút một lần (Program.cs) để phòng không người nhận được mở lại sớm cho bạn đọc khác (port ELIB-LRC 10-04).
/// StartAt/EndAt lưu UTC — so với UtcNow (trước đây so với DateTime.Now: máy chủ UTC+7 chuyển NoShow/Rejected lệch 7 giờ).
/// Tenant (K12): xử lý theo từng đơn vị — cờ ROOM_BOOKING_ENABLED, ân hạn của phòng và tham số khoá đọc theo đơn vị của lượt đặt
/// (trước đây đọc 1 cờ chung cho mọi đơn vị).
/// </summary>
public class RoomBookingExpiryJob(
    ELIBAPIDbContext db,
    RoomBookingNotifier notifier,
    RoomBookingBanService bans,
    ILogger<RoomBookingExpiryJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var now = DateTime.UtcNow;
        var tenantIds = await db.RoomBookings
            .Where(x => x.IsDelete != 2 && ((x.Status == 1 || x.Status == 2) && x.StartAt < now || x.Status == 3 && x.EndAt < now))
            .Select(x => x.TenantId).Distinct().ToListAsync();
        foreach (var tenantId in tenantIds)
        {
            if (!await RoomBookingParams.IsEnabledAsync(db, "ROOM_BOOKING_ENABLED", tenantId)) continue;
            await RunTenantAsync(tenantId, now);
        }
    }

    private async Task RunTenantAsync(long? tenantId, DateTime now)
    {
        var expiredPending = await db.RoomBookings
            .Where(x => x.TenantId == tenantId && x.IsDelete != 2 && x.Status == 1 && x.StartAt < now)
            .ToListAsync();
        foreach (var b in expiredPending) { b.Status = 6; b.UpdatedRowDate = now; }

        var completed = await db.RoomBookings
            .Where(x => x.TenantId == tenantId && x.IsDelete != 2 && x.Status == 3 && x.EndAt < now)
            .ToListAsync();
        foreach (var b in completed) { b.Status = 4; b.UpdatedRowDate = now; }

        // NoShow cần CheckInGraceMinutes theo từng phòng — join qua RoomBookingConfig cùng đơn vị.
        var approved = await db.RoomBookings
            .Where(x => x.TenantId == tenantId && x.IsDelete != 2 && x.Status == 2 && x.StartAt < now)
            .ToListAsync();
        var mapObjectIds = approved.Select(x => x.MapObjectId).Distinct().ToList();
        var graceByRoom = await db.RoomBookingConfigs
            .Where(c => mapObjectIds.Contains(c.MapObjectId) && c.TenantId == tenantId && c.IsDelete != 2)
            .GroupBy(c => c.MapObjectId)
            .Select(g => new { g.Key, Grace = g.Max(c => c.CheckInGraceMinutes) })
            .ToDictionaryAsync(x => x.Key, x => x.Grace);

        var noShow = approved
            .Where(b => now > b.StartAt.AddMinutes(graceByRoom.TryGetValue(b.MapObjectId, out var g) ? g : RoomBookingHours.DefaultCheckInGraceMinutes))
            .ToList();
        foreach (var b in noShow) { b.Status = 7; b.UpdatedRowDate = now; }

        var changed = expiredPending.Count + completed.Count + noShow.Count;
        if (changed == 0) return;
        await db.SaveChangesAsync();
        logger.LogInformation("RoomBookingExpiryJob (đơn vị {TenantId}): {Rejected} tự từ chối, {NoShow} NoShow, {Completed} hoàn tất",
            tenantId, expiredPending.Count, noShow.Count, completed.Count);

        foreach (var b in expiredPending) await notifier.NotifyAsync(b, RoomBookingNotifier.Event.Expired);
        foreach (var b in noShow) await notifier.NotifyAsync(b, RoomBookingNotifier.Event.NoShow);

        List<RoomBookingBan> newBans = await bans.EvaluateAsync(noShow.Select(b => b.ReaderId), tenantId);
        foreach (var ban in newBans)
        {
            logger.LogInformation("RoomBookingExpiryJob: khoá đặt phòng bạn đọc {ReaderId} đến {Until}", ban.ReaderId, ban.BannedUntil);
            await notifier.NotifyBanAsync(ban);
        }
    }
}
