using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ab_order", Schema = "PrintBook")]
public class AbOrder
{
    [Key] public long Id { get; set; }
    public DateTime? Date_Order      { get; set; }
    public DateTime? Duedate         { get; set; }
    public long?     Source_Id       { get; set; }
    public int?      Supplier_Id     { get; set; }
    public int?      BUDGET_ID       { get; set; }
    public long?     CREATED_BY      { get; set; }
    public string?   Order_Name      { get; set; }
    public long?     Code            { get; set; }
    public string?   Note            { get; set; }
    public DateTime? CreatedDate     { get; set; }
    public int?      PaymentMethodId { get; set; }
    public long?     FundId          { get; set; }
    public int?      Payment_status  { get; set; }
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

