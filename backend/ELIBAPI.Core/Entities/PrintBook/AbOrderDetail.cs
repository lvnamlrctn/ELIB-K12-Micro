using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ab_order_detail", Schema = "PrintBook")]
public class AbOrderDetail
{
    [Key] public long Id { get; set; }
    public long?    Order_Id      { get; set; }
    public long?    Bibid         { get; set; }
    public int?     Amount        { get; set; }
    public double?  Price         { get; set; }
    public string?  CURRENCY      { get; set; }
    public double?  Rate          { get; set; }
    public string?  Cancel_Reson  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

