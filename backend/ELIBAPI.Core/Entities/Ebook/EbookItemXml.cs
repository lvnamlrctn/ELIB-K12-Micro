using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("itemXml", Schema = "Ebook")]
public class EbookItemXml
{
    [Key] public long    Id          { get; set; }
    public string? Title      { get; set; }
    public string? Author     { get; set; }
    public string? Publisher  { get; set; }
    public string? PublishDate { get; set; }
    public string? Keyword    { get; set; }
    public string? Xml        { get; set; }
    public string? OtherTitle { get; set; }
    public string? Page       { get; set; }
    public string? OldAuthor  { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

