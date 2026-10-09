using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Gamification;

// Danh mục huy hiệu đọc (Gamification) — quản trị viên tự tạo/sửa/xoá qua màn CRUD thường, chọn
// CriteriaType qua dropdown + nhập Threshold, không cần 1 DSL luật tuỳ biến.
// Khác ELIB-LRC (đơn-tenant, danh mục dùng chung): ELIB đa tenant — mỗi tenant tự định nghĩa huy hiệu
// riêng của mình (TenantId không null với dữ liệu thật, tương tự phần lớn danh mục nghiệp vụ khác),
// không phải danh mục toàn cục có bật/tắt theo tenant.
[Table("Badge", Schema = "Gamification")]
public class Badge
{
    [Key] public long    Id            { get; set; }
    /// Mã ổn định, không đổi sau khi tạo — dùng để BadgeEvaluationJob nhận diện huy hiệu đã cấp cho 1
    /// bạn đọc (tránh cấp trùng khi Name bị sửa).
    public string? Code          { get; set; }
    public string? Name          { get; set; }
    public string? Description   { get; set; }
    /// Tên icon Material (mirror MapObject.IconName) — hiển thị ở tab "Huy hiệu".
    public string? IconName      { get; set; }
    /// TotalDigitalReads | DistinctDigitalTitles | TotalPagesRead | TotalPrintBorrows — huy hiệu dạng
    /// streak (đọc liên tục N ngày) để fast-follow sau vì EbookLog không có khái niệm phiên đọc.
    public string? CriteriaType  { get; set; }
    public long?   Threshold     { get; set; }
    public int?    SortOrder     { get; set; }
    public int?    Status        { get; set; }
    // Audit Trail
    public int?      IsDelete       { get; set; }
    public long?      CreatedRowBy   { get; set; }
    public long?      UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?      TenantId       { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}
