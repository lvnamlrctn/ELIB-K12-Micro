using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("ab_deliverer_detail", Schema = "PrintBook")]
public class AbDelivererDetail
{
    [Key] public long Id { get; set; }
    public long? Deliverer_Id { get; set; }
    public long? BarcodeId    { get; set; }
    public int?  Store_Id     { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

