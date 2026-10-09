using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Evaluate;

/// <summary>Đơn vị đào tạo (Khoa/Viện/Trường...) — cây phân cấp sở hữu Chương trình đào tạo
/// (EvaluateProgram.DonViId). KHÁC với ELIBAPI.Core.Entities.Dbo.Org (phòng ban nội bộ hệ thống) dù cùng
/// hình dạng Name/ParentId/Level/Status/Order — 2 khái niệm nghiệp vụ độc lập, không dùng chung.</summary>
[Table("DonVi", Schema = "Evaluate")]
public class DonVi
{
    [Key] public long    Id       { get; set; }
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public long?   Level    { get; set; }
    public int?    Status   { get; set; }
    public int?    Order    { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}
