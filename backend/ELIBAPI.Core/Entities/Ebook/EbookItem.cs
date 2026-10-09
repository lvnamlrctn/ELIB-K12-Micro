using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("Item", Schema = "Ebook")]
public class EbookItem
{
    [Key] public long      Id            { get; set; }
    public DateTime? Submited      { get; set; }
    public long?     CreatedBy     { get; set; }
    public long?     UpdateBy      { get; set; }
    public long?     CollectionId  { get; set; }
    public string?   Images        { get; set; }
    public int?      TotalView     { get; set; }
    public int?      TotalDownload { get; set; }
    public byte?     Status        { get; set; }
    public long?     SubjectId     { get; set; }
    public DateTime? LastUpdate    { get; set; }
    public long?     TypeId        { get; set; }
    public int?      AllowDownload { get; set; }
    public int?      Free          { get; set; }
    public long?     TopicId       { get; set; }
    public string?   PortalId      { get; set; }
    public string?   Language      { get; set; }
    public int?      Show          { get; set; }
    public int?      IndexContent  { get; set; }
    public int?      Share         { get; set; }
    public int?      FileVersion  { get; set; }
    public DateTime? IndexedAt    { get; set; }
    public string?   ErrorMessage { get; set; }
    public int?      PrintCopies  { get; set; }   // Số bản in — số bản đọc/tải đồng thời tối đa; null = không giới hạn
    public int?      OfflineDays  { get; set; }   // Số ngày Offline — thời hạn giữ quyền truy cập sau khi mượn; null = không giới hạn
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }

    [ForeignKey("Id")]
    public virtual EbookItemXml? ItemXml { get; set; }

    [ForeignKey("CollectionId")]
    public virtual EbookCollection? Collection { get; set; }

    [ForeignKey("SubjectId")]
    public virtual EbookSubject? Subject { get; set; }

    [ForeignKey("TopicId")]
    public virtual EbookTopic? Topic { get; set; }

    [ForeignKey("EbookId")]
    public virtual ICollection<EbookFile> Files { get; set; } = [];

    [NotMapped] public string? CollectionName => Collection?.Name;
    [NotMapped] public string? SubjectName    => Subject?.Name;
    [NotMapped] public string? TopicName      => Topic?.Name;
    [NotMapped] public int     TotalFile      => Files.Count(f => f.IsDelete != 2);
    [NotMapped] public string? TenantName { get; set; }
}

