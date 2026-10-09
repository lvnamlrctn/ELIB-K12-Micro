using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ab_deliverer", Schema = "PrintBook")]
public class AbDeliverer
{
    [Key] public long Id { get; set; }
    public long?     Code              { get; set; }
    public string?   ReceiptName       { get; set; }
    public string?   ReceiptAddress    { get; set; }
    public string?   DelivererName     { get; set; }
    public string?   DelivererAddress  { get; set; }
    public DateTime? DelivererDate     { get; set; }
    public DateTime? ReceiptDate       { get; set; }
    public int?      UserIddeliverer   { get; set; }
    public int?      UserIdReceipt     { get; set; }
    public int?      Store_Id          { get; set; }
    public int?      Receipt_Id        { get; set; }
    public string?   Note              { get; set; }
    public int?      Sign              { get; set; }
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

