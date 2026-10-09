using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("CabinetCompartment", Schema = "PrintBook")]
public class CabinetCompartment
{
    [Key] public long Id { get; set; }
    public long   CabinetId { get; set; }
    public int    RowIndex  { get; set; }
    public int    ColIndex  { get; set; }
    public string? Code     { get; set; }
    public string? Name     { get; set; }
    public string? Note     { get; set; }
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}
