using System.Text.Json;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

// Cổng SMS SpeedSMS (speedsms.vn/sms-api-service): GET access-token/to/content/type/sender qua query
// string. Field chính xác có thể lệch nhẹ so với tài liệu khi test với access-token thật.
// Khác ELIB-LRC: nhận credential tường minh (đọc từ NotificationChannelConfig theo tenant, không tự đọc
// IConfiguration) và trả (Ok, Error) thay vì tự nuốt lỗi — để NotificationDispatcher ghi đúng NotificationLog.
public class SmsService(IHttpClientFactory httpClientFactory, ILogger<SmsService> logger) : ISmsService
{
    public async Task<(bool Ok, string? Error)> SendAsync(
        string accessToken, string apiUrl, string? sender, string phone, string message,
        CancellationToken ct = default)
    {
        var normalizedPhone = NormalizePhone(phone);
        var query = $"?access-token={Uri.EscapeDataString(accessToken)}" +
                    $"&to={Uri.EscapeDataString(normalizedPhone)}" +
                    $"&content={Uri.EscapeDataString(message)}" +
                    "&type=3" +
                    (string.IsNullOrWhiteSpace(sender) ? "" : $"&sender={Uri.EscapeDataString(sender)}");

        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(apiUrl + query, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(body);
            var status = json.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
            if (status == "success") return (true, null);
            logger.LogWarning("SmsService: gửi SMS tới {Phone} thất bại — phản hồi: {Body}", phone, body);
            return (false, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SmsService: gửi SMS tới {Phone} thất bại", phone);
            return (false, ex.Message);
        }
    }

    // SpeedSMS cần số dạng quốc tế không dấu "+": đổi "0xxxxxxxxx" (VN) -> "84xxxxxxxxx", bỏ khoảng
    // trắng/gạch ngang. Xác nhận lại đúng định dạng khi test với access-token thật.
    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith('0')) digits = "84" + digits[1..];
        return digits;
    }
}
