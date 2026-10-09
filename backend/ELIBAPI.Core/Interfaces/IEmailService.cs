namespace ELIBAPI.Core.Interfaces;

public interface IEmailService
{
    /// Gửi 1 email. Nếu Smtp:Host chưa cấu hình, implementation chỉ log cảnh báo và bỏ qua (không throw).
    Task SendAsync(string toEmail, string subject, string htmlBody,
        (string FileName, byte[] Content)? attachment = null, CancellationToken ct = default);
}
