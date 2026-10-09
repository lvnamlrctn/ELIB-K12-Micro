using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("Review", Schema = "Ebook")]
public class EbookReview
{
    [Key] public long      Id          { get; set; }
    public long?     ItemId      { get; set; }
    public int?      Rating      { get; set; }
    public string?   DisplayName { get; set; }
    public string?   Content     { get; set; }
    public string?   Email       { get; set; }
    public int?      Status      { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}
