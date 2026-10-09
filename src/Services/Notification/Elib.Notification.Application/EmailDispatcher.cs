using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Notification.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Notification.Application;

public sealed record EmailRequest(
    string TemplateCode,
    string? ToAddress,
    string? ToName,
    IReadOnlyDictionary<string, string> Data,
    string? DeduplicationKey = null,
    Guid? EventId = null);

/// <summary>
/// Gửi một email theo mẫu cho đơn vị hiện tại và ghi nhật ký. Chọn máy chủ: SMTP riêng của đơn vị (đang bật) → SMTP nền tảng.
/// Không ném lỗi gửi ra ngoài — kết quả (kể cả thất bại) nằm trong bản ghi nhật ký trả về.
/// </summary>
public sealed class EmailDispatcher(
    INotificationDb db,
    ITenantContext tenant,
    ISmtpSender sender,
    ISecretProtector secrets,
    SmtpHostPolicy hostPolicy,
    IOptions<NotificationOptions> options)
{
    public async Task<NotificationLog> SendAsync(EmailRequest request, CancellationToken ct)
    {
        var code = request.TemplateCode.Trim().ToUpperInvariant();
        if (request.DeduplicationKey is { Length: > 0 } key)
        {
            var sent = await db.NotificationLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.DeduplicationKey == key && l.Status == NotificationStatus.Sent, ct);
            if (sent is not null) return sent;
        }

        var to = EmailRules.Normalize(request.ToAddress);
        if (to is null)
            return await LogAsync(request, code, request.ToAddress, null, NotificationStatus.Skipped, "Người nhận không có địa chỉ email hợp lệ.", false, ct);

        var template = await ResolveTemplateAsync(code, ct);
        if (template is null)
            return await LogAsync(request, code, to, null, NotificationStatus.Failed, $"Không có mẫu email '{code}'.", false, ct);

        var data = new Dictionary<string, string>(request.Data, StringComparer.Ordinal);
        if (!data.ContainsKey("tenant_name"))
            data["tenant_name"] = await db.TenantNameAsync(tenant.RequireTenantId(), ct) ?? "";
        var subject = TemplateRenderer.RenderSubject(template.Value.Subject, data);
        var body = TemplateRenderer.RenderHtml(template.Value.Body, data);

        var (endpoint, viaPlatform, error) = await ResolveEndpointAsync(ct);
        if (endpoint is null)
            return await LogAsync(request, code, to, subject, NotificationStatus.Failed, error, viaPlatform, ct);

        try
        {
            await sender.SendAsync(endpoint, new OutgoingEmail(to, request.ToName, subject, body), ct);
        }
        catch (SmtpDeliveryException ex)
        {
            return await LogAsync(request, code, to, subject, NotificationStatus.Failed, ex.Message, viaPlatform, ct);
        }

        return await LogAsync(request, code, to, subject, NotificationStatus.Sent, null, viaPlatform, ct);
    }

    /// <summary>
    /// Thư cấp nền tảng (không thuộc đơn vị, ví dụ OTP của quản trị nền tảng): mẫu mặc định + SMTP nền tảng, không ghi DB.
    /// Trả về lỗi (null = đã gửi) để consumer ghi log.
    /// </summary>
    public async Task<string?> SendSystemAsync(string templateCode, string? toAddress, string? toName,
        IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        var to = EmailRules.Normalize(toAddress);
        if (to is null) return "Người nhận không có địa chỉ email hợp lệ.";
        if (DefaultTemplates.Find(templateCode.Trim().ToUpperInvariant()) is not { } template) return $"Không có mẫu email '{templateCode}'.";
        var platform = options.Value.Smtp;
        if (!platform.IsConfigured) return "Nền tảng chưa cấu hình SMTP.";

        var values = new Dictionary<string, string>(data, StringComparer.Ordinal);
        values.TryAdd("tenant_name", platform.FromName ?? "ELIB");
        try
        {
            await sender.SendAsync(platform.ToEndpoint(),
                new OutgoingEmail(to, toName, TemplateRenderer.RenderSubject(template.Subject, values), TemplateRenderer.RenderHtml(template.Body, values)), ct);
            return null;
        }
        catch (SmtpDeliveryException ex)
        {
            return ex.Message;
        }
    }

    private async Task<(string Subject, string Body)?> ResolveTemplateAsync(string code, CancellationToken ct)
    {
        var own = await db.EmailTemplates.AsNoTracking().Where(t => t.Code == code)
            .Select(t => new { t.Subject, t.Body, t.Status }).FirstOrDefaultAsync(ct);
        if (own is not null)
            return own.Status == IHasStatus.Active ? (own.Subject, own.Body) : null; // đơn vị tắt mẫu = không gửi
        return DefaultTemplates.Find(code) is { } d ? (d.Subject, d.Body) : null;
    }

    private async Task<(SmtpEndpoint? Endpoint, bool ViaPlatform, string? Error)> ResolveEndpointAsync(CancellationToken ct)
    {
        var own = await db.EmailSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (own is { Status: IHasStatus.Active })
        {
            try
            {
                await hostPolicy.EnsureAllowedAsync(own.Host, ct);
            }
            catch (BusinessRuleException ex)
            {
                return (null, false, ex.Message);
            }

            var password = own.ProtectedPassword is { } p ? secrets.Unprotect(p) : null;
            if (own.ProtectedPassword is not null && password is null)
                return (null, false, "Không giải mã được mật khẩu SMTP — nhập lại mật khẩu trong cấu hình email.");
            return (new SmtpEndpoint(own.Host, own.Port, own.Security, own.UserName, password, own.FromAddress, own.FromName), false, null);
        }

        var platform = options.Value.Smtp;
        return platform.IsConfigured
            ? (platform.ToEndpoint(), true, null)
            : (null, true, "Đơn vị chưa cấu hình SMTP và nền tảng không có SMTP mặc định.");
    }

    private async Task<NotificationLog> LogAsync(EmailRequest request, string code, string? to, string? subject,
        NotificationStatus status, string? error, bool viaPlatform, CancellationToken ct)
    {
        var log = NotificationLog.Email(code, to, subject, status, error, request.DeduplicationKey, request.EventId, viaPlatform);
        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log;
    }
}
