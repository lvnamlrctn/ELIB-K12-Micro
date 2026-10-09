using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Xem <see cref="IRoomBookingPortalService"/> (tách từ RoomBookingController phía OPAC). Tenant (K12): lọc theo tenantId
/// truyền vào ở mọi truy vấn.</summary>
public class RoomBookingPortalService(ELIBAPIDbContext db) : IRoomBookingPortalService
{
    private const string EnabledKey = "ROOM_BOOKING_ENABLED";

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public async Task<(bool Enabled, List<BookableRoom> Rooms)> RoomsAsync(long? tenantId, long? readerId = null)
    {
        if (!await RoomBookingParams.IsEnabledAsync(db, EnabledKey, tenantId)) return (false, []);
        var rooms = await (
            from c in db.RoomBookingConfigs
            where c.IsDelete != 2 && c.Status == 2 && c.TenantId == tenantId
            join o in db.MapObjects on c.MapObjectId equals o.Id
            where o.IsDelete != 2 && o.TenantId == tenantId
            orderby o.Name
            select new { o.Id, o.Name, o.IconName, c.Capacity, c.SlotMinutes, c.AutoApprove, c.Rules, c.AllowedReaderTypeIds, c.MinGroupSize, c.Maintenance, c.MaintenanceNote }).ToListAsync();
        var audience = await AudienceAsync(rooms.Select(r => r.AllowedReaderTypeIds), readerId);
        return (true, rooms.Select(r =>
        {
            var (names, allowed) = audience(r.AllowedReaderTypeIds);
            return new BookableRoom(r.Id, r.Name, r.IconName, r.Capacity, r.SlotMinutes, r.AutoApprove, r.Rules, names, allowed,
                Math.Max(1, r.MinGroupSize ?? 1), r.Maintenance, r.MaintenanceNote);
        }).ToList());
    }

    /// <summary>Đối tượng được đặt của từng phòng: tên loại bạn đọc + bạn đọc đang đăng nhập có thuộc đối tượng không.</summary>
    private async Task<Func<string?, (List<string> Names, bool? Allowed)>> AudienceAsync(IEnumerable<string?> rawIds, long? readerId)
    {
        var allIds = rawIds.SelectMany(RoomBookingRules.ParseIds).Distinct().ToList();
        var typeNames = allIds.Count == 0 ? new Dictionary<long, string>() : await db.ReaderTypes.AsNoTracking()
            .Where(t => allIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);
        long? readerType = readerId == null ? null
            : await db.Readers.AsNoTracking().Where(r => r.Id == readerId).Select(r => r.ReaderTypeId).FirstOrDefaultAsync();
        return raw =>
        {
            var ids = RoomBookingRules.ParseIds(raw);
            var names = ids.Select(id => typeNames.GetValueOrDefault(id)).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
            bool? allowed = readerId == null ? null : ids.Count == 0 || (readerType != null && ids.Contains(readerType.Value));
            return (names, allowed);
        };
    }

    /// <summary>Trước đây lấy ngày theo UTC (date.Date) và trả giờ không kèm "Z" nên trình duyệt hiểu là giờ địa phương —
    /// lệch 7 giờ so với FloorBoard.</summary>
    public async Task<List<RoomBusySlot>> AvailabilityAsync(long mapObjectId, DateTime date, long? tenantId)
    {
        var (dayStart, dayEnd) = RoomBookingHours.LibraryDayUtcRange(date);
        var rows = await db.RoomBookings.AsNoTracking()
            .Where(b => b.MapObjectId == mapObjectId && b.TenantId == tenantId && b.IsDelete != 2 && (b.Status == 1 || b.Status == 2 || b.Status == 3)
                        && b.StartAt < dayEnd && b.EndAt > dayStart)
            .OrderBy(b => b.StartAt)
            .Select(b => new { b.StartAt, b.EndAt, b.Status }).ToListAsync();
        return rows.Select(b => new RoomBusySlot(Utc(b.StartAt), Utc(b.EndAt), b.Status)).ToList();
    }

