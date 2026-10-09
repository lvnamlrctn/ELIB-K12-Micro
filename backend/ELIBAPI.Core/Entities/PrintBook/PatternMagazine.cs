using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("PatternMagazine", Schema = "PrintBook")]
public class PatternMagazine
{
    [Key] public long Id { get; set; }
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? Function    { get; set; }
    public int?    Order       { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

