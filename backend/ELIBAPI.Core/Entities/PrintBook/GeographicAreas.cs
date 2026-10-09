using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("GeographicAreas", Schema = "PrintBook")]
public class GeographicAreas
{
    [Key] public long Id { get; set; }
    public string? Code          { get; set; }
    public string? VnDescription { get; set; }
    public string? Description   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

