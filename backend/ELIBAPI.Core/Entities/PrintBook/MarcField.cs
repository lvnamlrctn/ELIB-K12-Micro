using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Marc_Field", Schema = "PrintBook")]
public class MarcField
{
    [Key] public int Id { get; set; }
    public string? Field       { get; set; }
    public string? Description { get; set; }
    public string? Vndescription { get; set; }
    public int?    Repeatable  { get; set; }
    public int?    MANDATORY   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

