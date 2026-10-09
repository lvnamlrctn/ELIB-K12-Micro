using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Fund", Schema = "PrintBook")]
public class Fund
{
    [Key] public long Id { get; set; }
    public string? Name       { get; set; }
    public string? Manager    { get; set; }
    public string? Note       { get; set; }
    public string? Purpose    { get; set; }
    public double? Blane      { get; set; }
    public long?   BudgetId   { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

