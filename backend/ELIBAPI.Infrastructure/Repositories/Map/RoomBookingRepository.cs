using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace ELIBAPI.Infrastructure.Repositories;

// Đặt phòng học nhóm — mirror đúng khuôn nghiệp vụ viết tay thêm của EbookItemReservationRepository
// (tuple (Ok, Error, StatusCode, Entity), IEmailService/ISystemParameterService best-effort). Khác biệt
// chính: booking khởi tạo = 1 Pending chờ nhân viên duyệt, trừ phòng cấu hình AutoApprove (tạo thẳng
// = 2 Approved, ApprovedBy = null nghĩa là hệ thống duyệt); và có thêm bước check-in tại phòng
// (RoomBookingExpiryJob tự NoShow nếu quá giờ ân hạn).
// Port ELIB-LRC 09-28..10-04 (giờ UTC, giờ mở cửa, quy định đặt, nhóm, trả phòng, QR check-in, ân hạn check-in, khoá đặt phòng).
// Tenant (K12): thao tác phía thủ thư (Search/Export/Approve/Reject/StaffCheckIn/CheckOut) lọc theo JWT bằng ApplyTenantFilter; thao
// tác phía OPAC (Create/CheckIn/Cancel/CheckOut của bạn đọc) nhận tenantId tường minh = Reader.TenantId (bạn đọc không có claim đơn
// vị). Cấu hình phòng, giờ mở cửa, tham số, thành viên nhóm (thẻ phải cùng đơn vị) đều theo đơn vị của lượt đặt.
public class RoomBookingRepository : BaseRepository<RoomBooking, RoomBookingSearchRequest, RoomBookingRequest>, IRoomBookingRepository
{
    private readonly RoomBookingNotifier _notifier;
    private readonly RoomBookingBanService _bans;

