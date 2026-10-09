using System.Net.Mail;
using Elib.BuildingBlocks.Domain;

namespace Elib.Notification.Domain;

/// <summary>Kiểu mã hoá kết nối SMTP.</summary>
public enum SmtpSecurity
{
    /// <summary>Không mã hoá — chỉ dùng cho máy chủ mail nội bộ (Mailpit…).</summary>
    None = 0,

    /// <summary>Nâng cấp TLS sau khi kết nối (cổng 587).</summary>
    StartTls = 1,

    /// <summary>TLS ngay khi kết nối (cổng 465).</summary>
    SslOnConnect = 2,
}

/// <summary>
/// Máy chủ SMTP riêng của đơn vị — mỗi đơn vị tối đa một bản ghi. Không có (hoặc tắt) thì gửi bằng SMTP của nền tảng.
/// Mật khẩu chỉ lưu dạng đã mã hoá (Data Protection); API không bao giờ trả lại.
/// </summary>
public sealed class EmailSettings : TenantEntity, IHasStatus
{
    private EmailSettings() { }

    public string Host { get; private set; } = "";
    public int Port { get; private set; }
    public SmtpSecurity Security { get; private set; }
    public string? UserName { get; private set; }
    public string? ProtectedPassword { get; private set; }
    public string FromAddress { get; private set; } = "";
    public string? FromName { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static EmailSettings Create(string host, int port, SmtpSecurity security, string? userName, string fromAddress, string? fromName, int? status)
    {
        var settings = new EmailSettings();
        settings.Update(host, port, security, userName, fromAddress, fromName, status);
        return settings;
    }

    public void Update(string host, int port, SmtpSecurity security, string? userName, string fromAddress, string? fromName, int? status)
    {
        var h = (host ?? "").Trim();
        Host = h.Length is > 0 and <= 255 && !h.Contains(' ', StringComparison.Ordinal)
            ? h
            : throw new BusinessRuleException("SMTP_HOST_INVALID", "Máy chủ SMTP bắt buộc, tối đa 255 ký tự.");
        Port = port is > 0 and <= 65535 ? port : throw new BusinessRuleException("SMTP_PORT_INVALID", "Cổng SMTP từ 1 đến 65535.");
        Security = Enum.IsDefined(security) ? security : throw new BusinessRuleException("SMTP_SECURITY_INVALID", "Kiểu mã hoá không hợp lệ.");
        UserName = Optional(userName, 255, "SMTP_USERNAME_INVALID", "Tên đăng nhập SMTP");
        FromAddress = EmailRules.Normalize(fromAddress) ?? throw new BusinessRuleException("FROM_ADDRESS_INVALID", "Địa chỉ gửi không hợp lệ.");
        FromName = Optional(fromName, 200, "FROM_NAME_INVALID", "Tên người gửi");
        if (status is not null) ChangeStatus(status.Value);
    }

    /// <summary>Đặt mật khẩu đã mã hoá; null = xoá mật khẩu (máy chủ không cần đăng nhập).</summary>
    public void SetProtectedPassword(string? protectedPassword) =>
        ProtectedPassword = string.IsNullOrEmpty(protectedPassword) ? null : protectedPassword;

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    private static string? Optional(string? value, int max, string code, string label)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} tối đa {max} ký tự.");
    }
}

public static class EmailRules
{
    public const int MaxLength = 320;

    /// <summary>Địa chỉ email đơn (không kèm tên hiển thị) đã cắt khoảng trắng, hoặc null nếu không hợp lệ.</summary>
    public static string? Normalize(string? address)
    {
        var a = address?.Trim();
        if (string.IsNullOrEmpty(a) || a.Length > MaxLength) return null;
        return MailAddress.TryCreate(a, out var parsed) && parsed.Address == a && parsed.DisplayName.Length == 0 ? a : null;
    }
}
