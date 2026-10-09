using System.Security.Cryptography;
using System.Text;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Giao tiếp với phần mềm kiểm soát truy cập (yêu cầu kỹ thuật mục 1: nhận UID thẻ, địa chỉ phòng, giờ quét → đối chiếu lịch đặt
/// → trả cho mở / không). Phần mềm kiểm soát gọi vào API này bằng mã thiết bị + khoá API của thiết bị.
/// - Thẻ quản trị (AccessStaffCard) mở mọi cửa.
/// - Thẻ bạn đọc (Reader.CardUid) mở cửa phòng khi bạn đọc là người đặt hoặc thành viên của lượt đã duyệt / đang dùng, trong khung
///   từ lúc mở check-in tới hết giờ đặt; lần quẹt đầu của lượt chưa check-in thì tự check-in (quá ân hạn thì từ chối — đã vắng mặt).
/// - Thủ thư mở cửa từ web: tạo lệnh OPEN cho các thiết bị của phòng, phần mềm kiểm soát lấy lệnh định kỳ rồi xác nhận.
/// Quyết định luôn theo giờ máy chủ; giờ quét thiết bị gửi chỉ lưu để tra cứu. Mọi lần quẹt/mở đều ghi AccessScanLog.
/// Port ELIB-LRC 10-04. Tenant (K12): thiết bị mang đơn vị của phòng nó mở (cửa chung: đơn vị của thủ thư); quẹt thẻ chỉ đối chiếu bạn
/// đọc, lượt đặt, thẻ quản trị của đơn vị thiết bị (thẻ quản trị TenantId null = tài khoản hệ thống, mở mọi cửa). Màn thủ thư chỉ thấy /
/// sửa thiết bị, thẻ, nhật ký, lệnh mở cửa trong phạm vi đơn vị (<see cref="TenantScope"/>).
/// </summary>
public class AccessControlService(ELIBAPIDbContext db)
{
    public const int SourceReaderCard = 1;
    public const int SourceStaffCard = 2;
    public const int SourceWebUnlock = 3;
    /// <summary>Lệnh mở cửa từ web hết hạn sau 2 phút nếu thiết bị chưa lấy.</summary>
    public static readonly TimeSpan CommandLifetime = TimeSpan.FromMinutes(2);

    public static string HashKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ── Thiết bị ───────────────────────────────────────────────────────────

    public async Task<List<AccessDevice>> DevicesAsync(TenantScope scope)
    {
        var list = await db.AccessDevices.AsNoTracking().Where(d => (scope.All || d.TenantId == scope.TenantId || (scope.IncludeShared && d.TenantId == null))).OrderBy(d => d.Code).ToListAsync();
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(db, list.Where(d => d.TenantId != null).Select(d => d.TenantId!.Value));
        var roomIds = list.Where(d => d.MapObjectId != null).Select(d => d.MapObjectId!.Value).Distinct().ToList();
        var rooms = await db.MapObjects.AsNoTracking().Where(o => roomIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name);
        foreach (var d in list)
        {
            d.RoomName = d.MapObjectId is long id ? rooms.GetValueOrDefault(id) : null;
            d.TenantName = d.TenantId is long t ? tenantNames.GetValueOrDefault(t) : null;
            if (d.LastSeenAt != null) d.LastSeenAt = DateTime.SpecifyKind(d.LastSeenAt.Value, DateTimeKind.Utc);
        }
        return list;
    }

    /// <summary>Thêm (publicId null → trả khoá API gốc, chỉ hiện 1 lần) hoặc sửa thiết bị.</summary>
    public async Task<ServiceResult<DeviceSaveResult>> SaveDeviceAsync(Guid? publicId, DeviceInput input, long userId, TenantScope scope)
    {
        var code = (input.Code ?? "").Trim();
        if (code.Length is 0 or > 100) return ServiceResult<DeviceSaveResult>.BadRequest("Mã thiết bị (địa chỉ cửa) bắt buộc, tối đa 100 ký tự.");
        long? roomTenant = null;
        if (input.MapObjectId != null)
        {
            var room = await db.MapObjects.AsNoTracking().Where(o => o.Id == input.MapObjectId && o.IsDelete != 2 && (scope.All || o.TenantId == scope.TenantId || (scope.IncludeShared && o.TenantId == null)))
                .Select(o => new { o.TenantId }).FirstOrDefaultAsync();
            if (room == null) return ServiceResult<DeviceSaveResult>.BadRequest("Phòng không tồn tại.");
            roomTenant = room.TenantId;
        }
        AccessDevice? device = null;
        if (publicId != null)
        {
            device = await db.AccessDevices.FirstOrDefaultAsync(d => d.PublicId == publicId && (scope.All || d.TenantId == scope.TenantId || (scope.IncludeShared && d.TenantId == null)));
            if (device == null) return ServiceResult<DeviceSaveResult>.NotFound("Không tìm thấy thiết bị.");
        }
        if (await db.AccessDevices.AnyAsync(d => d.Code == code && (device == null || d.Id != device.Id)))
            return ServiceResult<DeviceSaveResult>.BadRequest($"Mã thiết bị \"{code}\" đã tồn tại.");

        string? key = null;
        if (device == null)
        {
            key = NewKey();
            device = db.AccessDevices.Add(new AccessDevice
            {
                ApiKeyHash = HashKey(key), CreatedRowBy = userId, CreatedRowDate = DateTime.UtcNow, PublicId = Guid.NewGuid(),
            }).Entity;
        }
        device.Code = code;
        device.Name = string.IsNullOrWhiteSpace(input.Name) ? null : input.Name.Trim();
        device.MapObjectId = input.MapObjectId;
        // Thiết bị thuộc đơn vị của phòng; cửa chung (không gắn phòng) thuộc đơn vị của thủ thư.
        device.TenantId = input.MapObjectId != null ? roomTenant : device.TenantId ?? scope.WriteTenantId;
        device.IsActive = input.IsActive;
        device.UpdatedRowDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ServiceResult<DeviceSaveResult>.Ok(new DeviceSaveResult(device, key));
    }

    public async Task<ServiceResult<DeviceSaveResult>> RegenerateKeyAsync(Guid publicId, TenantScope scope)
    {
        var device = await db.AccessDevices.FirstOrDefaultAsync(d => d.PublicId == publicId && (scope.All || d.TenantId == scope.TenantId || (scope.IncludeShared && d.TenantId == null)));
        if (device == null) return ServiceResult<DeviceSaveResult>.NotFound("Không tìm thấy thiết bị.");
        var key = NewKey();
        device.ApiKeyHash = HashKey(key);
        device.UpdatedRowDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ServiceResult<DeviceSaveResult>.Ok(new DeviceSaveResult(device, key));
    }

    public async Task<ServiceResult<bool>> DeleteDeviceAsync(Guid publicId, TenantScope scope)
    {
        var device = await db.AccessDevices.FirstOrDefaultAsync(d => d.PublicId == publicId && (scope.All || d.TenantId == scope.TenantId || (scope.IncludeShared && d.TenantId == null)));
        if (device == null) return ServiceResult<bool>.NotFound("Không tìm thấy thiết bị.");
        db.AccessDevices.Remove(device);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Thiết bị đang bật khớp mã + khoá (so hash thời gian hằng). Cập nhật LastSeenAt.</summary>
    public async Task<AccessDevice?> AuthenticateAsync(string? code, string? key)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(key)) return null;
        var device = await db.AccessDevices.FirstOrDefaultAsync(d => d.Code == code.Trim() && d.IsActive);
        if (device == null) return null;
        var given = Encoding.ASCII.GetBytes(HashKey(key.Trim()));
        var stored = Encoding.ASCII.GetBytes(device.ApiKeyHash);
        if (given.Length != stored.Length || !CryptographicOperations.FixedTimeEquals(given, stored)) return null;
        device.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return device;
    }

    // ── Thẻ quản trị ──────────────────────────────────────────────────────

    public Task<List<AccessStaffCard>> StaffCardsAsync(TenantScope scope) => db.AccessStaffCards.AsNoTracking()
        .Where(c => (scope.All || c.TenantId == scope.TenantId || (scope.IncludeShared && c.TenantId == null))).OrderBy(c => c.Label).ThenBy(c => c.CardUid).ToListAsync();

    /// <summary>Thẻ mới thuộc đơn vị của thủ thư (tài khoản hệ thống không chọn đơn vị: thẻ dùng chung, mở mọi cửa). UID không trùng thẻ
    /// quản trị khác cùng đơn vị và không trùng UID bạn đọc của đơn vị đó.</summary>
    public async Task<ServiceResult<AccessStaffCard>> SaveStaffCardAsync(Guid? publicId, StaffCardInput input, long userId, TenantScope scope)
    {
        var uid = CardUid.Normalize(input.CardUid);
        if (!CardUid.IsValid(uid)) return ServiceResult<AccessStaffCard>.BadRequest("UID thẻ chỉ gồm chữ và số, tối đa 64 ký tự.");
        AccessStaffCard? card = null;
        if (publicId != null)
        {
            card = await db.AccessStaffCards.FirstOrDefaultAsync(c => c.PublicId == publicId && (scope.All || c.TenantId == scope.TenantId || (scope.IncludeShared && c.TenantId == null)));
            if (card == null) return ServiceResult<AccessStaffCard>.NotFound("Không tìm thấy thẻ quản trị.");
        }
        var tenantId = card != null ? card.TenantId : scope.WriteTenantId;
        if (await db.AccessStaffCards.AnyAsync(c => c.CardUid == uid && c.TenantId == tenantId && (card == null || c.Id != card.Id)))
            return ServiceResult<AccessStaffCard>.BadRequest($"UID {uid} đã là thẻ quản trị.");
        if (await db.Readers.AnyAsync(r => r.CardUid == uid && r.IsDelete != 2 && (tenantId == null || r.TenantId == tenantId)))
            return ServiceResult<AccessStaffCard>.BadRequest($"UID {uid} đang gán cho một bạn đọc.");
        card ??= db.AccessStaffCards.Add(new AccessStaffCard { CreatedRowBy = userId, CreatedRowDate = DateTime.UtcNow, TenantId = tenantId, PublicId = Guid.NewGuid() }).Entity;
        card.CardUid = uid!;
        card.Label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim();
        card.UserId = input.UserId;
        card.IsActive = input.IsActive;
        await db.SaveChangesAsync();
        return ServiceResult<AccessStaffCard>.Ok(card);
    }

    public async Task<ServiceResult<bool>> DeleteStaffCardAsync(Guid publicId, TenantScope scope)
    {
        var card = await db.AccessStaffCards.FirstOrDefaultAsync(c => c.PublicId == publicId && (scope.All || c.TenantId == scope.TenantId || (scope.IncludeShared && c.TenantId == null)));
        if (card == null) return ServiceResult<bool>.NotFound("Không tìm thấy thẻ quản trị.");
        db.AccessStaffCards.Remove(card);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    // ── Quẹt thẻ ──────────────────────────────────────────────────────────

    public async Task<AccessDecision> VerifyAsync(AccessDevice device, string? rawUid, DateTime? scannedAt)
    {
        var now = DateTime.UtcNow;
        var uid = CardUid.Normalize(rawUid);
        var log = new AccessScanLog
        {
            DeviceId = device.Id, DeviceCode = device.Code, MapObjectId = device.MapObjectId, CardUid = uid, Source = SourceReaderCard,
            ScannedAt = scannedAt == null ? null : RoomBookingHours.ToUtc(scannedAt.Value), CreatedAt = now, TenantId = device.TenantId,
        };
        var decision = await DecideAsync(device, uid, now, log);
        log.Allowed = decision.Allowed;
        log.Reason = decision.Reason;
        db.AccessScanLogs.Add(log);
        await db.SaveChangesAsync();
        return decision;
    }

    private async Task<AccessDecision> DecideAsync(AccessDevice device, string? uid, DateTime now, AccessScanLog log)
    {
        if (!CardUid.IsValid(uid)) return AccessDecision.Deny("UID thẻ không hợp lệ.");

        var staff = await db.AccessStaffCards.AsNoTracking()
            .Where(c => c.CardUid == uid && c.IsActive && (c.TenantId == null || c.TenantId == device.TenantId))
            .OrderByDescending(c => c.TenantId != null).FirstOrDefaultAsync();
        if (staff != null)
        {
            log.Source = SourceStaffCard;
            log.StaffUserId = staff.UserId;
            return AccessDecision.Allow("Thẻ quản trị.", null, staff.Label, null);
        }

        var readers = await db.Readers.AsNoTracking().Where(r => r.CardUid == uid && r.TenantId == device.TenantId && r.IsDelete != 2)
            .Select(r => new { r.Id, r.FirstName, r.LastName }).Take(2).ToListAsync();
        if (readers.Count == 0) return AccessDecision.Deny("Thẻ chưa đăng ký với thư viện.");
        if (readers.Count > 1) return AccessDecision.Deny("UID thẻ trùng nhiều bạn đọc, liên hệ thủ thư.");
        var reader = readers[0];
        log.ReaderId = reader.Id;
        var name = RoomBookingPrivacy.MaskName(RoomBookingPrivacy.FullName(reader.FirstName, reader.LastName));

        if (device.MapObjectId == null) return AccessDecision.Deny("Cửa này chỉ mở bằng thẻ quản trị.");
        var roomId = device.MapObjectId.Value;
        var openFrom = now.AddMinutes(RoomBookingHours.CheckInOpenMinutes);
        var bookings = await db.RoomBookings
            .Where(b => b.MapObjectId == roomId && b.TenantId == device.TenantId && b.IsDelete != 2 && (b.Status == 2 || b.Status == 3)
                && b.StartAt <= openFrom && b.EndAt >= now
                && (b.ReaderId == reader.Id || db.RoomBookingMembers.Any(m => m.BookingId == b.Id && m.ReaderId == reader.Id)))
            .OrderBy(b => b.StartAt).ToListAsync();
        if (bookings.Count == 0) return AccessDecision.Deny("Không có lượt đặt phòng này vào lúc này.");

        var inUse = bookings.FirstOrDefault(b => b.Status == 3);
        if (inUse != null)
        {
            log.BookingId = inUse.Id;
            return AccessDecision.Allow("Đang trong giờ sử dụng.", inUse.PublicId, name, Utc(inUse.EndAt));
        }

        var grace = await db.RoomBookingConfigs.AsNoTracking().Where(c => c.MapObjectId == roomId && c.TenantId == device.TenantId && c.IsDelete != 2)
            .Select(c => (int?)c.CheckInGraceMinutes).MaxAsync() ?? RoomBookingHours.DefaultCheckInGraceMinutes;
        var booking = bookings.FirstOrDefault(b => now <= b.StartAt.AddMinutes(grace));
        if (booking == null)
        {
            log.BookingId = bookings[0].Id;
            return AccessDecision.Deny($"Đã quá thời gian check-in ({grace} phút sau giờ bắt đầu).");
        }
        log.BookingId = booking.Id;
        // Quẹt thẻ lần đầu = check-in (tránh bị tính vắng mặt).
        await db.RoomBookings.Where(b => b.Id == booking.Id && b.Status == 2)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, 3).SetProperty(b => b.CheckedInAt, now).SetProperty(b => b.UpdatedRowDate, now));
        return AccessDecision.Allow("Check-in thành công.", booking.PublicId, name, Utc(booking.EndAt));
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // ── Lệnh mở cửa từ web ───────────────────────────────────────────────

    /// <summary>Tạo lệnh OPEN cho thiết bị chỉ định, hoặc mọi thiết bị đang bật của phòng (theo phòng / theo lượt đặt).</summary>
    public async Task<ServiceResult<int>> UnlockAsync(UnlockInput input, long staffUserId, TenantScope scope)
    {
        long? roomId = input.MapObjectId;
        long? bookingId = null;
        if (input.BookingPublicId != null)
        {
            var booking = await db.RoomBookings.AsNoTracking().Where(b => b.PublicId == input.BookingPublicId && b.IsDelete != 2 && (scope.All || b.TenantId == scope.TenantId || (scope.IncludeShared && b.TenantId == null)))
                .Select(b => new { b.Id, b.MapObjectId }).FirstOrDefaultAsync();
            if (booking == null) return ServiceResult<int>.NotFound("Không tìm thấy lượt đặt phòng.");
            bookingId = booking.Id;
            roomId = booking.MapObjectId;
        }
        var devices = await db.AccessDevices.Where(d => d.IsActive && (scope.All || d.TenantId == scope.TenantId || (scope.IncludeShared && d.TenantId == null))
                && (input.DevicePublicId != null ? d.PublicId == input.DevicePublicId : roomId != null && d.MapObjectId == roomId))
            .ToListAsync();
        if (devices.Count == 0) return ServiceResult<int>.BadRequest("Phòng chưa khai báo thiết bị kiểm soát cửa nào đang hoạt động.");

        var now = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        foreach (var d in devices)
        {
            db.AccessCommands.Add(new AccessCommand
            {
                DeviceId = d.Id, Command = "OPEN", RequestedBy = staffUserId, BookingId = bookingId, Note = note,
                RequestedAt = now, ExpiresAt = now + CommandLifetime, TenantId = d.TenantId, PublicId = Guid.NewGuid(),
            });
            db.AccessScanLogs.Add(new AccessScanLog
            {
                DeviceId = d.Id, DeviceCode = d.Code, MapObjectId = d.MapObjectId, StaffUserId = staffUserId, BookingId = bookingId,
                Source = SourceWebUnlock, Allowed = true, Reason = "Thủ thư mở cửa từ web" + (note == null ? "." : $": {note}"), CreatedAt = now,
                TenantId = d.TenantId,
            });
        }
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(devices.Count);
    }

    /// <summary>Lệnh còn hạn chưa giao của thiết bị — đánh dấu đã giao.</summary>
    public async Task<List<AccessCommand>> PendingCommandsAsync(AccessDevice device)
    {
        var now = DateTime.UtcNow;
        var list = await db.AccessCommands.Where(c => c.DeviceId == device.Id && c.DeliveredAt == null && c.ExpiresAt > now)
            .OrderBy(c => c.RequestedAt).ToListAsync();
        foreach (var c in list) c.DeliveredAt = now;
        if (list.Count > 0) await db.SaveChangesAsync();
        return list;
    }

    public async Task<bool> AckAsync(AccessDevice device, Guid commandPublicId)
    {
        var cmd = await db.AccessCommands.FirstOrDefaultAsync(c => c.PublicId == commandPublicId && c.DeviceId == device.Id);
        if (cmd == null) return false;
        cmd.AckAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    // ── Nhật ký ──────────────────────────────────────────────────────────

    public async Task<PagedResult<AccessScanLog>> LogsAsync(AccessLogFilter f, TenantScope scope)
    {
        var page = Math.Max(1, f.PageIndex);
        var size = Math.Clamp(f.PageSize, 1, 200);
        var q = db.AccessScanLogs.AsNoTracking().Where(l => (scope.All || l.TenantId == scope.TenantId || (scope.IncludeShared && l.TenantId == null)));
        if (f.From != null) q = q.Where(l => l.CreatedAt >= RoomBookingHours.ToUtc(f.From.Value));
        if (f.To != null) q = q.Where(l => l.CreatedAt < RoomBookingHours.ToUtc(f.To.Value));
        if (f.MapObjectId != null) q = q.Where(l => l.MapObjectId == f.MapObjectId);
        if (f.Allowed != null) q = q.Where(l => l.Allowed == f.Allowed);
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = f.Keyword.Trim().ToLower();
            var uid = CardUid.Normalize(f.Keyword);
            var readerIds = db.Readers.Where(r => (r.Cardno ?? "").ToLower().Contains(k)
                || ((r.FirstName ?? "") + " " + (r.LastName ?? "")).ToLower().Contains(k)).Select(r => r.Id);
            q = q.Where(l => (l.ReaderId != null && readerIds.Contains(l.ReaderId.Value)) || l.CardUid == uid || (l.DeviceCode ?? "").ToLower().Contains(k));
        }
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.Id).Skip((page - 1) * size).Take(size).ToListAsync();
        var roomIds = items.Where(l => l.MapObjectId != null).Select(l => l.MapObjectId!.Value).Distinct().ToList();
        var rooms = await db.MapObjects.AsNoTracking().Where(o => roomIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name);
        var ids = items.Where(l => l.ReaderId != null).Select(l => l.ReaderId!.Value).Distinct().ToList();
        var readers = await db.Readers.AsNoTracking().Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.FirstName, r.LastName, r.Cardno }).ToDictionaryAsync(r => r.Id);
        foreach (var l in items)
        {
            l.CreatedAt = Utc(l.CreatedAt);
            l.RoomName = l.MapObjectId is long rid ? rooms.GetValueOrDefault(rid) : null;
            if (l.ReaderId is long id && readers.TryGetValue(id, out var r))
            {
                l.ReaderName = RoomBookingPrivacy.FullName(r.FirstName, r.LastName);
                l.ReaderCardNo = r.Cardno;
            }
        }
        return new PagedResult<AccessScanLog> { Items = items, TotalCount = total, PageIndex = page, PageSize = size };
    }
}

