using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Budget", Schema = "PrintBook")]
public class Budget
{
    [Key] public long Id { get; set; }
    public string?   Name      { get; set; }
    public double?   Blance    { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime   { get; set; }
    public string?   Note      { get; set; }
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

