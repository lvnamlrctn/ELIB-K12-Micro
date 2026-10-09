using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

// Lịch sử gửi SMS/Zalo ZNS — chỉ ghi bởi NotificationDispatcher, không có UI Add/Edit/Delete. Khác
// ELIB-LRC (chỉ ghi ILogger, không tra cứu được) — thêm bảng này để admin audit qua UI.
[Table("NotificationLog", Schema = "dbo")]
public class NotificationLog
{
    [Key] public long Id { get; set; }
    public string?  Channel      { get; set; } // "SMS" | "ZALO"
    public string?  Recipient    { get; set; }
    public string?  EventCode    { get; set; } // "BADGE_EARNED", "ROOM_BOOKING_APPROVED", ...
    public bool?    Success      { get; set; }
    public string?  ErrorMessage { get; set; }
    public DateTime SentAt       { get; set; }
    // Audit Trail
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public long?      TenantId      { get; set; }
    public Guid       PublicId      { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}
