using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("EbookLog", Schema = "Ebook")]
public class EbookLog
{
    [Key] public long      Id         { get; set; }
    public long?     ReaderId   { get; set; }
    public string?   Cardnumber { get; set; }
    public DateTime? Submited   { get; set; }
    public long?     Bookid     { get; set; }
    public int?      Page       { get; set; }
    public long?     Size       { get; set; }
    public int?      Type       { get; set; }
    public string?   Ip         { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

