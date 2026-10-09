using Elib.Notification.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Notification.Application;

/// <summary>Cổng dữ liệu của service — Infrastructure (NotificationDbContext) cài đặt.</summary>
public interface INotificationDb
{
    DbSet<EmailSettings> EmailSettings { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<NotificationLog> NotificationLogs { get; }

    /// <summary>Tên đơn vị từ bản sao (biến <c>tenant_name</c> trong mẫu); null nếu chưa có bản sao.</summary>
    Task<string?> TenantNameAsync(long tenantId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Một máy chủ SMTP đã giải mã mật khẩu — chỉ tồn tại trong bộ nhớ lúc gửi.</summary>
public sealed record SmtpEndpoint(string Host, int Port, SmtpSecurity Security, string? UserName, string? Password,
    string FromAddress, string? FromName);

public sealed record OutgoingEmail(string ToAddress, string? ToName, string Subject, string HtmlBody);

/// <summary>Gửi thư qua SMTP (Infrastructure: MailKit). Lỗi kết nối/xác thực ném <see cref="SmtpDeliveryException"/>.</summary>
public interface ISmtpSender
{
    Task SendAsync(SmtpEndpoint endpoint, OutgoingEmail email, CancellationToken cancellationToken);
}

public sealed class SmtpDeliveryException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Mã hoá mật khẩu SMTP lưu trong DB (Infrastructure: ASP.NET Data Protection, khoá lưu trong DB của service).</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>Null nếu không giải mã được (khoá đã mất/xoay) — coi như chưa có mật khẩu.</summary>
    string? Unprotect(string protectedValue);
}

public sealed class NotificationOptions
{
    public const string SectionName = "Notification";

    /// <summary>SMTP của nền tảng — dùng khi đơn vị chưa cấu hình SMTP riêng. Host rỗng = không có.</summary>
    public PlatformSmtpOptions Smtp { get; set; } = new();

    /// <summary>
    /// Cho đơn vị đặt SMTP trỏ vào địa chỉ nội bộ (loopback, 10.x, 192.168.x…). Mặc định TẮT: ở SaaS, admin đơn vị
    /// không được dùng nút "Gửi thử" để dò mạng nội bộ của nền tảng. Bật cho cụm triển khai riêng theo tỉnh.
    /// </summary>
    public bool AllowPrivateSmtpHosts { get; set; }

    /// <summary>Thời gian chờ kết nối/gửi một thư.</summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(20);
}

public sealed class PlatformSmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "";
    public string? FromName { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);

    public SmtpEndpoint ToEndpoint() => new(Host, Port, Security, UserName, Password, FromAddress, FromName);
}
