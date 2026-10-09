using System.Text;
using System.Text.Json;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

// Zalo Notification Service (ZNS): POST {phone, template_id, template_data}, header "access_token".
// Access token lấy qua luồng OAuth riêng (App ID + Secret Key), hết hạn theo thời gian — chưa có Official
// Account + template được duyệt thật để xây refresh tự động ở vòng này, admin tự cập nhật access-token
// theo chu kỳ qua màn cấu hình SMS/Zalo. Khác ELIB-LRC: nhận credential tường minh, trả (Ok, Error).
public class ZaloZnsService(IHttpClientFactory httpClientFactory, ILogger<ZaloZnsService> logger) : IZaloZnsService
{
    public async Task<(bool Ok, string? Error)> SendAsync(
        string accessToken, string apiUrl, string phone, string templateId,
        Dictionary<string, string> templateData, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("access_token", accessToken);
            var payload = JsonSerializer.Serialize(new
            {
                phone,
                template_id = templateId,
                template_data = templateData
            });
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(body);
            var error = json.RootElement.TryGetProperty("error", out var e) ? e.GetInt32() : -1;
            if (error == 0) return (true, null);
            logger.LogWarning("ZaloZnsService: gửi ZNS tới {Phone} thất bại — phản hồi: {Body}", phone, body);
            return (false, body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ZaloZnsService: gửi ZNS tới {Phone} thất bại", phone);
            return (false, ex.Message);
        }
    }
}
