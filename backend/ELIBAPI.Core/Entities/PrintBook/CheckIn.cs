using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("CheckIn", Schema = "PrintBook")]
public class CheckIn
{
    [Key] public long Id { get; set; }
    public long?     Readerid     { get; set; }
    public long?     UserId       { get; set; }
    public DateTime? CheckInTime  { get; set; }
    public long?     StoreId      { get; set; }
    public long?     CircPlaceId  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

