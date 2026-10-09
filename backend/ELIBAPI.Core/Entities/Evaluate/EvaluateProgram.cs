using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Evaluate;

[Table("Program", Schema = "Evaluate")]
public class EvaluateProgram
{
    [Key] public long    Id          { get; set; }
    public string? Name        { get; set; }
    public string? Description { get; set; }
    /// <summary>Đơn vị đào tạo sở hữu chương trình này — FK sang DonVi (Đợt 8). DonVi là entity RIÊNG,
    /// KHÁC với Org (Org = phòng ban nội bộ thư viện/hệ thống; DonVi = đơn vị đào tạo học thuật như
    /// Khoa/Viện/Trường — 2 khái niệm khác nhau dù cùng hình dạng Name/ParentId/Level/Status/Order).</summary>
    public long?   DonViId     { get; set; }
    public string? PortalId    { get; set; }
    public string? Language    { get; set; }
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

