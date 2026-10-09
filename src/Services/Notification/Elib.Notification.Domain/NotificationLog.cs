using Elib.BuildingBlocks.Domain;

namespace Elib.Notification.Domain;

public enum NotificationStatus
{
    Sent = 1,
    Failed = 2,

    /// <summary>Không gửi vì thiếu điều kiện (không có địa chỉ, không đúng kênh…).</summary>
    Skipped = 3,
}

/// <summary>
/// Nhật ký mỗi lần gửi tin (thay NotificationLogs của monolith). KHÔNG lưu nội dung thư — nội dung có thể chứa mã OTP.
/// </summary>
public sealed class NotificationLog : TenantEntity
{
    private NotificationLog() { }

    public Guid? EventId { get; private set; }
    public string Channel { get; private set; } = "email";
    public string TemplateCode { get; private set; } = "";
    public string? Recipient { get; private set; }
    public string? Subject { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string? Error { get; private set; }
    public string? DeduplicationKey { get; private set; }

    /// <summary>Gửi qua SMTP của nền tảng (đơn vị chưa cấu hình SMTP riêng).</summary>
    public bool ViaPlatform { get; private set; }

    public static NotificationLog Email(string templateCode, string? recipient, string? subject, NotificationStatus status,
        string? error, string? deduplicationKey, Guid? eventId, bool viaPlatform) => new()
    {
        EventId = eventId,
        TemplateCode = Cut(templateCode, 50) ?? "",
        Recipient = Cut(recipient, EmailRules.MaxLength),
        Subject = Cut(subject, 300),
        Status = status,
        Error = Cut(error, 1000),
        DeduplicationKey = Cut(deduplicationKey, 200),
        ViaPlatform = viaPlatform,
    };

    private static string? Cut(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
