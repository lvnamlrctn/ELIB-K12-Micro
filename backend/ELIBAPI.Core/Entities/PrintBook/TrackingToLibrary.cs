using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("trackingtolibrary", Schema = "PrintBook")]
public class TrackingToLibrary
{
    [Key] public long Id { get; set; }
    public long?     Readerid { get; set; }
    public long?     UserId   { get; set; }
    public DateTime? Time     { get; set; }
    public long?     StoreId  { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

