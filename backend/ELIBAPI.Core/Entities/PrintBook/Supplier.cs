using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Supplier", Schema = "PrintBook")]
public class Supplier
{
    [Key] public int Id { get; set; }
    public string? Name           { get; set; }
    public string? Address        { get; set; }
    public string? Mobile         { get; set; }
    public string? Fax            { get; set; }
    public string? Account        { get; set; }
    public string? Bank           { get; set; }
    public string? Mst            { get; set; }
    public string? Email          { get; set; }
    public string? Website        { get; set; }
    public string? Position       { get; set; }
    public string? Representative { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

