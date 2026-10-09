using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("worksheet_field", Schema = "PrintBook")]
public class WorksheetField
{
    [Key] public long Id { get; set; }
    public int?    Bib_Worksheet_Id { get; set; }
    public string? Field            { get; set; }
    public string? L1               { get; set; }
    public string? L2               { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

