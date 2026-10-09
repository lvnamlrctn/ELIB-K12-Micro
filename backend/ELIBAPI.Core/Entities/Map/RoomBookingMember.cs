using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

/// Thành viên nhóm của 1 lượt đặt phòng (không gồm người đặt RoomBooking.ReaderId). Thẻ của thành viên cũng mở được cửa phòng
/// và check-in được; chỉ người đặt huỷ được lượt đặt.
[Table("RoomBookingMember", Schema = "map")]
public class RoomBookingMember
{
    [Key] public long Id { get; set; }
    public long      BookingId      { get; set; }
    public long      ReaderId       { get; set; }
    /// Đơn vị (K12): cùng đơn vị với bạn đọc / phòng liên quan.
    public long?     TenantId       { get; set; }
    public DateTime? CreatedRowDate { get; set; }
}
