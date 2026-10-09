namespace ELIBAPI.Core.Interfaces;

// Nhận credential tường minh (không tự đọc IConfiguration) vì NotificationChannelConfig lưu theo tenant
// trong DB — NotificationDispatcher là nơi duy nhất biết dùng credential của tenant nào. Trả tuple thay vì
// nuốt lỗi để NotificationDispatcher ghi đúng NotificationLog.Success/ErrorMessage.
public interface ISmsService
{
    Task<(bool Ok, string? Error)> SendAsync(
        string accessToken, string apiUrl, string? sender, string phone, string message,
        CancellationToken ct = default);
}