    public RoomBookingRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http, IEmailService emailService,
        ISystemParameterService sysParam, INotificationDispatcher notificationDispatcher)
        : base(ctx, http)
    {
        _notifier     = new RoomBookingNotifier(ctx, emailService, notificationDispatcher);
        _bans         = new RoomBookingBanService(ctx);
    }

    protected override IQueryable<RoomBooking> BuildQuery(RoomBookingSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.MapObjectId.HasValue) q = q.Where(x => x.MapObjectId == r.MapObjectId);
        if (r.ReaderId.HasValue)    q = q.Where(x => x.ReaderId == r.ReaderId);
        if (r.Status.HasValue)     q = q.Where(x => x.Status == r.Status);
        if (r.DateFrom.HasValue)   q = q.Where(x => x.StartAt >= r.DateFrom);
        if (r.DateTo.HasValue)     q = q.Where(x => x.StartAt <= r.DateTo);
        if (r.Category.HasValue)
        {
            var rooms = _context.MapObjects.Where(o => o.Category == r.Category).Select(o => o.Id);
            q = q.Where(x => rooms.Contains(x.MapObjectId));
        }
        if (!string.IsNullOrWhiteSpace(r.Keyword))
        {
            var k = r.Keyword.Trim().ToLower();
            var readers = _context.Readers.Where(x => (x.Cardno ?? "").ToLower().Contains(k)
                || ((x.FirstName ?? "") + " " + (x.LastName ?? "")).ToLower().Contains(k)).Select(x => x.Id);
            q = q.Where(x => readers.Contains(x.ReaderId)
                || _context.RoomBookingMembers.Any(m => m.BookingId == x.Id && readers.Contains(m.ReaderId)));
        }
        return q.OrderByDescending(x => x.StartAt);
    }

    public override async Task<PagedResult<RoomBooking>> SearchAsync(RoomBookingSearchRequest r)
    {
        var paged = await base.SearchAsync(r);
        await FillNamesAsync(paged.Items);
        return paged;
    }

    public override async Task<List<RoomBooking>> SearchAllAsync(RoomBookingSearchRequest r)
    {
        var list = await base.SearchAllAsync(r);
        await FillNamesAsync(list);
        return list;
    }

    /// <summary>Dòng xuất Excel theo đúng bộ lọc màn danh sách, tối đa <paramref name="max"/> dòng (kèm tên phòng/bạn đọc/thành viên).</summary>
    public async Task<(List<RoomBooking> Rows, int Total)> ExportRowsAsync(RoomBookingSearchRequest r, int max)
    {
        var q = ApplyTenantFilter(BuildQuery(r), await ResolveRequestTenantIdAsync(r.TenantId));
        var total = await q.CountAsync();
        var rows = await q.Take(max).ToListAsync();
        await FillNamesAsync(rows);
        await FillTenantNamesAsync(rows);
        return (rows, total);
    }

    private async Task FillNamesAsync(List<RoomBooking> list)
    {
        var objectIds = list.Select(x => x.MapObjectId).Distinct().ToList();
        var readerIds = list.Select(x => x.ReaderId).Distinct().ToList();
        var roomMap   = await _context.MapObjects.Where(o => objectIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name);
        var readerMap = await _context.Readers.Where(r => readerIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => $"{r.FirstName} {r.LastName}".Trim());
        var cardMap   = await _context.Readers.Where(r => readerIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Cardno);
        var bookingIds = list.Select(x => x.Id).ToList();
        var members = await (from m in _context.RoomBookingMembers
                             where bookingIds.Contains(m.BookingId)
                             join rd in _context.Readers on m.ReaderId equals rd.Id
                             orderby m.Id
                             select new { m.BookingId, rd.FirstName, rd.LastName, rd.Cardno }).ToListAsync();
        foreach (var x in list)
        {
            x.Members      = members.Where(m => m.BookingId == x.Id)
                .Select(m => $"{RoomBookingPrivacy.FullName(m.FirstName, m.LastName)} ({m.Cardno})").ToList();
            x.RoomName     = roomMap.TryGetValue(x.MapObjectId, out var rn) ? rn : null;
            x.ReaderName   = readerMap.TryGetValue(x.ReaderId, out var nm) ? nm : null;
            x.ReaderCardNo = cardMap.TryGetValue(x.ReaderId, out var cn) ? cn : null;
        }
    }

    protected override void MapRequestToEntity(RoomBookingRequest r, RoomBooking e, long userId, bool isNew) { }

    protected override void SoftDelete(RoomBooking e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.UtcNow; }

    protected override void SetStatus(RoomBooking e, int status, long userId) { }

    // ── Nghiệp vụ đặt/duyệt/check-in/hủy phòng ────────────────────────────────

    public async Task<(bool Ok, string? Error, int StatusCode, RoomBooking? Booking)> CreateBookingAsync(CreateRoomBookingRequest request, long readerId, long? tenantId)
    {
        // Predicate/range protection also covers the first booking in an otherwise empty room.
        // A process-local lock would not protect requests handled by another API instance.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        RoomBooking? created = null;
        try
        {
            var result = await CreateBookingInTransactionAsync(request, readerId, tenantId);
            created = result.Booking;
            if (result.Ok)
            {
                await transaction.CommitAsync();
                // Phòng tự động duyệt: báo "đã duyệt" ngay như khi thủ thư duyệt tay; phòng chờ duyệt: báo "đã nhận yêu cầu".
                // Gửi SAU commit để không giữ transaction Serializable trong lúc chờ email/SMS (notifier tự nuốt lỗi).
                await _notifier.NotifyAsync(result.Booking!, result.Booking!.Status == 2
                    ? RoomBookingNotifier.Event.Approved : RoomBookingNotifier.Event.Created);
            }
            return result;
        }
        catch (Exception ex) when (IsBookingConflict(ex))
        {
            // Disposal rolls back an active transaction. A failed COMMIT may already have ended it.
            foreach (var entry in _context.ChangeTracker.Entries<RoomBooking>()
                         .Where(e => e.State == EntityState.Added || e.Entity == created).ToList())
                entry.State = EntityState.Detached;
            return (false, "Lịch phòng vừa thay đổi. Vui lòng tải lại lịch và chọn khung giờ còn trống.", 409, null);
        }
    }

    private static bool IsBookingConflict(Exception ex) => ex switch
    {
        PostgresException pg => pg.SqlState is "40001" or "40P01" or "23P01",
        SqlException sql => sql.Number is 1205 or 3960,
        DbUpdateException { InnerException: { } inner } => IsBookingConflict(inner),
        _ => false
    };

    private async Task<(bool Ok, string? Error, int StatusCode, RoomBooking? Booking)> CreateBookingInTransactionAsync(CreateRoomBookingRequest request, long readerId, long? tenantId)
    {
        // StartAt/EndAt lưu và so sánh theo UTC (OPAC gửi ISO có "Z"). Không dùng DateTime.Now: máy chủ chạy
        // múi giờ khác UTC (vd. máy dev Windows UTC+7) sẽ lệch 7 giờ và chặn nhầm "thời điểm đã qua".
        request.StartAt = RoomBookingHours.ToUtc(request.StartAt);
        request.EndAt   = RoomBookingHours.ToUtc(request.EndAt);
        var now = DateTime.UtcNow;
        if (request.EndAt <= request.StartAt)
            return (false, "Giờ kết thúc phải sau giờ bắt đầu.", 400, null);

        // Giờ mở cửa theo ngày + loại cơ sở (ngày đặc biệt / giờ theo thứ / giờ chung).
        var category = await _context.MapObjects.Where(o => o.Id == request.MapObjectId).Select(o => o.Category).FirstOrDefaultAsync();
        var libraryDay = RoomBookingHours.ToLibraryLocal(request.StartAt).Date;
        var hours = await RoomBookingHours.ResolveAsync(_context, tenantId, libraryDay, category);
        if (hours.Closed)
            return (false, $"Thư viện không mở cửa đặt phòng ngày {libraryDay:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(hours.Reason) ? "" : $" ({hours.Reason})")}.", 400, null);
        var hoursError = RoomBookingHours.Validate(request.StartAt, request.EndAt, hours.Open, hours.Close);
        if (hoursError != null) return (false, hoursError, 400, null);
        if (request.StartAt < now)
            return (false, "Không thể đặt phòng cho thời điểm đã qua.", 400, null);

        var config = await _context.RoomBookingConfigs
            .FirstOrDefaultAsync(c => c.MapObjectId == request.MapObjectId && c.TenantId == tenantId && c.IsDelete != 2 && c.Status == 2);
        if (config == null) return (false, "Phòng này chưa được cấu hình để đặt chỗ.", 400, null);

        if (config.Maintenance)
            return (false, "Phòng đang tạm ngưng phục vụ" + (string.IsNullOrWhiteSpace(config.MaintenanceNote) ? "." : $": {config.MaintenanceNote}."), 400, null);

        var ban = await _bans.ActiveBanAsync(readerId);
        if (ban != null)
            return (false, $"Bạn đang bị tạm khoá đặt phòng đến {RoomBookingHours.ToLibraryLocal(ban.BannedUntil):dd/MM/yyyy HH:mm}"
                + (string.IsNullOrWhiteSpace(ban.Reason) ? "." : $" (lý do: {ban.Reason})."), 403, null);

        var allowedTypes = RoomBookingRules.ParseIds(config.AllowedReaderTypeIds);
        if (allowedTypes.Count > 0)
        {
            var readerType = await _context.Readers.Where(r => r.Id == readerId).Select(r => r.ReaderTypeId).FirstOrDefaultAsync();
            if (readerType == null || !allowedTypes.Contains(readerType.Value))
            {
                var names = await _context.ReaderTypes.Where(t => allowedTypes.Contains(t.Id)).Select(t => t.Name).ToListAsync();
                return (false, $"Phòng này chỉ dành cho: {string.Join(", ", names)}.", 403, null);
            }
        }

        // Đặt theo nhóm: thẻ thành viên (số thẻ hoặc UID chip) → bạn đọc; có thành viên thì số người = thành viên + người đặt.
        var (memberIds, memberError, memberStatus) = await ResolveMembersAsync(request.MemberCards, readerId, tenantId);
        if (memberError != null) return (false, memberError, memberStatus, null);
        if (memberIds.Count > 0) request.PartySize = memberIds.Count + 1;
        var minGroup = Math.Max(1, config.MinGroupSize ?? 1);
        if (request.PartySize < minGroup || (minGroup > 1 && memberIds.Count + 1 < minGroup))
            return (false, $"Phòng này cần tối thiểu {minGroup} người: vui lòng nhập mã thẻ của {minGroup - 1} thành viên trở lên.", 400, null);
        if (request.PartySize <= 0 || request.PartySize > config.Capacity)
            return (false, $"Số người tham gia phải từ {minGroup} đến {config.Capacity}.", 400, null);

        var minStart = now.AddMinutes(config.MinAdvanceMinutes);
        if (request.StartAt < minStart)
            return (false, $"Cần đặt trước ít nhất {config.MinAdvanceMinutes} phút.", 400, null);

        if (config.MaxAdvanceHours is > 0)
        {
            if (request.StartAt > now.AddHours(config.MaxAdvanceHours.Value))
                return (false, $"Chỉ có thể đặt trước tối đa {config.MaxAdvanceHours} giờ.", 400, null);
        }
        else if (request.StartAt > now.AddDays(config.MaxAdvanceDays))
            return (false, $"Chỉ có thể đặt trước tối đa {config.MaxAdvanceDays} ngày.", 400, null);

        var rules = await RoomBookingRules.GetAsync(_context, tenantId);
        if (rules.MaxSessionMinutes > 0 && (request.EndAt - request.StartAt).TotalMinutes > rules.MaxSessionMinutes)
            return (false, $"Mỗi lượt đặt dài tối đa {rules.MaxSessionMinutes} phút.", 400, null);

        var overlap = await _dbSet.AnyAsync(x =>
            x.MapObjectId == request.MapObjectId && x.TenantId == tenantId && x.IsDelete != 2
            && (x.Status == 1 || x.Status == 2 || x.Status == 3)
            && x.StartAt < request.EndAt && x.EndAt > request.StartAt);
        if (overlap) return (false, "Khoảng thời gian này phòng đã có người đặt.", 409, null);

        // Một bạn đọc (người đặt hoặc thành viên) không giữ 2 phòng cùng lúc — yêu cầu kỹ thuật: đặt nhiều phòng phải khác thời điểm.
        var participants = memberIds.Append(readerId).ToList();
        var clash = await ParticipantsQuery(participants)
            .Where(x => x.TenantId == tenantId && x.IsDelete != 2 && (x.Status == 1 || x.Status == 2 || x.Status == 3) && x.StartAt < request.EndAt && x.EndAt > request.StartAt)
            .Select(x => new { x.Id, x.MapObjectId, x.StartAt, x.EndAt, x.ReaderId }).FirstOrDefaultAsync();
        if (clash != null)
        {
            var otherRoom = await _context.MapObjects.Where(o => o.Id == clash.MapObjectId).Select(o => o.Name).FirstOrDefaultAsync();
            var mine = clash.ReaderId == readerId || await _context.RoomBookingMembers.AnyAsync(m => m.BookingId == clash.Id && m.ReaderId == readerId);
            var who = mine ? "Bạn" : "Một thành viên trong nhóm";
            return (false, $"{who} đã có lượt đặt phòng \"{otherRoom}\" từ {RoomBookingHours.ToLibraryLocal(clash.StartAt):HH:mm} đến "
                + $"{RoomBookingHours.ToLibraryLocal(clash.EndAt):HH:mm} trùng khung giờ này.", 409, null);
        }

        var (dayStart, dayEnd) = RoomBookingHours.LibraryDayUtcRange(libraryDay);
        if (config.MaxBookingMinutesPerReader > 0)
        {
            var usedMinutes = await _dbSet
                .Where(x => x.MapObjectId == request.MapObjectId && x.ReaderId == readerId && x.TenantId == tenantId && x.IsDelete != 2 && (x.Status == 1 || x.Status == 2 || x.Status == 3)
                            && x.StartAt >= dayStart && x.StartAt < dayEnd)
                .ToListAsync();
            var totalMinutes = usedMinutes.Sum(x => (x.EndAt - x.StartAt).TotalMinutes) + (request.EndAt - request.StartAt).TotalMinutes;
            if (totalMinutes > config.MaxBookingMinutesPerReader)
                return (false, $"Bạn chỉ được đặt tối đa {config.MaxBookingMinutesPerReader} phút/ngày cho phòng này.", 400, null);
        }

        if (rules.MaxPerDay > 0)
        {
            var today = await _dbSet.CountAsync(x => x.ReaderId == readerId && x.TenantId == tenantId && x.IsDelete != 2
                && (x.Status == 1 || x.Status == 2 || x.Status == 3 || x.Status == 4) && x.StartAt >= dayStart && x.StartAt < dayEnd);
            if (today >= rules.MaxPerDay)
                return (false, $"Mỗi bạn đọc chỉ được đặt tối đa {rules.MaxPerDay} lượt/ngày.", 400, null);
        }

        if (rules.MaxActive > 0)
        {
            var upcoming = await _dbSet.CountAsync(x => x.ReaderId == readerId && x.TenantId == tenantId && x.IsDelete != 2 && (x.Status == 1 || x.Status == 2) && x.EndAt > now);
            if (upcoming >= rules.MaxActive)
                return (false, $"Bạn đang có {upcoming} lượt đặt chưa sử dụng, tối đa {rules.MaxActive} lượt.", 400, null);
        }

        var booking = new RoomBooking
        {
            MapObjectId    = request.MapObjectId,
            ReaderId       = readerId,
            StartAt        = request.StartAt,
            EndAt          = request.EndAt,
            PartySize      = request.PartySize,
            Status         = config.AutoApprove ? 2 : 1,
            ApprovedAt     = config.AutoApprove ? now : null,
            Note           = request.Note,
            CreatedRowDate = now,
            UpdatedRowDate = now,
            TenantId       = tenantId,
            PublicId       = Guid.NewGuid()
        };
        _dbSet.Add(booking);
        await _context.SaveChangesAsync();
        if (memberIds.Count > 0)
        {
            _context.RoomBookingMembers.AddRange(memberIds.Select(id => new RoomBookingMember { BookingId = booking.Id, ReaderId = id, CreatedRowDate = now, TenantId = tenantId }));
            await _context.SaveChangesAsync();
        }
        return (true, null, 200, booking);
    }

    /// <summary>Lượt đặt mà bạn đọc là người đặt hoặc thành viên nhóm.</summary>
    private IQueryable<RoomBooking> ParticipantsQuery(IReadOnlyCollection<long> readerIds) =>
        _dbSet.Where(x => readerIds.Contains(x.ReaderId) || _context.RoomBookingMembers.Any(m => m.BookingId == x.Id && readerIds.Contains(m.ReaderId)));

    private IQueryable<RoomBooking> OfParticipant(long readerId) => ParticipantsQuery([readerId]);

    /// <summary>Thẻ thành viên → Id bạn đọc: khớp số thẻ (không phân biệt hoa thường) hoặc UID chip. Bỏ thẻ của chính người đặt,
    /// báo lỗi thẻ không tồn tại, trùng, hết hạn hoặc đang bị khoá đặt phòng. Tenant: chỉ bạn đọc cùng đơn vị với người đặt.</summary>
    private async Task<(List<long> Ids, string? Error, int Status)> ResolveMembersAsync(List<string>? cards, long readerId, long? tenantId)
    {
        var raw = (cards ?? []).Select(c => c?.Trim()).Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).ToList();
        if (raw.Count == 0) return ([], null, 200);
        if (raw.Count > 50) return ([], "Tối đa 50 thành viên cho một lượt đặt.", 400);
        var lower = raw.Select(c => c.ToLower()).Distinct().ToList();
        var uids = raw.Select(CardUid.Normalize).Where(u => u != null).Distinct().ToList();
        var found = await _context.Readers.AsNoTracking()
            .Where(r => r.IsDelete != 2 && r.TenantId == tenantId && ((r.Cardno != null && lower.Contains(r.Cardno.ToLower())) || (r.CardUid != null && uids.Contains(r.CardUid))))
            .Select(r => new { r.Id, r.Cardno, r.CardUid, r.ExpireDate, r.FirstName, r.LastName }).ToListAsync();
        var ids = new List<long>();
        var today = LibraryClock.Now.Date;
        foreach (var card in raw)
        {
            var uid = CardUid.Normalize(card);
            var matches = found.Where(r => string.Equals(r.Cardno, card, StringComparison.OrdinalIgnoreCase) || (uid != null && r.CardUid == uid)).ToList();
            if (matches.Count == 0) return ([], $"Không tìm thấy bạn đọc có thẻ \"{card}\".", 400);
            if (matches.Count > 1) return ([], $"Thẻ \"{card}\" khớp nhiều bạn đọc, vui lòng nhập số thẻ.", 400);
            var m = matches[0];
            if (m.Id == readerId) continue;
            if (ids.Contains(m.Id)) return ([], $"Thành viên có thẻ \"{card}\" bị nhập trùng.", 400);
            if (m.ExpireDate != null && m.ExpireDate.Value.Date < today) return ([], $"Thẻ \"{card}\" đã hết hạn.", 400);
            var ban = await _bans.ActiveBanAsync(m.Id);
            if (ban != null) return ([], $"Thành viên {RoomBookingPrivacy.FullName(m.FirstName, m.LastName)} đang bị tạm khoá đặt phòng.", 403);
            ids.Add(m.Id);
        }
        return (ids, null, 200);
    }

    public async Task<(bool Ok, string? Error)> ApproveAsync(Guid publicId, long staffUserId)
    {
        var booking = await ApplyTenantFilter(_dbSet.AsNoTracking().Where(x => x.PublicId == publicId && x.IsDelete != 2)).FirstOrDefaultAsync();
        if (booking == null) return (false, "Không tìm thấy yêu cầu đặt phòng.");
        if (booking.Status != 1) return (false, "Yêu cầu này không còn ở trạng thái chờ duyệt.");

        var now = DateTime.UtcNow;
        var changed = await _dbSet.Where(x => x.Id == booking.Id && x.Status == 1 && x.IsDelete != 2)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, 2)
                .SetProperty(x => x.ApprovedBy, staffUserId).SetProperty(x => x.ApprovedAt, now)
                .SetProperty(x => x.UpdateRowBy, staffUserId).SetProperty(x => x.UpdatedRowDate, now));
        if (changed == 0) return (false, "Yêu cầu vừa thay đổi, vui lòng tải lại.");
        booking.Status = 2;
        booking.ApprovedBy = staffUserId;
        booking.ApprovedAt = now;
        booking.UpdateRowBy = staffUserId;
        booking.UpdatedRowDate = now;
        await _notifier.NotifyAsync(booking, RoomBookingNotifier.Event.Approved);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RejectAsync(Guid publicId, long staffUserId, string? reason)
    {
        var booking = await ApplyTenantFilter(_dbSet.Where(x => x.PublicId == publicId && x.IsDelete != 2)).FirstOrDefaultAsync();
        if (booking == null) return (false, "Không tìm thấy yêu cầu đặt phòng.");
        if (booking.Status != 1 && booking.Status != 2) return (false, "Yêu cầu này không thể từ chối ở trạng thái hiện tại.");

        var now = DateTime.UtcNow;
        booking.Status = 6;
        booking.Note = string.IsNullOrWhiteSpace(reason) ? booking.Note : reason;
        booking.UpdateRowBy = staffUserId;
        booking.UpdatedRowDate = now;
        await _context.SaveChangesAsync();

        await _notifier.NotifyAsync(booking, RoomBookingNotifier.Event.Rejected, string.IsNullOrWhiteSpace(reason) ? null : $"Lý do: {reason}");
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CheckInAsync(Guid publicId, long readerId, long? tenantId)
    {
        var booking = await OfParticipant(readerId).FirstOrDefaultAsync(x => x.PublicId == publicId && x.TenantId == tenantId && x.IsDelete != 2);
        if (booking == null) return (false, "Không tìm thấy lượt đặt phòng của bạn.");
        var error = await CheckInCoreAsync(booking, null);
        return (error == null, error);
    }

    /// <summary>Thủ thư/kiosk quét mã QR bạn đọc xuất trình. mapObjectId (chế độ kiosk tại 1 phòng) khác
    /// phòng của lượt đặt thì từ chối. Booking trả kèm tên phòng/bạn đọc để màn quét hiện kết quả.</summary>
    public async Task<(bool Ok, string? Error, RoomBooking? Booking)> StaffCheckInAsync(Guid publicId, long? mapObjectId, long staffUserId)
    {
        var booking = await ApplyTenantFilter(_dbSet.AsNoTracking().Where(x => x.PublicId == publicId && x.IsDelete != 2)).FirstOrDefaultAsync();
        if (booking == null) return (false, "Mã QR không hợp lệ hoặc lượt đặt phòng không tồn tại.", null);
        return await StaffCheckInCoreAsync(booking, mapObjectId, staffUserId);
    }

    private const int CheckInOpenMinutes = RoomBookingHours.CheckInOpenMinutes;

    // ── Check-in bằng khuôn mặt (port ELIB-LRC 09-30) ─────────────────────────────────────────────

    /// <summary>Lượt đã duyệt đang trong khung check-in (từ 15 phút trước giờ bắt đầu tới hết ân hạn của phòng), sớm nhất trước.
    /// Ân hạn đọc theo cấu hình phòng của đúng đơn vị sở hữu lượt.</summary>
    private async Task<List<RoomBooking>> CheckInWindowAsync(IQueryable<RoomBooking> query, DateTime now)
    {
        var openBefore = now.AddMinutes(CheckInOpenMinutes);
        var rows = await query.Where(x => x.IsDelete != 2 && x.Status == 2 && x.StartAt <= openBefore && x.EndAt >= now)
            .OrderBy(x => x.StartAt).ToListAsync();
        var result = new List<RoomBooking>();
        foreach (var byTenant in rows.GroupBy(x => x.TenantId))
        {
            var grace = await GraceByRoomAsync(byTenant.Select(x => x.MapObjectId), byTenant.Key);
            result.AddRange(byTenant.Where(x => now <= x.StartAt.AddMinutes(Grace(grace, x.MapObjectId))));
        }
        return result.OrderBy(x => x.StartAt).ToList();
    }

    public async Task<List<long>> FaceCheckInCandidatesAsync(long? mapObjectId)
    {
        var q = ApplyTenantFilter(_dbSet.AsNoTracking());
        if (mapObjectId.HasValue) q = q.Where(x => x.MapObjectId == mapObjectId.Value);
        var rows = await CheckInWindowAsync(q, DateTime.UtcNow);
        var ids = rows.Select(x => x.Id).ToList();
        var members = await _context.RoomBookingMembers.AsNoTracking().Where(m => ids.Contains(m.BookingId)).Select(m => m.ReaderId).ToListAsync();
        return rows.Select(x => x.ReaderId).Concat(members).Distinct().ToList();
    }

    public async Task<(bool Ok, string? Error, RoomBooking? Booking)> StaffCheckInByReaderAsync(long readerId, long? mapObjectId, long staffUserId)
    {
        var active = await CheckInWindowAsync(ApplyTenantFilter(OfParticipant(readerId).AsNoTracking()), DateTime.UtcNow);
        // Ưu tiên lượt của đúng phòng kiosk; không có thì lấy lượt khác để báo rõ "thuộc phòng ..." thay vì "không có lượt".
        var booking = (mapObjectId.HasValue ? active.FirstOrDefault(x => x.MapObjectId == mapObjectId.Value) : null) ?? active.FirstOrDefault();
        if (booking == null) return (false, "Bạn đọc không có lượt đặt phòng nào đang tới giờ check-in.", null);
        return await StaffCheckInCoreAsync(booking, mapObjectId, staffUserId);
    }

    public async Task<string?> CheckInPrecheckAsync(Guid publicId, long readerId, long? tenantId)
    {
        var booking = await OfParticipant(readerId).AsNoTracking()
            .FirstOrDefaultAsync(x => x.PublicId == publicId && x.TenantId == tenantId && x.IsDelete != 2);
        return booking == null ? "Không tìm thấy lượt đặt phòng của bạn." : await CheckInStateErrorAsync(booking, DateTime.UtcNow);
    }

    /// <summary>Thời gian ân hạn check-in sau giờ bắt đầu theo cấu hình phòng — cùng mốc RoomBookingExpiryJob dùng để chuyển
    /// NoShow. Trước đây check-in mở tới hết giờ đặt, nên đến muộn quá ân hạn lúc được lúc không tuỳ job đã chạy hay chưa.</summary>
    private async Task<Dictionary<long, int>> GraceByRoomAsync(IEnumerable<long> mapObjectIds, long? tenantId)
    {
        var ids = mapObjectIds.Distinct().ToList();
        return await _context.RoomBookingConfigs.AsNoTracking()
            .Where(c => ids.Contains(c.MapObjectId) && c.TenantId == tenantId && c.IsDelete != 2)
            .GroupBy(c => c.MapObjectId)
            .Select(g => new { g.Key, Grace = g.Max(c => c.CheckInGraceMinutes) })
            .ToDictionaryAsync(x => x.Key, x => x.Grace);
    }

    private static int Grace(Dictionary<long, int> graceByRoom, long mapObjectId) =>
        graceByRoom.TryGetValue(mapObjectId, out var g) ? g : RoomBookingHours.DefaultCheckInGraceMinutes;

    private async Task<(bool Ok, string? Error, RoomBooking? Booking)> StaffCheckInCoreAsync(RoomBooking booking, long? mapObjectId, long staffUserId)
    {
        await FillNamesAsync([booking]);
        if (mapObjectId.HasValue && booking.MapObjectId != mapObjectId.Value)
            return (false, $"Lượt đặt này thuộc phòng \"{booking.RoomName}\", không phải phòng đang quét.", booking);

        var error = await CheckInCoreAsync(booking, staffUserId);
        if (error != null) return (false, error, booking);
        booking.Status = 3;
        booking.CheckedInAt = DateTime.UtcNow;
        return (true, null, booking);
    }

    private async Task<string?> CheckInStateErrorAsync(RoomBooking booking, DateTime now)
    {
        if (booking.Status == 3) return "Lượt đặt phòng này đã check-in rồi.";
        if (booking.Status == 1) return "Lượt đặt phòng chưa được duyệt, chưa thể check-in.";
        if (booking.Status != 2) return "Chỉ có thể check-in cho đặt phòng đã được duyệt.";
        if (now < booking.StartAt.AddMinutes(-CheckInOpenMinutes))
            return $"Chưa tới giờ check-in (mở trước {CheckInOpenMinutes} phút).";
        if (now > booking.EndAt)
            return "Đã quá giờ sử dụng phòng.";
        var grace = Grace(await GraceByRoomAsync([booking.MapObjectId], booking.TenantId), booking.MapObjectId);
        if (now > booking.StartAt.AddMinutes(grace))
            return $"Đã quá thời gian check-in ({grace} phút sau giờ bắt đầu), lượt đặt được tính là vắng mặt.";
        return null;
    }

    private async Task<string?> CheckInCoreAsync(RoomBooking booking, long? staffUserId)
    {
        var now = DateTime.UtcNow;
        var stateError = await CheckInStateErrorAsync(booking, now);
        if (stateError != null) return stateError;

        var changed = await _dbSet.Where(x => x.Id == booking.Id && x.Status == 2 && x.IsDelete != 2)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, 3)
                .SetProperty(x => x.CheckedInAt, now).SetProperty(x => x.UpdatedRowDate, now)
                .SetProperty(x => x.UpdateRowBy, x => staffUserId ?? x.UpdateRowBy));
        return changed == 0 ? "Lượt đặt phòng vừa thay đổi, vui lòng tải lại." : null;
    }

    /// <summary>Chỉ huỷ được trước giờ bắt đầu: lượt đã duyệt mà quá giờ không check-in là vắng mặt (RoomBookingExpiryJob chuyển
    /// NoShow theo giờ) — trước đây bạn đọc huỷ được trong lúc chờ job chạy nên vắng mặt không bị tính.</summary>
    public async Task<string?> CancelAsync(Guid publicId, long readerId, long? tenantId)
    {
        var booking = await _dbSet.FirstOrDefaultAsync(x => x.PublicId == publicId && x.ReaderId == readerId && x.TenantId == tenantId && x.IsDelete != 2);
        if (booking == null) return "Không tìm thấy lượt đặt phòng của bạn.";
        if (booking.Status != 1 && booking.Status != 2) return "Lượt đặt phòng này không còn huỷ được.";
        var now = DateTime.UtcNow;
        if (booking.StartAt <= now) return "Đã tới giờ sử dụng phòng, không thể huỷ.";

        booking.Status = 5;
        booking.CancelledAt = now;
        booking.UpdateRowBy = readerId;
        booking.UpdatedRowDate = now;
        await _context.SaveChangesAsync();
        await _notifier.NotifyAsync(booking, RoomBookingNotifier.Event.Cancelled);
        return null;
    }

    /// <summary>Trả phòng (kết thúc sớm) lượt đang sử dụng: Status 3 → 4, ghi CheckedOutAt. Phòng giải phóng ngay vì lịch bận
    /// chỉ tính Status 1/2/3. readerId != null: người đặt hoặc thành viên (tenantId = đơn vị bạn đọc); staffUserId != null: thủ thư trả
    /// hộ (lọc theo đơn vị JWT).</summary>
    public async Task<(bool Ok, string? Error, RoomBooking? Booking)> CheckOutAsync(Guid publicId, long? readerId, long? staffUserId, long? tenantId = null)
    {
        var scoped = readerId == null ? ApplyTenantFilter(_dbSet) : OfParticipant(readerId.Value).Where(x => x.TenantId == tenantId);
        var booking = await scoped.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PublicId == publicId && x.IsDelete != 2);
        if (booking == null) return (false, readerId == null ? "Không tìm thấy lượt đặt phòng." : "Không tìm thấy lượt đặt phòng của bạn.", null);
        if (booking.Status != 3) return (false, "Chỉ trả phòng được khi lượt đặt đang sử dụng (đã check-in).", booking);

        var now = DateTime.UtcNow;
        var changed = await _dbSet.Where(x => x.Id == booking.Id && x.Status == 3 && x.IsDelete != 2)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, 4).SetProperty(x => x.CheckedOutAt, now)
                .SetProperty(x => x.UpdatedRowDate, now).SetProperty(x => x.UpdateRowBy, x => staffUserId ?? readerId ?? x.UpdateRowBy));
        if (changed == 0) return (false, "Lượt đặt phòng vừa thay đổi, vui lòng tải lại.", booking);
        booking.Status = 4;
        booking.CheckedOutAt = now;
        await FillNamesAsync([booking]);
        return (true, null, booking);
    }
}
