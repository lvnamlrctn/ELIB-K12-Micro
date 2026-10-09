using Elib.BuildingBlocks.Domain;
using Elib.Notification.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Notification.Application;

/// <summary>Cấu hình hiện tại. <see cref="Configured"/> = false: đơn vị đang dùng SMTP của nền tảng.</summary>
public sealed record EmailSettingsDto(
    bool Configured,
    string? Host,
    int? Port,
    SmtpSecurity? Security,
    string? UserName,
    bool HasPassword,
    string? FromAddress,
    string? FromName,
    int? Status,
    bool PlatformAvailable);

/// <summary><see cref="Password"/> rỗng = giữ mật khẩu cũ; <see cref="ClearPassword"/> = xoá mật khẩu.</summary>
public sealed record EmailSettingsRequest(
    string Host,
    int Port,
    SmtpSecurity Security,
    string? UserName,
    string? Password,
    bool ClearPassword,
    string FromAddress,
    string? FromName,
    int? Status);

public sealed record TestEmailRequest(string To);

/// <summary>SMTP riêng của đơn vị (monolith: NotificationChannelConfigs kênh email).</summary>
public sealed class EmailSettingsService(
    INotificationDb db,
    ISecretProtector secrets,
    SmtpHostPolicy hostPolicy,
    EmailDispatcher dispatcher,
    IOptions<NotificationOptions> options)
{
    public async Task<EmailSettingsDto> GetAsync(CancellationToken ct)
    {
        var s = await db.EmailSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var platform = options.Value.Smtp.IsConfigured;
        return s is null
            ? new EmailSettingsDto(false, null, null, null, null, false, null, null, null, platform)
            : new EmailSettingsDto(true, s.Host, s.Port, s.Security, s.UserName, s.ProtectedPassword is not null,
                s.FromAddress, s.FromName, s.Status, platform);
    }

    public async Task<EmailSettingsDto> SaveAsync(EmailSettingsRequest request, CancellationToken ct)
    {
        var settings = await db.EmailSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = EmailSettings.Create(request.Host, request.Port, request.Security, request.UserName,
                request.FromAddress, request.FromName, request.Status);
            db.EmailSettings.Add(settings);
        }
        else
        {
            settings.Update(request.Host, request.Port, request.Security, request.UserName,
                request.FromAddress, request.FromName, request.Status);
        }

        await hostPolicy.EnsureAllowedAsync(settings.Host, ct);

        if (request.ClearPassword) settings.SetProtectedPassword(null);
        else if (!string.IsNullOrEmpty(request.Password))
        {
            if (request.Password.Length > 500) throw new BusinessRuleException("SMTP_PASSWORD_INVALID", "Mật khẩu SMTP tối đa 500 ký tự.");
            settings.SetProtectedPassword(secrets.Protect(request.Password));
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    /// <summary>Bỏ SMTP riêng — quay về SMTP của nền tảng.</summary>
    public async Task DeleteAsync(CancellationToken ct)
    {
        var settings = await db.EmailSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return;
        db.EmailSettings.Remove(settings); // interceptor đổi thành xoá mềm
        await db.SaveChangesAsync(ct);
    }

    public async Task<NotificationLogDto> SendTestAsync(TestEmailRequest request, CancellationToken ct)
    {
        if (EmailRules.Normalize(request.To) is null)
            throw new BusinessRuleException("EMAIL_INVALID", "Địa chỉ nhận thư kiểm tra không hợp lệ.");
        var log = await dispatcher.SendAsync(
            new EmailRequest(DefaultTemplates.TestEmail, request.To, null, new Dictionary<string, string>()), ct);
        return NotificationLogDto.From(log);
    }
}
