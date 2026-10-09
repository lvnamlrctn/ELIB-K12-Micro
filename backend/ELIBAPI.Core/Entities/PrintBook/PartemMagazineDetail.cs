using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("PartemMagazineDetail", Schema = "PrintBook")]
public class PartemMagazineDetail
{
    [Key] public long Id { get; set; }
    public long? PatternId { get; set; }
    public string? X       { get; set; }
    public string? Y       { get; set; }
    public string? Z       { get; set; }
    public int?   StepX    { get; set; }
    public int?   StepY    { get; set; }
    public int?   StepZ    { get; set; }
    public int?   RepeatX  { get; set; }
    public int?   RepeatY  { get; set; }
    public int?   RepeatZ  { get; set; }
    public int?   MaxX     { get; set; }
    public int?   MaxY     { get; set; }
    public int?   MaxZ     { get; set; }
    public int?   ResetX   { get; set; }
    public int?   ResetY   { get; set; }
    public int?   ResetZ   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

