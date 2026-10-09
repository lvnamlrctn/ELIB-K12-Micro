using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Danh sách đen đặt phòng (map.RoomBookingBan). Tự khoá: vắng mặt (Status 7) ≥ ROOM_BOOKING_NOSHOW_LIMIT lần trong
/// ROOM_BOOKING_NOSHOW_WINDOW_DAYS ngày → khoá ROOM_BOOKING_BAN_DAYS ngày. Chỉ đếm các lần vắng mặt SAU lệnh khoá gần nhất
/// (đã gỡ hoặc hết hạn), để cùng những lần vắng cũ không khoá lặp lại ngay khi vừa được gỡ.
/// Port ELIB-LRC 10-04. Tenant (K12): lệnh khoá mang đơn vị của bạn đọc; tham số khoá đọc theo đơn vị; danh sách / gỡ khoá / khoá tay
/// chỉ trong phạm vi đơn vị của thủ thư (<see cref="TenantScope"/>).
/// </summary>
public class RoomBookingBanService(ELIBAPIDbContext db)
{
    public const int SourceAuto = 1;
    public const int SourceStaff = 2;

    public Task<RoomBookingBan?> ActiveBanAsync(long readerId)
    {
        var now = DateTime.UtcNow;
        return db.RoomBookingBans.AsNoTracking()
            .Where(b => b.ReaderId == readerId && b.LiftedAt == null && b.BannedUntil > now)
            .OrderByDescending(b => b.BannedUntil).FirstOrDefaultAsync();
    }

    /// <summary>Xét khoá tự động cho các bạn đọc (cùng 1 đơn vị) vừa có lượt vắng mặt; trả các lệnh khoá mới tạo (đã lưu) để gửi thông báo.</summary>
    public async Task<List<RoomBookingBan>> EvaluateAsync(IEnumerable<long> readerIds, long? tenantId)
    {
        var rules = await RoomBookingRules.GetAsync(db, tenantId);
        var ids = readerIds.Distinct().ToList();
        if (rules.NoShowLimit <= 0 || ids.Count == 0) return [];

        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(-rules.NoShowWindowDays);
        var bans = await db.RoomBookingBans.AsNoTracking().Where(b => ids.Contains(b.ReaderId)).ToListAsync();
        var noShows = await db.RoomBookings.AsNoTracking()
            .Where(b => ids.Contains(b.ReaderId) && b.TenantId == tenantId && b.IsDelete != 2 && b.Status == 7 && b.StartAt >= windowStart)
            .Select(b => new { b.ReaderId, b.StartAt }).ToListAsync();

        var created = new List<RoomBookingBan>();
        foreach (var readerId in ids)
        {
            var mine = bans.Where(b => b.ReaderId == readerId).ToList();
            if (mine.Any(b => b.LiftedAt == null && b.BannedUntil > now)) continue;
            // Mốc kết thúc lệnh khoá gần nhất: gỡ sớm → LiftedAt, không thì BannedUntil.
            var lastEnd = mine.Select(b => b.LiftedAt ?? b.BannedUntil).DefaultIfEmpty(DateTime.MinValue).Max();
            var count = noShows.Count(n => n.ReaderId == readerId && n.StartAt > lastEnd);
            if (count < rules.NoShowLimit) continue;

            var ban = new RoomBookingBan
            {
                ReaderId = readerId, Source = SourceAuto, BannedUntil = now.AddDays(rules.BanDays),
                Reason = $"Vắng mặt {count} lượt đặt phòng trong {rules.NoShowWindowDays} ngày gần đây.",
                CreatedRowDate = now, TenantId = tenantId, PublicId = Guid.NewGuid(),
            };
            db.RoomBookingBans.Add(ban);
            created.Add(ban);
        }
        if (created.Count > 0) await db.SaveChangesAsync();
        return created;
    }