    public async Task<RoomFloorBoard> FloorBoardAsync(long floorId, DateTime date, long? tenantId, long? readerId, bool staffView = false)
    {
        var hoursOf = await RoomBookingHours.DayResolverAsync(db, tenantId, date);
        var fallback = hoursOf(null);
        if (!staffView && !await RoomBookingParams.IsEnabledAsync(db, EnabledKey, tenantId))
            return new RoomFloorBoard(false, RoomBookingHours.Format(fallback.Open), RoomBookingHours.Format(fallback.Close), RoomBookingHours.StepMinutes, []);

        var rooms = await (
            from c in db.RoomBookingConfigs
            where c.IsDelete != 2 && c.Status == 2 && c.TenantId == tenantId
            join o in db.MapObjects on c.MapObjectId equals o.Id
            where o.IsDelete != 2 && o.FloorId == floorId && o.TenantId == tenantId
            orderby o.Name
            select new { o.Id, o.Name, o.IconName, o.Category, c.Capacity, c.MinAdvanceMinutes, c.MaxAdvanceDays, c.MaxAdvanceHours, c.AutoApprove,
                c.Rules, c.AllowedReaderTypeIds, c.MinGroupSize, c.Maintenance, c.MaintenanceNote }).ToListAsync();
        var roomIds = rooms.Select(r => r.Id).ToList();
        var audience = await AudienceAsync(rooms.Select(r => r.AllowedReaderTypeIds), readerId);

        var equipment = await db.MapEquipments.AsNoTracking()
            .Where(e => roomIds.Contains(e.ObjectId) && e.IsDelete != 2)
            .OrderBy(e => e.Name)
            .Select(e => new { e.ObjectId, e.Name, e.Quantity }).ToListAsync();

        var (dayStart, dayEnd) = RoomBookingHours.LibraryDayUtcRange(date);
        var busy = await db.RoomBookings.AsNoTracking()
            .Where(b => roomIds.Contains(b.MapObjectId) && b.TenantId == tenantId && b.IsDelete != 2
                        && (b.Status == 1 || b.Status == 2 || b.Status == 3)
                        && b.StartAt < dayEnd && b.EndAt > dayStart)
            .OrderBy(b => b.StartAt)
            .Select(b => new { b.Id, b.PublicId, b.MapObjectId, b.StartAt, b.EndAt, b.Status, b.ReaderId, b.PartySize, b.Note }).ToListAsync();
        var busyIds = busy.Select(b => b.Id).ToList();
        var memberRows = await db.RoomBookingMembers.AsNoTracking().Where(m => busyIds.Contains(m.BookingId))
            .Select(m => new { m.BookingId, m.ReaderId }).ToListAsync();
        var readerIds = busy.Select(b => b.ReaderId).Concat(memberRows.Select(m => m.ReaderId)).Distinct().ToList();
        var readers = await db.Readers.AsNoTracking()
            .Where(r => readerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.FirstName, r.LastName, r.Cardno })
            .ToDictionaryAsync(r => r.Id);

        var now = DateTime.UtcNow;
        var result = rooms.Select(r =>
        {
            var hours = hoursOf(r.Category);
            var roomBusy = busy.Where(b => b.MapObjectId == r.Id).ToList();
            var (names, allowed) = audience(r.AllowedReaderTypeIds);
            return new RoomBoardRoom(r.Id, r.Name, r.IconName, r.Capacity, r.MinAdvanceMinutes, r.MaxAdvanceDays, r.AutoApprove,
                equipment.Where(e => e.ObjectId == r.Id).Select(e => new RoomEquipmentItem(e.Name, e.Quantity)).ToList(),
                roomBusy.Select(b =>
                {
                    var members = memberRows.Where(m => m.BookingId == b.Id).Select(m => m.ReaderId).ToList();
                    var isMine = readerId != null && (b.ReaderId == readerId || members.Contains(readerId.Value));
                    var reader = readers.GetValueOrDefault(b.ReaderId);
                    var name = RoomBookingPrivacy.FullName(reader?.FirstName, reader?.LastName);
                    var open = isMine || staffView;
                    return new RoomBoardBusy(Utc(b.StartAt), Utc(b.EndAt), b.Status, isMine,
                        open ? name : RoomBookingPrivacy.MaskName(name),
                        open ? reader?.Cardno : RoomBookingPrivacy.MaskCardNo(reader?.Cardno),
                        b.PartySize, open ? b.Note : null,
                        staffView ? b.PublicId : null,
                        staffView ? members.Select(id => readers.GetValueOrDefault(id)).Where(x => x != null)
                            .Select(x => $"{RoomBookingPrivacy.FullName(x!.FirstName, x.LastName)} ({x.Cardno})").ToList() : null);
                }).ToList(),
                r.MaxAdvanceHours, r.Rules, names, allowed,
                RoomBookingHours.Format(hours.Open), RoomBookingHours.Format(hours.Close),
                hours.Closed || r.Maintenance,
                r.Maintenance ? "Tạm ngưng phục vụ" + (string.IsNullOrWhiteSpace(r.MaintenanceNote) ? "" : $": {r.MaintenanceNote}") : hours.Closed ? hours.Reason : null,
                hours.Closed || r.Maintenance ? null : EarliestFree(dayStart, hours.Open, hours.Close, now.AddMinutes(r.MinAdvanceMinutes),
                    roomBusy.Select(b => (b.StartAt, b.EndAt)).ToList()),
                Math.Max(1, r.MinGroupSize ?? 1), r.Maintenance, r.MaintenanceNote);
        }).ToList();

