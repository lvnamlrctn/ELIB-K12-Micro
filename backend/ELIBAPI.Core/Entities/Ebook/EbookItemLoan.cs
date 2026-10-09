using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("ItemLoan", Schema = "Ebook")]
public class EbookItemLoan
{
    [Key] public long      Id           { get; set; }
    public long      EbookItemId  { get; set; }
    public long      ReaderId     { get; set; }
    public DateTime  CheckedOutAt { get; set; }
    public DateTime? ExpiresAt    { get; set; }   // CheckedOutAt + EbookItem.OfflineDays; null = không giới hạn
    public DateTime? LastAccessAt { get; set; }
    public int?      Status       { get; set; }   // 1 = Active, 2 = Recalled
    public DateTime? RecalledAt   { get; set; }
    public long?     RecalledBy   { get; set; }
    public string?   RecallReason { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }

    [NotMapped] public string? ReaderName    { get; set; }
    [NotMapped] public string? ReaderCardNo  { get; set; }
    [NotMapped] public string? EbookTitle    { get; set; }
    [NotMapped] public string? TenantName    { get; set; }
}