    public async Task<PagedResult<RoomBookingBan>> ListAsync(TenantScope scope, bool activeOnly, string? keyword, int page, int pageSize)
    {
        var now = DateTime.UtcNow;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.RoomBookingBans.AsNoTracking()
            .Where(b => scope.All || b.TenantId == scope.TenantId || (scope.IncludeShared && b.TenantId == null));
        if (activeOnly) q = q.Where(b => b.LiftedAt == null && b.BannedUntil > now);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            var readerIds = db.Readers.Where(r => (r.Cardno ?? "").ToLower().Contains(k)
                || ((r.FirstName ?? "") + " " + (r.LastName ?? "")).ToLower().Contains(k)).Select(r => r.Id);
            q = q.Where(b => readerIds.Contains(b.ReaderId));
        }
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(b => b.CreatedRowDate).ThenByDescending(b => b.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ids = items.Select(b => b.ReaderId).Distinct().ToList();
        var readers = await db.Readers.AsNoTracking().Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.FirstName, r.LastName, r.Cardno }).ToDictionaryAsync(r => r.Id);
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(db, items.Where(b => b.TenantId != null).Select(b => b.TenantId!.Value));
        foreach (var b in items)
        {
            b.BannedUntil = DateTime.SpecifyKind(b.BannedUntil, DateTimeKind.Utc);
            if (b.LiftedAt != null) b.LiftedAt = DateTime.SpecifyKind(b.LiftedAt.Value, DateTimeKind.Utc);
            if (b.CreatedRowDate != null) b.CreatedRowDate = DateTime.SpecifyKind(b.CreatedRowDate.Value, DateTimeKind.Utc);
            b.TenantName = b.TenantId is long t ? tenantNames.GetValueOrDefault(t) : null;
            if (readers.TryGetValue(b.ReaderId, out var r))
            {
                b.ReaderName = RoomBookingPrivacy.FullName(r.FirstName, r.LastName);
                b.ReaderCardNo = r.Cardno;
            }
        }
        return new PagedResult<RoomBookingBan> { Items = items, TotalCount = total, PageIndex = page, PageSize = pageSize };
    }

    /// <summary>Thủ thư khoá tay. Bạn đọc phải tồn tại và thuộc phạm vi đơn vị của thủ thư; số ngày 1–365.</summary>
    public async Task<ServiceResult<RoomBookingBan>> AddAsync(long readerId, int days, string? reason, long staffUserId, TenantScope scope)
    {
        if (days is < 1 or > 365) return ServiceResult<RoomBookingBan>.BadRequest("Số ngày khoá phải từ 1 đến 365.");
        if (string.IsNullOrWhiteSpace(reason)) return ServiceResult<RoomBookingBan>.BadRequest("Vui lòng nhập lý do khoá.");
        var reader = await db.Readers.AsNoTracking().Where(r => r.Id == readerId && r.IsDelete != 2
                && (scope.All || r.TenantId == scope.TenantId || (scope.IncludeShared && r.TenantId == null)))
            .Select(r => new { r.Id, r.TenantId }).FirstOrDefaultAsync();
        if (reader == null) return ServiceResult<RoomBookingBan>.NotFound("Không tìm thấy bạn đọc.");
        if (await ActiveBanAsync(readerId) != null)
            return ServiceResult<RoomBookingBan>.BadRequest("Bạn đọc đang bị khoá đặt phòng.");

        var now = DateTime.UtcNow;
        var ban = new RoomBookingBan
        {
            ReaderId = readerId, Source = SourceStaff, BannedUntil = now.AddDays(days), Reason = reason.Trim(),
            CreatedRowBy = staffUserId, CreatedRowDate = now, TenantId = reader.TenantId, PublicId = Guid.NewGuid(),
        };
        db.RoomBookingBans.Add(ban);
        await db.SaveChangesAsync();
        return ServiceResult<RoomBookingBan>.Ok(ban);
    }

    public async Task<ServiceResult<bool>> LiftAsync(Guid publicId, long staffUserId, TenantScope scope)
    {
        var ban = await db.RoomBookingBans.FirstOrDefaultAsync(b => b.PublicId == publicId
            && (scope.All || b.TenantId == scope.TenantId || (scope.IncludeShared && b.TenantId == null)));
        if (ban == null) return ServiceResult<bool>.NotFound("Không tìm thấy lệnh khoá.");
        if (ban.LiftedAt != null || ban.BannedUntil <= DateTime.UtcNow) return ServiceResult<bool>.BadRequest("Lệnh khoá này đã hết hiệu lực.");
        ban.LiftedAt = DateTime.UtcNow;
        ban.LiftedBy = staffUserId;
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }
}
