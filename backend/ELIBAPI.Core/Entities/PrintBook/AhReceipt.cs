using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ah_receipt", Schema = "PrintBook")]
public class AhReceipt
{
    [Key] public long Id { get; set; }
    public DateTime? Receipt_Date    { get; set; }
    public long?     Source_Id       { get; set; }
    public long?     Supplier_Id     { get; set; }
    public long?     BUDGET_ID       { get; set; }
    public long?     CREATED_BY      { get; set; }
    public string?   Receipt_Name    { get; set; }
    public long?     Code            { get; set; }
    public int?      Payment_Status  { get; set; }
    public string?   Note            { get; set; }
    public long?     Store_Id        { get; set; }
    public DateTime? CreatedDate     { get; set; }
    public int?      PaymentMethodId { get; set; }
    public long?     FundId          { get; set; }
    public int?      Status         { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

