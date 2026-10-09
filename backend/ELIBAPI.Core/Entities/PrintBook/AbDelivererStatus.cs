using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ab_deliverer_status", Schema = "PrintBook")]
public class AbDelivererStatus
{
    [Key] public int Id { get; set; }
    public string? Name { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

