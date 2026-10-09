using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ELIBAPI.Core.Entities.Ebook;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Bib", Schema = "PrintBook")]
public class Bib
{
    [Key] public long    Bibid          { get; set; }
    public long?    Mfn              { get; set; }
    public DateTime? CreatedTime     { get; set; }
    public DateTime? UpdateTime      { get; set; }
    public long?    CreatedBy        { get; set; }
    public long?    UpdateBy         { get; set; }
    public string?  Status           { get; set; }
    public long?    Bib_worksheet_id { get; set; }
    public long?    Bib_type_id      { get; set; }
    public string?  MARC_STATUS      { get; set; }
    public string?  Url              { get; set; }
    public string?  Images           { get; set; }
    public long?    EbookId          { get; set; }
    public long?    DocNum           { get; set; }
    public long?    CollectionId     { get; set; }
    public int?     IsDelete         { get; set; }
    public long?    CreatedRowBy     { get; set; }
    public long?    UpdateRowBy      { get; set; }
    public DateTime? CreatedRowDate  { get; set; }
    public DateTime? UpdatedRowDate  { get; set; }
    public long?    TenantId     { get; set; }
    public Guid     PublicId         { get; set; }
    [NotMapped] public string? TenantName { get; set; }

    [ForeignKey("CollectionId")] public virtual EbookCollection? Collection { get; set; }
    [NotMapped] public string? CollectionName => Collection?.Name;
}

