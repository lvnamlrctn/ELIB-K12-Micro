using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

// Đặt phòng học nhóm — mirror đúng cấu trúc status-enum + timestamp tường minh của
// Ebook.ItemReservation, mở rộng thêm Rejected (duyệt/từ chối) và NoShow (có check-in, không tới).
// Booking khởi tạo ở Status=1 (Pending) chờ nhân viên duyệt, trừ phòng cấu hình AutoApprove (tạo thẳng Status=2).
// StartAt/EndAt/CheckedInAt/CheckedOutAt lưu theo UTC (OPAC gửi ISO có "Z") — xem RoomBookingHours.
// Khác ELIB-LRC (đơn-tenant): có thêm TenantId — mọi truy vấn trên bảng này PHẢI lọc thêm theo TenantId
// dù MapObjectId/ReaderId đã tự gián tiếp tenant-scoped (defense-in-depth, đúng quy ước toàn dự án).
[Table("RoomBooking", Schema = "map")]
public class RoomBooking
{
    [Key] public long      Id          { get; set; }
    public long      MapObjectId { get; set; }
    public long      ReaderId    { get; set; }
    public DateTime  StartAt     { get; set; }
    public DateTime  EndAt       { get; set; }
    public int       PartySize   { get; set; }
    /// 1=Pending (chờ duyệt), 2=Approved (đã duyệt), 3=CheckedIn, 4=Completed, 5=Cancelled (bạn đọc tự
    /// hủy), 6=Rejected (từ chối/hết hạn chưa duyệt), 7=NoShow (đã duyệt nhưng không check-in đúng giờ).
    public int       Status      { get; set; }
    public long?     ApprovedBy  { get; set; }
    public DateTime? ApprovedAt  { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    /// Bạn đọc/thủ thư trả phòng (kết thúc sớm) → Status 4; phòng được giải phóng ngay, EndAt giữ nguyên làm lịch sử.
    public DateTime? CheckedOutAt { get; set; }
    public string?   Note        { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?      TenantId      { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? RoomName   { get; set; }
    [NotMapped] public string? ReaderName { get; set; }
    [NotMapped] public string? ReaderCardNo { get; set; }
    [NotMapped] public string? TenantName { get; set; }
    /// Thành viên nhóm "Họ tên (số thẻ)" — chỉ điền khi đọc cho màn quản trị.
    [NotMapped] public List<string>? Members { get; set; }
}
