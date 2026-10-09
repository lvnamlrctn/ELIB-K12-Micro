using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Gamification;

// Huy hiệu đã cấp cho 1 bạn đọc — chỉ thêm (BadgeEvaluationJob), không có màn sửa/xoá qua UI ngoài
// soft-delete để đính chính khi cần. TenantId lưu lại tường minh (dù suy ra được qua ReaderId/BadgeId)
// để lọc/thống kê theo tenant không phải join thêm bảng — cùng quy ước TenantId trên mọi entity của dự án.
[Table("ReaderBadge", Schema = "Gamification")]
public class ReaderBadge
{
    [Key] public long      Id         { get; set; }
    public long      ReaderId   { get; set; }
    public long      BadgeId    { get; set; }
    public DateTime  EarnedAt   { get; set; }
    /// Null = email/SMS/Zalo chưa gửi được hoặc chưa cấu hình — xem BadgeEvaluationJob.
    public DateTime? NotifiedAt { get; set; }
    // Audit Trail
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public long?      TenantId      { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }

    [NotMapped] public string? BadgeCode     { get; set; }
    [NotMapped] public string? BadgeName     { get; set; }
    [NotMapped] public string? BadgeIconName { get; set; }
    [NotMapped] public string? BadgeDescription { get; set; }
}
