namespace ELIBAPI.Core.Interfaces;

// Điểm gọi chung cho mọi trigger SMS/Zalo ZNS (hold sách thành công, sách đến hạn/quá hạn, ebook sắp hết
// hạn/có sẵn để mượn, đạt huy hiệu, duyệt/từ chối đặt phòng học nhóm). Nhận tenantId tường minh vì phải
// chạy được từ Hangfire job (không có HttpContext) và vì NotificationChannelConfig lưu theo tenant.
// Không bao giờ throw ra ngoài — mọi lỗi được nuốt và ghi vào NotificationLog.
public interface INotificationDispatcher
{
    Task DispatchSmsAsync(long? tenantId, string? phone, string eventCode, Dictionary<string, string> tokens);
    Task DispatchZaloAsync(long? tenantId, string? phone, string eventCode, Dictionary<string, string> tokens);
}
