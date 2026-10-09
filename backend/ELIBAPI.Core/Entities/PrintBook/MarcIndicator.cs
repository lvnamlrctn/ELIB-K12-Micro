using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Marc_Indicator", Schema = "PrintBook")]
public class MarcIndicator
{
    [Key] public int Id { get; set; }
    public string? INDICATOR    { get; set; }
    public string? Value        { get; set; }
    public string? Description  { get; set; }
    public string? VNDESCRIPTION { get; set; }
    public string? Field_Id     { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

