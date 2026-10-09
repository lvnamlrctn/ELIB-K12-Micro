using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Store", Schema = "PrintBook")]
public class Store
{
    [Key] public long Id { get; set; }
    public string? Name        { get; set; }
    public string? Postion     { get; set; }
    public long?   StoreTypeId { get; set; }
    public string? Code        { get; set; }
    public string? Images      { get; set; }
    /// <summary>Đợt 22.4 — sức chứa (số bản) để tính tỷ lệ lấp đầy ở dashboard Bổ sung &amp; Kho. null = chưa
    /// cấu hình (hiển thị "chưa cấu hình", không phải 0%).</summary>
    public int?    Capacity    { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