public sealed record AccessDecision(bool Allowed, string Reason, Guid? BookingPublicId, string? Holder, DateTime? ValidUntil)
{
    public static AccessDecision Allow(string reason, Guid? booking, string? holder, DateTime? until) => new(true, reason, booking, holder, until);
    public static AccessDecision Deny(string reason) => new(false, reason, null, null, null);
}

public sealed record DeviceSaveResult(AccessDevice Device, string? ApiKey);

public sealed class DeviceInput
{
    public string? Code        { get; set; }
    public string? Name        { get; set; }
    public long?   MapObjectId { get; set; }
    public bool    IsActive    { get; set; } = true;
}

public sealed class StaffCardInput
{
    public string? CardUid  { get; set; }
    public string? Label    { get; set; }
    public long?   UserId   { get; set; }
    public bool    IsActive { get; set; } = true;
}

public sealed class UnlockInput
{
    public Guid?   DevicePublicId  { get; set; }
    public long?   MapObjectId     { get; set; }
    public Guid?   BookingPublicId { get; set; }
    public string? Note            { get; set; }
}

public sealed class AccessLogFilter
{
    public DateTime? From        { get; set; }
    public DateTime? To          { get; set; }
    public long?     MapObjectId { get; set; }
    public bool?     Allowed     { get; set; }
    public string?   Keyword     { get; set; }
    /// Tài khoản đặc quyền chọn đơn vị (Guid).
    public Guid?     TenantId    { get; set; }
    public int       PageIndex   { get; set; } = 1;
    public int       PageSize    { get; set; } = 20;
}
