using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("CheckOut", Schema = "PrintBook")]
public class CheckOut
{
    [Key] public long Id { get; set; }
    public long?     ReaderId     { get; set; }
    public long?     UserId       { get; set; }
    public DateTime? CheckInTime  { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public long?     StoreId      { get; set; }
    public long?     CircPlaceId  { get; set; }
    public long?     CheckInId    { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

