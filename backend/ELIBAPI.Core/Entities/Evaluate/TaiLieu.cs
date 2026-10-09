using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Evaluate;

[Table("TaiLieu", Schema = "Evaluate")]
public class TaiLieu
{
    [Key] public long    Id           { get; set; }
    public long?   BibId        { get; set; }
    public string? Title        { get; set; }
    public string? Author       { get; set; }
    public string? Publisher    { get; set; }
    public string? Url          { get; set; }
    public string? PublishDate  { get; set; }
    public int?    LoaiTaiLieu  { get; set; }
    public string? LanXuatBan   { get; set; }
    public string? Note         { get; set; }
    public long?   EBookId      { get; set; }
    public long?   MonHocId     { get; set; }
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

