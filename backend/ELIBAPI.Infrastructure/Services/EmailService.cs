using System.Net;
using System.Net.Mail;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

// Dùng System.Net.Mail (BCL, không cần gói ngoài) thay vì MailKit — MailKit kéo theo
// BouncyCastle.Cryptography, có namespace gốc "Org" xung đột với entity Dbo.Org trong dự án
// (CS0118/CS0576), không thể alias vòng quanh trong 1 using-directive.
public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody,
        (string FileName, byte[] Content)? attachment = null, CancellationToken ct = default)
    {
        var s = config.GetSection("Smtp");
        var host = s["Host"] ?? "";
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("Chưa cấu hình Smtp:Host — bỏ qua gửi email tới {ToEmail} (subject: {Subject})", toEmail, subject);
            return;
        }
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            logger.LogWarning("Không có địa chỉ email người nhận — bỏ qua gửi (subject: {Subject})", subject);
            return;
        }

        var port = int.TryParse(s["Port"], out var p) ? p : 587;
        var user = s["User"] ?? "";
        var password = s["Password"] ?? "";
        if (password.StartsWith("ENC:")) password = AesEncryptionHelper.Decrypt(password[4..]);
        var from = s["From"] ?? user;
        var fromName = s["FromName"] ?? "ELIB";
        var useStartTls = !bool.TryParse(s["UseStartTls"], out var tls) || tls;

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(toEmail);

        using var attachmentStream = attachment.HasValue ? new MemoryStream(attachment.Value.Content) : null;
        if (attachment.HasValue)
            message.Attachments.Add(new Attachment(attachmentStream!, attachment.Value.FileName));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useStartTls,
            Credentials = string.IsNullOrWhiteSpace(user) ? null : new NetworkCredential(user, password),
        };
        try
        {
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gửi email tới {ToEmail} thất bại (subject: {Subject})", toEmail, subject);
            throw;
        }
    }
}
