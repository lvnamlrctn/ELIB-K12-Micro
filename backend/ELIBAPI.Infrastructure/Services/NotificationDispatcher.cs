using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

// Điều phối gửi SMS/Zalo ZNS cho 8 sự kiện dùng chung (xem INotificationDispatcher). Thứ tự xử lý: phone
// rỗng -> bỏ qua; cờ tắt (mặc định) -> bỏ qua im lặng; chưa cấu hình nội dung mẫu/Template ID -> bỏ qua;
// chưa cấu hình access-token cho tenant -> log cảnh báo + bỏ qua (không throw, đúng quy ước "config rỗng
// = bỏ qua" của EmailService). Luôn ghi 1 dòng NotificationLog best-effort sau mỗi lần thử gửi thật.
public class NotificationDispatcher(
    ELIBAPIDbContext db,
    ISystemParameterService sysParam,
    ISmsService smsService,
    IZaloZnsService zaloService,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    private const string DefaultSmsApiUrl  = "https://api.speedsms.vn/index.php/sms/send";
    private const string DefaultZaloApiUrl = "https://business.openapi.zalo.me/message/template";

    public async Task DispatchSmsAsync(long? tenantId, string? phone, string eventCode, Dictionary<string, string> tokens)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        if (!await sysParam.IsEnabledAsync("SMS_NOTIFICATIONS_ENABLED", tenantId)) return;

        var template = await sysParam.GetValueAsync($"SMS_{eventCode}", tenantId);
        if (string.IsNullOrWhiteSpace(template)) return;

        var config = await GetConfigAsync(tenantId);
        if (config == null || string.IsNullOrWhiteSpace(config.SmsAccessToken))
        {
            logger.LogWarning("NotificationDispatcher: chưa cấu hình SmsAccessToken cho tenant {TenantId} — bỏ qua gửi SMS ({EventCode})", tenantId, eventCode);
            return;
        }

        var accessToken = Decrypt(config.SmsAccessToken);
        var apiUrl      = string.IsNullOrWhiteSpace(config.SmsApiUrl) ? DefaultSmsApiUrl : config.SmsApiUrl;
        var message     = RenderTemplate(template, tokens);

        (bool Ok, string? Error) result;
        try { result = await smsService.SendAsync(accessToken, apiUrl, config.SmsSender, phone, message); }
        catch (Exception ex) { result = (false, ex.Message); }

        await WriteLogAsync(tenantId, "SMS", phone, eventCode, result.Ok, result.Error);
    }

    public async Task DispatchZaloAsync(long? tenantId, string? phone, string eventCode, Dictionary<string, string> tokens)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        if (!await sysParam.IsEnabledAsync("ZALO_NOTIFICATIONS_ENABLED", tenantId)) return;

        var templateId = await sysParam.GetValueAsync($"ZALO_TEMPLATE_{eventCode}", tenantId);
        if (string.IsNullOrWhiteSpace(templateId)) return;

        var config = await GetConfigAsync(tenantId);
        if (config == null || string.IsNullOrWhiteSpace(config.ZaloAccessToken))
        {
            logger.LogWarning("NotificationDispatcher: chưa cấu hình ZaloAccessToken cho tenant {TenantId} — bỏ qua gửi ZNS ({EventCode})", tenantId, eventCode);
            return;
        }

        var accessToken = Decrypt(config.ZaloAccessToken);
        var apiUrl      = string.IsNullOrWhiteSpace(config.ZaloApiUrl) ? DefaultZaloApiUrl : config.ZaloApiUrl;

        (bool Ok, string? Error) result;
        try { result = await zaloService.SendAsync(accessToken, apiUrl, phone, templateId, tokens); }
        catch (Exception ex) { result = (false, ex.Message); }

        await WriteLogAsync(tenantId, "ZALO", phone, eventCode, result.Ok, result.Error);
    }

    // Ưu tiên cấu hình đúng tenant, fallback dòng TenantId=null (cấu hình mặc định toàn hệ thống) —
    // đúng pattern SystemParameterService.GetValueAsync.
    private async Task<NotificationChannelConfig?> GetConfigAsync(long? tenantId)
    {
        return await db.NotificationChannelConfigs
            .Where(c => c.IsDelete != 2 && (c.TenantId == tenantId || c.TenantId == null))
            .OrderByDescending(c => c.TenantId != null)
            .FirstOrDefaultAsync();
    }

    private static string Decrypt(string value) => value.StartsWith("ENC:") ? AesEncryptionHelper.Decrypt(value[4..]) : value;

    private static string RenderTemplate(string template, Dictionary<string, string> tokens)
    {
        foreach (var kv in tokens) template = template.Replace("{" + kv.Key + "}", kv.Value);
        return template;
    }

    private async Task WriteLogAsync(long? tenantId, string channel, string recipient, string eventCode, bool success, string? error)
    {
        try
        {
            db.NotificationLogs.Add(new NotificationLog
            {
                PublicId       = Guid.NewGuid(),
                Channel        = channel,
                Recipient      = recipient,
                EventCode      = eventCode,
                Success        = success,
                ErrorMessage   = error,
                SentAt         = DateTime.Now,
                TenantId       = tenantId,
                CreatedRowDate = DateTime.Now,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NotificationDispatcher: ghi NotificationLog thất bại ({Channel}/{EventCode})", channel, eventCode);
        }
    }
}
