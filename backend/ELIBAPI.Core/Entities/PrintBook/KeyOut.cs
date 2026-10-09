using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("KeyOut", Schema = "PrintBook")]
public class KeyOut
{
    [Key] public long Id { get; set; }
    public long?     Keyid      { get; set; }
    public long?     Readerid   { get; set; }
    public DateTime? BorrowDate { get; set; }
    public long?     Userid     { get; set; }
    public long?     CircPlaceId { get; set; }
    public string?   Note       { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
}

