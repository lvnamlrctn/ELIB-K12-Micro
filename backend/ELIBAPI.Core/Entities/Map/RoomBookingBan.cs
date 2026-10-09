using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

/// Danh sách đen đặt phòng: bạn đọc bị tạm khoá đặt phòng tới BannedUntil (UTC). Source 1 = hệ thống tự khoá vì vắng mặt
/// quá ROOM_BOOKING_NOSHOW_LIMIT lần, 2 = thủ thư khoá tay. Gỡ khoá ghi LiftedAt/LiftedBy (không xoá dòng).
[Table("RoomBookingBan", Schema = "map")]
public class RoomBookingBan
{
    [Key] public long Id { get; set; }
    public long      ReaderId       { get; set; }
    public string?   Reason         { get; set; }
    public int       Source         { get; set; }
    public DateTime  BannedUntil    { get; set; }
    public DateTime? LiftedAt       { get; set; }
    public long?     LiftedBy       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    /// Đơn vị (K12): cùng đơn vị với bạn đọc / phòng liên quan.
    public long?     TenantId       { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? ReaderName   { get; set; }
    [NotMapped] public string? ReaderCardNo { get; set; }
    [NotMapped] public string? TenantName   { get; set; }
}
