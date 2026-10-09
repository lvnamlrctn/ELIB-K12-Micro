using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("EbookFavorite", Schema = "Ebook")]
public class EbookFavorite
{
    [Key] public long      Id             { get; set; }
    public long?     ReaderId       { get; set; }
    public string?   Cardnumber     { get; set; }
    public long?     ItemId         { get; set; }
    public long?     TenantId       { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public Guid      PublicId       { get; set; }
}
