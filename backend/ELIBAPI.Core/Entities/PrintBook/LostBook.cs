using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("LostBook", Schema = "PrintBook")]
public class LostBook
{
    [Key] public long Id { get; set; }
    public string?   Barcode   { get; set; }
    public DateTime? Submited  { get; set; }
    public long?     Store     { get; set; }
    public long?     CreatedBy { get; set; }
    public string?   Reason    { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

