using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("DFixFieldPost", Schema = "PrintBook")]
public class DFixFieldPost
{
    [Key] public long Id { get; set; }
    public int?    PostNumber    { get; set; }
    public int?    PostLengh     { get; set; }
    public long?   FixFieldId   { get; set; }
    public string? VnDesciption { get; set; }
    public string? Description  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