        // Trục giờ của lịch = khung rộng nhất của các phòng còn mở; cả tầng đóng cửa thì dùng giờ chung của ngày.
        var open = result.Where(r => !r.Closed).ToList();
        var axisOpen = open.Count == 0 ? RoomBookingHours.Format(fallback.Open) : open.Min(r => r.OpenTime)!;
        var axisClose = open.Count == 0 ? RoomBookingHours.Format(fallback.Close) : open.Max(r => r.CloseTime)!;
        return new RoomFloorBoard(true, axisOpen, axisClose, RoomBookingHours.StepMinutes, result);
    }

    /// <summary>Ô 30 phút đầu tiên trong giờ mở cửa, không sớm hơn <paramref name="notBefore"/> và không trùng lượt đang giữ phòng.</summary>
    internal static DateTime? EarliestFree(DateTime dayStartUtc, TimeSpan open, TimeSpan close, DateTime notBefore, List<(DateTime Start, DateTime End)> busy)
    {
        var step = TimeSpan.FromMinutes(RoomBookingHours.StepMinutes);
        for (var t = dayStartUtc + open; t + step <= dayStartUtc + close; t += step)
        {
            if (t < notBefore) continue;
            var end = t + step;
            if (!busy.Any(b => b.Start < end && b.End > t)) return Utc(t);
        }
        return null;
    }

    public async Task<List<MyRoomBooking>> MyBookingsAsync(long readerId, long? tenantId)
    {
        var memberOf = db.RoomBookingMembers.Where(m => m.ReaderId == readerId).Select(m => m.BookingId);
        var rows = await (
            from b in db.RoomBookings
            where (b.ReaderId == readerId || memberOf.Contains(b.Id)) && b.TenantId == tenantId && b.IsDelete != 2
            join o in db.MapObjects on b.MapObjectId equals o.Id into oj
            from o in oj.DefaultIfEmpty()
            orderby b.StartAt descending
            select new { b.Id, b.PublicId, b.MapObjectId, b.ReaderId, RoomName = o != null ? o.Name : null, b.StartAt, b.EndAt, b.PartySize, b.Status, b.Note }).ToListAsync();
        var roomIds = rows.Select(r => r.MapObjectId).Distinct().ToList();
        var grace = await db.RoomBookingConfigs.AsNoTracking().Where(c => roomIds.Contains(c.MapObjectId) && c.TenantId == tenantId && c.IsDelete != 2)
            .GroupBy(c => c.MapObjectId).Select(g => new { g.Key, Grace = g.Max(c => c.CheckInGraceMinutes) }).ToDictionaryAsync(x => x.Key, x => x.Grace);
        var ids = rows.Select(r => r.Id).ToList();
        var members = await db.RoomBookingMembers.AsNoTracking().Where(m => ids.Contains(m.BookingId)).Select(m => new { m.BookingId, m.ReaderId }).ToListAsync();
        var peopleIds = members.Select(m => m.ReaderId).Concat(rows.Select(r => r.ReaderId)).Distinct().ToList();
        var people = await db.Readers.AsNoTracking().Where(r => peopleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.FirstName, r.LastName }).ToDictionaryAsync(r => r.Id, r => RoomBookingPrivacy.FullName(r.FirstName, r.LastName));
        return rows.Select(r => new MyRoomBooking(r.PublicId, r.RoomName, Utc(r.StartAt), Utc(r.EndAt), r.PartySize, r.Status,
            r.ReaderId == readerId ? r.Note : null,
            Utc(r.StartAt.AddMinutes(grace.TryGetValue(r.MapObjectId, out var g) ? g : RoomBookingHours.DefaultCheckInGraceMinutes)),
            r.ReaderId == readerId, people.GetValueOrDefault(r.ReaderId),
            members.Where(m => m.BookingId == r.Id).Select(m => people.GetValueOrDefault(m.ReaderId) ?? "").ToList())).ToList();
    }

    public async Task<MemberCardLookup?> LookupMemberAsync(string card, long currentReaderId, long? tenantId)
    {
        var raw = (card ?? "").Trim();
        if (raw.Length == 0 || raw.Length > 64) return null;
        var lower = raw.ToLower();
        var uid = ELIBAPI.Core.Common.CardUid.Normalize(raw);
        var matches = await db.Readers.AsNoTracking()
            .Where(r => r.IsDelete != 2 && r.Id != currentReaderId && r.TenantId == tenantId && ((r.Cardno != null && r.Cardno.ToLower() == lower) || (uid != null && r.CardUid == uid)))
            .Select(r => new { r.FirstName, r.LastName }).Take(2).ToListAsync();
        return matches.Count == 1
            ? new MemberCardLookup(raw, RoomBookingPrivacy.MaskName(RoomBookingPrivacy.FullName(matches[0].FirstName, matches[0].LastName)))
            : null;
    }
}
