namespace Elib.Contracts.Events.Platform;

/// <summary>Yêu cầu service notification gửi tin theo template (docs 03 §3.2).</summary>
public sealed record NotificationRequested : IntegrationEvent
{
    public required string TemplateCode { get; init; }

    /// <summary>email | sms | zalo — rỗng = theo cấu hình kênh của đơn vị.</summary>
    public IReadOnlyList<string> Channels { get; init; } = [];

    public required NotificationRecipient Recipient { get; init; }
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();

    /// <summary>Khoá chống gửi trùng phía nghiệp vụ, ví dụ "loan-due:{loanId}:{date}".</summary>
    public string? DeduplicationKey { get; init; }
}

public sealed record NotificationRecipient(long? ReaderId, long? UserId, string? Email, string? Phone, string? DisplayName);

/// <summary>
/// Gửi tin cấp nền tảng, không thuộc đơn vị nào — ví dụ mã OTP của tài khoản quản trị nền tảng.
/// Chỉ email, mẫu mặc định của nền tảng, SMTP của nền tảng; không ghi nhật ký gửi tin của đơn vị.
/// </summary>
public sealed record SystemNotificationRequested : SystemEvent
{
    public required string TemplateCode { get; init; }
    public required NotificationRecipient Recipient { get; init; }
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
}

/// <summary>Nhật ký thao tác gửi tới service audit (thay UserLog/EntityHistory ghi trực tiếp).</summary>
public sealed record AuditRecorded : IntegrationEvent
{
    public required string Service { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? Summary { get; init; }

    /// <summary>Diff rút gọn dạng JSON — KHÔNG chứa mật khẩu, token, số CCCD.</summary>
    public string? ChangesJson { get; init; }

    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }

    /// <summary>Tên hiển thị của người thực hiện lúc ghi (audit không tra ngược identity).</summary>
    public string? ActorName { get; init; }
}

/// <summary>
/// Nhật ký cấp nền tảng (không thuộc đơn vị): quản trị nền tảng đăng nhập, tạo/khoá đơn vị, bán phân hệ…
/// <see cref="TenantId"/> = đơn vị bị tác động (nếu có), chỉ để tra cứu — không đặt ngữ cảnh đơn vị.
/// </summary>
public sealed record SystemAuditRecorded : SystemEvent
{
    public required string Service { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public string? EntityId { get; init; }
    public long? TenantId { get; init; }
    public string? Summary { get; init; }
    public string? IpAddress { get; init; }
    public string? ActorName { get; init; }
}
