namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Phần đọc của trang đặt phòng học nhóm trên OPAC (danh sách phòng, lịch bận theo tầng/phòng, lượt đặt của tôi).
/// Đặt/check-in/huỷ vẫn qua <see cref="IRoomBookingRepository"/>. Mọi mốc giờ trả về là UTC (Kind = Utc → JSON có "Z").
/// Port ELIB-LRC 09-28..10-04. Tenant (K12): mọi hàm nhận <c>tenantId</c> = đơn vị của bạn đọc đang đăng nhập (hoặc của tầng, ở màn thủ
/// thư) — chỉ phòng, lượt đặt, bạn đọc, giờ mở cửa của đơn vị đó.
/// </summary>
public interface IRoomBookingPortalService
{
    /// <summary>Phòng khả đặt (cấu hình đang bật, đối tượng sơ đồ chưa xoá). Enabled = false khi tắt ROOM_BOOKING_ENABLED.</summary>
    Task<(bool Enabled, List<BookableRoom> Rooms)> RoomsAsync(long? tenantId, long? readerId = null);

    /// <summary>Lượt đang giữ phòng (chờ duyệt/đã duyệt/đã check-in) chạm vào ngày <paramref name="date"/> theo giờ thư viện.</summary>
    Task<List<RoomBusySlot>> AvailabilityAsync(long mapObjectId, DateTime date, long? tenantId);

    /// <summary>Phòng khả đặt của 1 tầng + thiết bị + lịch bận trong ngày. Tên/mã thẻ người khác bị che, ghi chú chỉ trả cho
    /// chính người đặt (<paramref name="readerId"/>). Không trả publicId lượt đặt (nội dung mã QR check-in).</summary>
    /// <param name="staffView">Màn thủ thư: không che tên/mã thẻ, trả ghi chú mọi lượt.</param>
    Task<RoomFloorBoard> FloorBoardAsync(long floorId, DateTime date, long? tenantId, long? readerId, bool staffView = false);

    /// <summary>Tra thẻ thành viên (số thẻ hoặc UID chip) khi bạn đọc thêm vào nhóm — chỉ trả tên đã che, null nếu không có.</summary>
    Task<MemberCardLookup?> LookupMemberAsync(string card, long currentReaderId, long? tenantId);

    Task<List<MyRoomBooking>> MyBookingsAsync(long readerId, long? tenantId);
}

/// <summary>Rules: nội quy phòng. AllowedReaderTypes: tên loại bạn đọc được đặt (rỗng = mọi đối tượng). ReaderAllowed: bạn đọc đang
/// đăng nhập có thuộc đối tượng được đặt không (null = chưa đăng nhập).</summary>
public sealed record BookableRoom(long MapObjectId, string? Name, string? IconName, int Capacity, int SlotMinutes, bool AutoApprove,
    string? Rules, List<string> AllowedReaderTypes, bool? ReaderAllowed, int MinGroupSize, bool Maintenance, string? MaintenanceNote);

public sealed record RoomBusySlot(DateTime StartAt, DateTime EndAt, int Status);

public sealed record RoomEquipmentItem(string? Name, int? Quantity);

/// <summary>Members: tên thành viên nhóm — chỉ có ở màn thủ thư (staffView).</summary>
public sealed record RoomBoardBusy(DateTime StartAt, DateTime EndAt, int Status, bool IsMine, string ReaderName, string? ReaderCardNo, int PartySize, string? Note,
    Guid? PublicId = null, List<string>? Members = null);

/// <summary>OpenTime/CloseTime/Closed/ClosedReason: giờ mở cửa của riêng phòng trong ngày (theo thứ, loại cơ sở, ngày đặc biệt).
/// EarliestFreeAt: ô trống sớm nhất còn đặt được trong ngày (UTC, đã tính đặt trước tối thiểu) — null khi hết chỗ/đóng cửa.</summary>
public sealed record RoomBoardRoom(long MapObjectId, string? Name, string? IconName, int Capacity, int MinAdvanceMinutes, int MaxAdvanceDays,
    bool AutoApprove, List<RoomEquipmentItem> Equipment, List<RoomBoardBusy> Busy,
    int? MaxAdvanceHours, string? Rules, List<string> AllowedReaderTypes, bool? ReaderAllowed,
    string OpenTime, string CloseTime, bool Closed, string? ClosedReason, DateTime? EarliestFreeAt,
    int MinGroupSize, bool Maintenance, string? MaintenanceNote);

/// <summary>OpenTime/CloseTime: khung rộng nhất của các phòng còn mở trong ngày (trục giờ của lịch).</summary>
public sealed record RoomFloorBoard(bool Enabled, string OpenTime, string CloseTime, int StepMinutes, List<RoomBoardRoom> Rooms);

/// <summary><c>CheckInUntil</c>: hết ân hạn check-in (UTC) — quá mốc này lượt đã duyệt bị tính vắng mặt.
/// <c>IsOwner</c>: false = bạn đọc là thành viên nhóm (không huỷ được); <c>OwnerName</c>: người đặt; <c>Members</c>: thành viên.</summary>
public sealed record MyRoomBooking(Guid PublicId, string? RoomName, DateTime StartAt, DateTime EndAt, int PartySize, int Status, string? Note, DateTime CheckInUntil,
    bool IsOwner, string? OwnerName, List<string> Members);

public sealed record MemberCardLookup(string Card, string MaskedName);
