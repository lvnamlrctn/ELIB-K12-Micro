using System.Globalization;
using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed class BookingSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public long? SpaceId { get; set; }
    public int? BookingStatus { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record MemberDto(Guid ReaderPublicId, string CardNo, string? Name);

public sealed record BookingDto(
    long Id, Guid PublicId, Guid ReaderPublicId, string CardNo, string? ReaderName, long SpaceId, string? SpaceName, string? FloorName,
    string? BuildingName, DateTimeOffset StartAt, DateTimeOffset EndAt, int PartySize, int Status, long? ApprovedBy, DateTimeOffset? ApprovedAt,
    string? RejectReason, DateTimeOffset? CancelledAt, DateTimeOffset? CheckedInAt, DateTimeOffset? CheckedOutAt, string? Note,
    DateTimeOffset CreatedAt, IReadOnlyList<MemberDto> Members)
{
    /// <summary>Bạn đọc tự xem (OPAC) — không lộ id cán bộ thao tác.</summary>
    public BookingDto ForReader() => this with { ApprovedBy = null };
}

public sealed record MyBookingDto(BookingDto Booking, bool IsOwner);

public sealed record ReaderBookRequest(long SpaceId, DateTimeOffset StartAt, DateTimeOffset EndAt, int? PartySize, string? Note,
    IReadOnlyList<string>? MemberCards);

public sealed record RejectBookingRequest(Guid PublicId, string Reason);

public sealed record CheckInRequest(Guid PublicId, long? KioskSpaceId);

public sealed record CheckInByCardRequest(string CardNo, long? KioskSpaceId);

public sealed record BookingOpUnused;

public sealed record BusySlot(DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record SpaceAvailability(bool Closed, TimeOnly? OpenTime, TimeOnly? CloseTime, string? ClosedReason, bool Maintenance,
    string? MaintenanceNote, IReadOnlyList<BusySlot> Busy);

/// <summary>
/// Lượt đặt phòng/không gian (monolith: map.RoomBooking, quyền SPACE_BOOKINGS). Tạo/duyệt/từ chối/check-in/check-out/huỷ đều qua
/// phương thức nghiệp vụ riêng — không có Add/Update chung, đúng khuôn LoanResource/DocumentSubmissionResource. Chưa gồm kiểm
/// tra cấm đặt (lát sau, cùng lúc thêm entity Ban) và thông báo (lát sau, cùng Notification).
/// </summary>
public sealed class BookingResource(ICrudDbContext db, Replicas replicas, HoursResource hours, SettingsResource settings, BanResource bans,
    SpaceReaderNotifier notifier, ICurrentActor actor, TimeProvider clock) : CrudResource<BookingResource, RoomBooking, BookingSearch, BookingOpUnused, BookingDto>(db)
{
    /// <summary>Mở cửa sổ check-in trước giờ bắt đầu (cố định, không cấu hình — đúng bản gốc).</summary>
    public const int CheckInOpenMinutes = 15;

    private static readonly int[] OpenStatuses = [RoomBookingStatus.Pending, RoomBookingStatus.Approved, RoomBookingStatus.CheckedIn];

    protected override string EntityName => "Lượt đặt phòng";

    protected override string? Describe(RoomBooking entity) => $"Thẻ {entity.CardNo}";

    protected override Expression<Func<RoomBooking, BookingDto>> Projection => x => new BookingDto(
        x.Id, x.PublicId, x.ReaderPublicId, x.CardNo,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.SpaceId, Db.Set<BookableSpace>().Where(s => s.Id == x.SpaceId).Select(s => s.Name).FirstOrDefault(),
        Db.Set<MapFloor>().Where(f => f.Id == Db.Set<BookableSpace>().Where(s => s.Id == x.SpaceId).Select(s => s.FloorId).FirstOrDefault())
            .Select(f => f.Name).FirstOrDefault(),
        Db.Set<MapBuilding>().Where(b => b.Id == Db.Set<MapFloor>().Where(f => f.Id ==
            Db.Set<BookableSpace>().Where(s => s.Id == x.SpaceId).Select(s => s.FloorId).FirstOrDefault()).Select(f => f.BuildingId).FirstOrDefault())
            .Select(b => b.Name).FirstOrDefault(),
        x.StartAt, x.EndAt, x.PartySize, x.Status, x.ApprovedBy, x.ApprovedAt, x.RejectReason, x.CancelledAt, x.CheckedInAt, x.CheckedOutAt, x.Note,
        x.CreatedAt, Array.Empty<MemberDto>());

    protected override RoomBooking Create(BookingOpUnused request) => throw new NotSupportedException("Lượt đặt phòng tạo qua BookAsync.");

    protected override void Update(RoomBooking entity, BookingOpUnused request) => throw new NotSupportedException("Lượt đặt phòng sửa qua Approve/Reject/CheckIn/CheckOut/Cancel.");

    protected override IQueryable<RoomBooking> Filter(IQueryable<RoomBooking> query, BookingSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.CardNo))
        {
            var card = PatronReplica.Key(search.CardNo);
            query = query.Where(x => x.CardNo == card);
        }
        if (search.SpaceId is { } spaceId) query = query.Where(x => x.SpaceId == spaceId);
        if (search.BookingStatus is { } status) query = query.Where(x => x.Status == status);
        if (search.From is { } from) query = query.Where(x => x.StartAt >= Local(from));
        if (search.To is { } to) query = query.Where(x => x.StartAt < Local(to.AddDays(1)));
        return query;
    }

    protected override IOrderedQueryable<RoomBooking> Order(IQueryable<RoomBooking> query) => query.OrderByDescending(x => x.StartAt).ThenByDescending(x => x.Id);

    /// <summary>Đặt phòng (bạn đọc, OPAC) — chuỗi kiểm tra đúng thứ tự bản gốc, trừ cấm đặt (lát sau) và thông báo (lát sau).</summary>
    public async Task<BookingDto> BookAsync(Guid readerPublicId, ReaderBookRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Frontend (lưới giờ địa phương) có thể gửi offset +07:00 — ElibDbContext chỉ nhận UTC khi lưu, chuẩn hoá ngay từ đầu.
        request = request with { StartAt = request.StartAt.ToUniversalTime(), EndAt = request.EndAt.ToUniversalTime() };
        var reader = await replicas.ByReaderPublicIdAsync(readerPublicId, ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", "Không tìm thấy thông tin bạn đọc.", 404);
        if (request.EndAt <= request.StartAt) throw new BusinessRuleException("TIME_RANGE_INVALID", "Giờ kết thúc phải sau giờ bắt đầu.");
        var now = clock.GetUtcNow();
        if (request.StartAt < now) throw new BusinessRuleException("TIME_IN_PAST", "Không thể đặt phòng cho thời điểm đã qua.");

        var space = await Db.Set<BookableSpace>().FirstOrDefaultAsync(s => s.Id == request.SpaceId && s.Status == IHasStatus.Active, ct)
            ?? throw new NotFoundException("Không gian", request.SpaceId);
        if (space.Maintenance)
            throw new BusinessRuleException("SPACE_MAINTENANCE", $"Không gian đang tạm ngưng để bảo trì." + (space.MaintenanceNote is { } n ? $" {n}" : ""));
        if (space.AllowedReaderTypeIds.Count > 0 && (reader.ReaderTypeId is null || !space.AllowedReaderTypeIds.Contains(reader.ReaderTypeId.Value)))
            throw new BusinessRuleException("READER_TYPE_NOT_ALLOWED", "Loại bạn đọc của bạn không được đặt không gian này.", 403);
        if (await bans.IsBannedAsync(reader.ReaderPublicId, ct))
            throw new BusinessRuleException("READER_BANNED", "Bạn đang bị cấm đặt phòng.", 403);

        var localDate = RoomBooking.LocalDate(request.StartAt);
        var resolved = await hours.ResolveAsync(localDate, space.Category, ct);
        if (resolved.Closed) throw new BusinessRuleException("SPACE_CLOSED", "Không gian không mở cửa vào thời điểm này." + (resolved.Reason is { } r ? $" ({r})" : ""));
        var open = resolved.OpenTime!.Value;
        var close = resolved.CloseTime!.Value;
        var startLocal = TimeOnly.FromDateTime(request.StartAt.UtcDateTime.AddHours(7));
        var endLocal = TimeOnly.FromDateTime(request.EndAt.UtcDateTime.AddHours(7));
        if (RoomBooking.LocalDate(request.EndAt) != localDate || startLocal < open || endLocal > close || startLocal > endLocal)
            throw new BusinessRuleException("OUTSIDE_OPENING_HOURS", $"Không gian mở cửa {open:HH\\:mm}-{close:HH\\:mm} ngày {localDate:dd/MM/yyyy}.");

        var members = await ResolveMembersAsync(request.MemberCards, reader.ReaderPublicId, ct);
        var minGroup = Math.Max(1, space.MinGroupSize ?? 1);
        var partySize = members.Count > 0 ? members.Count + 1 : request.PartySize ?? 1;
        if (partySize < minGroup) throw new BusinessRuleException("MIN_GROUP_NOT_MET", $"Cần tối thiểu {minGroup} người (kể cả bạn) để đặt không gian này.");
        if (partySize > space.Capacity) throw new BusinessRuleException("PARTY_SIZE_EXCEEDS_CAPACITY", $"Không gian chỉ chứa tối đa {space.Capacity} người.");

        if (request.StartAt < now.AddMinutes(space.MinAdvanceMinutes))
            throw new BusinessRuleException("MIN_ADVANCE_NOT_MET", $"Phải đặt trước ít nhất {space.MinAdvanceMinutes} phút.");
        var maxAdvance = space.MaxAdvanceHours is { } hoursAhead and > 0 ? now.AddHours(hoursAhead) : now.AddDays(space.MaxAdvanceDays);
        if (request.StartAt > maxAdvance) throw new BusinessRuleException("MAX_ADVANCE_EXCEEDED", "Vượt quá thời hạn đặt trước cho phép.");

        var settingsRow = await settings.GetAsync(ct);
        if (settingsRow.MaxSessionMinutes > 0 && (request.EndAt - request.StartAt).TotalMinutes > settingsRow.MaxSessionMinutes)
            throw new BusinessRuleException("SESSION_TOO_LONG", $"Một lượt đặt tối đa {settingsRow.MaxSessionMinutes} phút.");

        await EnsureNoOverlapAsync(space.Id, reader.ReaderPublicId, members.Select(m => m.ReaderPublicId), request.StartAt, request.EndAt, ct);
        await EnsurePerReaderLimitsAsync(space, reader.ReaderPublicId, request.StartAt, request.EndAt, settingsRow, ct);

        var booking = RoomBooking.Open(reader.ReaderPublicId, reader.CardNo, space.Id, request.StartAt, request.EndAt, partySize, request.Note,
            space.AutoApprove, now, approvedBy: null);
        Set.Add(booking);
        await AuditAsync(booking, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct); // cần Id để ghi thành viên
        foreach (var member in members) Db.Set<RoomBookingMember>().Add(RoomBookingMember.Create(booking.Id, member.ReaderPublicId, member.CardNo));

        var template = booking.Status == RoomBookingStatus.Approved ? SpaceTemplates.Approved : SpaceTemplates.Created;
        await notifier.NotifyAsync(reader, template, BookingTokens(booking, space.Name), $"{template}:{booking.Id}", ct);
        await Db.SaveChangesAsync(ct);

        return (await ToDtosAsync([booking], ct))[0];
    }

    public async Task<BookingDto> ApproveAsync(Guid publicId, CancellationToken ct)
    {
        var booking = await LoadAsync(publicId, ct);
        booking.Approve(actor.Id, clock.GetUtcNow());
        await Record(booking, CrudChange.StatusChanged, $"Duyệt lượt đặt — thẻ {booking.CardNo}", ct);
        await NotifyBookingAsync(booking, SpaceTemplates.Approved, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    public async Task<BookingDto> RejectAsync(RejectBookingRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var booking = await LoadAsync(request.PublicId, ct);
        booking.Reject(request.Reason, actor.Id, clock.GetUtcNow());
        await Record(booking, CrudChange.StatusChanged, $"Từ chối lượt đặt — thẻ {booking.CardNo}. Lý do: {request.Reason}", ct);
        await NotifyBookingAsync(booking, SpaceTemplates.Rejected, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    /// <summary>Check-in bởi thủ thư — quét/gõ mã (QR hoặc publicId trực tiếp), kèm tuỳ chọn điểm kiosk để chỉ nhận đúng phòng đó.</summary>
    public async Task<BookingDto> CheckInAsync(CheckInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var booking = await LoadAsync(request.PublicId, ct);
        if (request.KioskSpaceId is { } kiosk && booking.SpaceId != kiosk)
            throw new BusinessRuleException("WRONG_KIOSK_ROOM", "Lượt đặt không thuộc không gian tại kiosk này.");
        await CheckInCoreAsync(booking, ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    /// <summary>Check-in theo số thẻ (sau khi nhận diện khuôn mặt ở Patron trả về thẻ) — tìm lượt Đã duyệt sắp tới của thẻ đó.</summary>
    public async Task<BookingDto> CheckInByCardAsync(CheckInByCardRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reader = await replicas.ReaderAsync(request.CardNo, ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{request.CardNo?.Trim()}'.", 404);
        var now = clock.GetUtcNow();
        var memberBookingIds = Db.Set<RoomBookingMember>().Where(m => m.ReaderPublicId == reader.ReaderPublicId).Select(m => m.BookingId);
        var query = Set.Where(x => x.Status == RoomBookingStatus.Approved && (x.ReaderPublicId == reader.ReaderPublicId || memberBookingIds.Contains(x.Id)));
        if (request.KioskSpaceId is { } kiosk) query = query.Where(x => x.SpaceId == kiosk);
        var booking = await query.OrderBy(x => x.StartAt).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("NO_DUE_BOOKING", $"Thẻ {reader.CardNo} không có lượt đặt phòng Đã duyệt nào đang chờ check-in.", 404);
        await CheckInCoreAsync(booking, ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    /// <summary>Bạn đọc tự check-in (OPAC) — chỉ chủ phòng hoặc thành viên nhóm.</summary>
    public async Task<BookingDto> SelfCheckInAsync(Guid readerPublicId, Guid bookingPublicId, CancellationToken ct)
    {
        var booking = await RequireParticipantBookingAsync(readerPublicId, bookingPublicId, ct);
        await CheckInCoreAsync(booking, ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    private async Task CheckInCoreAsync(RoomBooking booking, CancellationToken ct)
    {
        var space = await Db.Set<BookableSpace>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == booking.SpaceId, ct);
        var grace = space?.CheckInGraceMinutes ?? BookableSpace.DefaultCheckInGraceMinutes;
        var now = clock.GetUtcNow();
        var windowStart = booking.StartAt.AddMinutes(-CheckInOpenMinutes);
        var windowEnd = booking.EndAt < booking.StartAt.AddMinutes(grace) ? booking.EndAt : booking.StartAt.AddMinutes(grace);
        if (now < windowStart || now > windowEnd)
            throw new BusinessRuleException("OUTSIDE_CHECK_IN_WINDOW", $"Chỉ check-in được từ {windowStart:HH:mm dd/MM} đến {windowEnd:HH:mm dd/MM}.");
        booking.CheckIn(actor.Id, now);
        await Record(booking, CrudChange.StatusChanged, $"Check-in — thẻ {booking.CardNo}", ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task<BookingDto> CheckOutAsync(Guid publicId, CancellationToken ct)
    {
        var booking = await LoadAsync(publicId, ct);
        booking.CheckOut(actor.Id, clock.GetUtcNow());
        await Record(booking, CrudChange.StatusChanged, $"Trả phòng — thẻ {booking.CardNo}", ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    public async Task<BookingDto> SelfCheckOutAsync(Guid readerPublicId, Guid bookingPublicId, CancellationToken ct)
    {
        var booking = await RequireParticipantBookingAsync(readerPublicId, bookingPublicId, ct);
        booking.CheckOut(null, clock.GetUtcNow());
        await Record(booking, CrudChange.StatusChanged, $"Bạn đọc tự trả phòng — thẻ {booking.CardNo}", ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    /// <summary>Huỷ — chỉ chủ phòng, chưa tới giờ bắt đầu.</summary>
    public async Task<BookingDto> CancelAsync(Guid readerPublicId, Guid bookingPublicId, CancellationToken ct)
    {
        var booking = await Set.FirstOrDefaultAsync(x => x.PublicId == bookingPublicId, ct) ?? throw new NotFoundException(EntityName, bookingPublicId);
        if (booking.ReaderPublicId != readerPublicId) throw new NotFoundException(EntityName, bookingPublicId);
        booking.Cancel(clock.GetUtcNow());
        await Record(booking, CrudChange.StatusChanged, $"Bạn đọc tự huỷ — thẻ {booking.CardNo}", ct);
        await NotifyBookingAsync(booking, SpaceTemplates.Cancelled, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([booking], ct))[0];
    }

    public async Task<IReadOnlyList<MyBookingDto>> MyBookingsAsync(Guid readerPublicId, CancellationToken ct)
    {
        var ownIds = Set.Where(x => x.ReaderPublicId == readerPublicId).Select(x => x.Id);
        var memberIds = Db.Set<RoomBookingMember>().Where(m => m.ReaderPublicId == readerPublicId).Select(m => m.BookingId);
        var ids = await ownIds.Union(memberIds).ToListAsync(ct);
        var bookings = await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderByDescending(x => x.StartAt).ToListAsync(ct);
        var dtos = await WithMembersAsync(await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(Projection).ToListAsync(ct), ct);
        return dtos.Select(d => new MyBookingDto(d.ForReader(), bookings.First(b => b.PublicId == d.PublicId).ReaderPublicId == readerPublicId)).ToList();
    }

    /// <summary>Giờ mở cửa + khung giờ đã có người đặt trong ngày — cho lưới chọn giờ ở OPAC (ẩn danh hoặc bạn đọc).</summary>
    public async Task<SpaceAvailability> AvailabilityAsync(long spaceId, DateOnly date, CancellationToken ct)
    {
        var space = await Db.Set<BookableSpace>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == spaceId && s.Status == IHasStatus.Active, ct)
            ?? throw new NotFoundException("Không gian", spaceId);
        var resolved = await hours.ResolveAsync(date, space.Category, ct);
        var dayStart = Local(date);
        var dayEnd = Local(date.AddDays(1));
        var busy = await Set.AsNoTracking().Where(x => x.SpaceId == spaceId && OpenStatuses.Contains(x.Status) && x.StartAt < dayEnd && x.EndAt > dayStart)
            .Select(x => new BusySlot(x.StartAt, x.EndAt)).ToListAsync(ct);
        return new SpaceAvailability(resolved.Closed, resolved.OpenTime, resolved.CloseTime, resolved.Reason, space.Maintenance, space.MaintenanceNote, busy);
    }

    public async Task<MemberDto?> LookupMemberAsync(string cardNo, Guid excludeReaderPublicId, CancellationToken ct)
    {
        var reader = await replicas.ReaderAsync(cardNo, ct);
        if (reader is null || reader.ReaderPublicId == excludeReaderPublicId) return null;
        return new MemberDto(reader.ReaderPublicId, reader.CardNo, Mask(reader.FullName));
    }

    /// <summary>Như SearchAsync nhưng kèm danh sách thành viên nhóm mỗi lượt — màn Yêu cầu cần hiện "+N thành viên".</summary>
    public async Task<CrudPage<BookingDto>> SearchWithMembersAsync(BookingSearch search, CancellationToken ct)
    {
        var page = await SearchAsync(search, ct);
        var withMembers = await WithMembersAsync(page.Items, ct);
        return page with { Items = withMembers };
    }

    private async Task<RoomBooking> RequireParticipantBookingAsync(Guid readerPublicId, Guid bookingPublicId, CancellationToken ct)
    {
        var booking = await Set.FirstOrDefaultAsync(x => x.PublicId == bookingPublicId, ct) ?? throw new NotFoundException(EntityName, bookingPublicId);
        var isMember = booking.ReaderPublicId == readerPublicId
            || await Db.Set<RoomBookingMember>().AnyAsync(m => m.BookingId == booking.Id && m.ReaderPublicId == readerPublicId, ct);
        if (!isMember) throw new NotFoundException(EntityName, bookingPublicId);
        return booking;
    }

    private async Task<List<PatronReplica>> ResolveMembersAsync(IReadOnlyList<string>? cards, Guid ownerReaderPublicId, CancellationToken ct)
    {
        if (cards is null || cards.Count == 0) return [];
        var trimmed = cards.Select(c => (c ?? "").Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (trimmed.Count > 50) throw new BusinessRuleException("TOO_MANY_MEMBERS", "Tối đa 50 thành viên mỗi lượt đặt.");
        var members = new List<PatronReplica>();
        foreach (var card in trimmed)
        {
            var reader = await replicas.ReaderAsync(card, ct) ?? throw new BusinessRuleException("MEMBER_NOT_FOUND", $"Không tìm thấy bạn đọc với thẻ '{card}'.");
            if (reader.ReaderPublicId == ownerReaderPublicId) continue;
            if (await bans.IsBannedAsync(reader.ReaderPublicId, ct))
                throw new BusinessRuleException("MEMBER_BANNED", $"Thẻ {reader.CardNo} đang bị cấm đặt phòng.", 403);
            members.Add(reader);
        }
        return [.. members.DistinctBy(m => m.ReaderPublicId)];
    }

    private async Task EnsureNoOverlapAsync(long spaceId, Guid ownerReaderPublicId, IEnumerable<Guid> memberReaderPublicIds, DateTimeOffset startAt,
        DateTimeOffset endAt, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.SpaceId == spaceId && OpenStatuses.Contains(x.Status) && x.StartAt < endAt && x.EndAt > startAt, ct))
            throw new ConflictException("SPACE_BOOKING_OVERLAP", "Không gian đã có lượt đặt khác trong khoảng giờ này.");

        var participantIds = memberReaderPublicIds.Append(ownerReaderPublicId).Distinct().ToList();
        var overlapping = Set.Where(x => OpenStatuses.Contains(x.Status) && x.StartAt < endAt && x.EndAt > startAt);
        var ownerClash = await overlapping.AnyAsync(x => participantIds.Contains(x.ReaderPublicId), ct);
        var memberClash = !ownerClash && await Db.Set<RoomBookingMember>()
            .Join(overlapping, m => m.BookingId, x => x.Id, (m, x) => m)
            .AnyAsync(m => participantIds.Contains(m.ReaderPublicId), ct);
        if (ownerClash || memberClash) throw new ConflictException("READER_BUSY", "Bạn hoặc một thành viên trong nhóm đã có lượt đặt khác trong khoảng giờ này.");
    }

    private async Task EnsurePerReaderLimitsAsync(BookableSpace space, Guid readerPublicId, DateTimeOffset startAt, DateTimeOffset endAt,
        SettingsDto settingsRow, CancellationToken ct)
    {
        var day = RoomBooking.LocalDate(startAt);
        var addedMinutes = (endAt - startAt).TotalMinutes;

        if (space.MaxBookingMinutesPerReader > 0)
        {
            var sameDaySameSpace = await Set.AsNoTracking()
                .Where(x => x.SpaceId == space.Id && x.ReaderPublicId == readerPublicId && OpenStatuses.Contains(x.Status))
                .Select(x => new { x.StartAt, x.EndAt }).ToListAsync(ct);
            var usedMinutes = sameDaySameSpace.Where(x => RoomBooking.LocalDate(x.StartAt) == day).Sum(x => (x.EndAt - x.StartAt).TotalMinutes);
            if (usedMinutes + addedMinutes > space.MaxBookingMinutesPerReader)
                throw new BusinessRuleException("MAX_MINUTES_PER_DAY_EXCEEDED", $"Bạn đã đặt đủ {space.MaxBookingMinutesPerReader} phút/ngày cho không gian này.");
        }

        if (settingsRow.MaxPerDay > 0)
        {
            var countedStatuses = new[] { RoomBookingStatus.Pending, RoomBookingStatus.Approved, RoomBookingStatus.CheckedIn, RoomBookingStatus.Completed };
            var sameDay = await Set.AsNoTracking().Where(x => x.ReaderPublicId == readerPublicId && countedStatuses.Contains(x.Status))
                .Select(x => x.StartAt).ToListAsync(ct);
            if (sameDay.Count(x => RoomBooking.LocalDate(x) == day) + 1 > settingsRow.MaxPerDay)
                throw new BusinessRuleException("MAX_PER_DAY_EXCEEDED", $"Bạn đã đặt đủ {settingsRow.MaxPerDay} lượt trong ngày.");
        }

        if (settingsRow.MaxActive > 0)
        {
            var activeStatuses = new[] { RoomBookingStatus.Pending, RoomBookingStatus.Approved };
            var active = await Set.CountAsync(x => x.ReaderPublicId == readerPublicId && activeStatuses.Contains(x.Status), ct);
            if (active + 1 > settingsRow.MaxActive) throw new BusinessRuleException("MAX_ACTIVE_EXCEEDED", $"Bạn đang có đủ {settingsRow.MaxActive} lượt đặt đang hoạt động.");
        }
    }

    /// <summary>Báo bạn đọc (outbox) — publish TRƯỚC SaveChanges để event ghi cùng transaction, đúng quy ước toàn dự án.</summary>
    private async Task NotifyBookingAsync(RoomBooking booking, string template, CancellationToken ct)
    {
        var reader = await replicas.ByReaderPublicIdAsync(booking.ReaderPublicId, ct);
        var spaceName = await Db.Set<BookableSpace>().AsNoTracking().Where(s => s.Id == booking.SpaceId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        await notifier.NotifyAsync(reader, template, BookingTokens(booking, spaceName), $"{template}:{booking.Id}", ct);
    }

    private static Dictionary<string, string> BookingTokens(RoomBooking booking, string? spaceName) => new()
    {
        ["space_name"] = spaceName ?? "",
        ["start_at"] = booking.StartAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        ["end_at"] = booking.EndAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        ["reject_reason"] = booking.RejectReason ?? "",
    };

    private async Task<IReadOnlyList<BookingDto>> ToDtosAsync(IReadOnlyList<RoomBooking> bookings, CancellationToken ct)
    {
        var ids = bookings.Select(b => b.Id).ToList();
        var dtos = await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(Projection).ToListAsync(ct);
        return await WithMembersAsync(dtos, ct);
    }

    private async Task<IReadOnlyList<BookingDto>> WithMembersAsync(IReadOnlyList<BookingDto> dtos, CancellationToken ct)
    {
        if (dtos.Count == 0) return dtos;
        var ids = dtos.Select(d => d.Id).ToList();
        var rows = await Db.Set<RoomBookingMember>().AsNoTracking().Where(m => ids.Contains(m.BookingId))
            .Select(m => new { m.BookingId, m.ReaderPublicId, m.CardNo, Name = Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == m.ReaderPublicId).Select(r => r.FullName).FirstOrDefault() })
            .ToListAsync(ct);
        return [.. dtos.Select(d => d with { Members = [.. rows.Where(r => r.BookingId == d.Id).Select(r => new MemberDto(r.ReaderPublicId, r.CardNo, r.Name))] })];
    }

    private Task Record(RoomBooking booking, CrudChange change, string summary, CancellationToken ct) =>
        AuditSink?.RecordAsync(new CrudAuditEntry(nameof(RoomBooking), EntityName, booking.PublicId, change, summary), ct) ?? Task.CompletedTask;

    private static DateTimeOffset Local(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();

    private static string Mask(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return fullName;
        return parts.Length == 1 ? parts[0] : $"{parts[0]} {string.Concat(parts.Skip(1).Select(p => p[0] + "."))}";
    }
}
