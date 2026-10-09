using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Map;

// Cấu hình đặt chỗ cho 1 MapObject (ObjectType = ROOM/STUDY_SPACE) — chỉ những phòng có dòng cấu hình
// này mới đặt được qua OPAC; admin tạo/sửa qua màn CRUD thường, không có màn nào khác sinh dòng này.
// Khác ELIB-LRC (đơn-tenant): có thêm TenantId (do BaseRepository tự gán/lọc qua JWT ở phía admin).
[Table("RoomBookingConfig", Schema = "map")]
public class RoomBookingConfig
{
    [Key] public long Id { get; set; }
    public long   MapObjectId       { get; set; }
    public int    Capacity          { get; set; }
    public int    SlotMinutes       { get; set; } = 60;
    public int    MinAdvanceMinutes { get; set; }
    public int    MaxAdvanceDays    { get; set; } = 7;
    public int    MaxBookingMinutesPerReader { get; set; }
    /// Số phút sau StartAt mà chưa check-in thì job tự chuyển NoShow, giải phóng chỗ.
    public int    CheckInGraceMinutes { get; set; } = 15;
    /// Loại phòng: true = tự động duyệt (bạn đọc đặt xong là Approved ngay), false = chờ thủ thư duyệt.
    public bool   AutoApprove       { get; set; }
    /// Đặt trước tối đa tính theo giờ (vd 4 = chỉ đặt trong vòng 4 giờ tới); &gt; 0 thì thay cho MaxAdvanceDays.
    public int?   MaxAdvanceHours   { get; set; }
    /// Nội quy / chính sách sử dụng phòng, hiện cho bạn đọc khi chọn phòng.
    public string? Rules            { get; set; }
    /// Loại bạn đọc được đặt phòng (Id ReaderType, cách nhau dấu phẩy); trống = mọi đối tượng.
    public string? AllowedReaderTypeIds { get; set; }
    /// Số người tối thiểu (kể cả người đặt) — phòng học nhóm bắt buộc nhập thẻ thành viên. null/≤1 = không yêu cầu.
    public int?   MinGroupSize      { get; set; }
    /// Thủ thư tạm ngưng phòng (bảo trì/hỏng thiết bị): không nhận đặt mới, OPAC hiện xám kèm MaintenanceNote.
    public bool   Maintenance       { get; set; }
    public string? MaintenanceNote  { get; set; }
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?      TenantId      { get; set; }
    public Guid      PublicId       { get; set; }

    [NotMapped] public string? RoomName { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}
