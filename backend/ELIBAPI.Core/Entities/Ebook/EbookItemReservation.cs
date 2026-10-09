using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

// Hàng đợi đặt trước tài liệu số khi EbookItem.PrintCopies đã đạt giới hạn đồng thời. Khác
// PrintBook.BookRequest ở chỗ không giữ chỗ 1 bản sao vật lý cụ thể (không có Barcode tương đương)
// — chỉ giữ VỊ TRÍ HÀNG ĐỢI cho EbookItemId, được "thăng hạng" (Status=2 Ready) khi có slot trống
// và tới lượt; bạn đọc vẫn phải tự bấm Mượn (BorrowDigital) để thực sự tạo EbookItemLoan.
[Table("ItemReservation", Schema = "Ebook")]
public class EbookItemReservation
{
    [Key] public long      Id             { get; set; }
    public long      EbookItemId    { get; set; }
    public long      ReaderId       { get; set; }
    public DateTime  RequestedAt    { get; set; }
    public int       Status         { get; set; }   // 1=Pending, 2=Ready, 3=Fulfilled, 4=Cancelled, 5=Expired
    public DateTime? ReadyAt        { get; set; }
    public DateTime? ReadyExpiresAt { get; set; }    // hạn chót phải bấm Mượn trước khi mất lượt
    public DateTime? FulfilledAt    { get; set; }
    public DateTime? CancelledAt    { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId        { get; set; }
    public Guid     PublicId        { get; set; }

    [NotMapped] public string? ReaderName   { get; set; }
    [NotMapped] public string? ReaderCardNo { get; set; }
    [NotMapped] public string? EbookTitle   { get; set; }
    [NotMapped] public string? TenantName   { get; set; }
}
