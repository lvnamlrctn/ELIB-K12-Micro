using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("worksheet_subfield", Schema = "PrintBook")]
public class WorksheetSubfield
{
    [Key] public long Id { get; set; }
    public long?   Worksheet_Field_Id { get; set; }
    public string? Subfield           { get; set; }
    public string? Value              { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

