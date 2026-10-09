using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("StoreType", Schema = "PrintBook")]
public class StoreType
{
    [Key] public long Id { get; set; }
    public string? Name     { get; set; }
    public long?   ParentId { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    [NotMapped] public string? TenantName { get; set; }
    public Guid      PublicId       { get; set; }
}

