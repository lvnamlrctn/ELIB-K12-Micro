using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Evaluate;

[Table("MonHoc", Schema = "Evaluate")]
public class MonHoc
{
    [Key] public long    Id             { get; set; }
    public string? MaMon          { get; set; }
    public string? TenMon         { get; set; }
    public int?    SoTinChi       { get; set; }
    public long?   DegreeId       { get; set; }
    public long?   KnowledgeId    { get; set; }
    public int?    OptionId       { get; set; }
    public string? NguoiBienSoan  { get; set; }
    public int?    Active         { get; set; }
    public string? Attachment     { get; set; }
    public string? Note           { get; set; }
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

