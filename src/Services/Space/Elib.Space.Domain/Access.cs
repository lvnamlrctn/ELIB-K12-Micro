using Elib.BuildingBlocks.Domain;

namespace Elib.Space.Domain;

/// <summary>
/// Đầu đọc RFID tại cửa (monolith: map.AccessDevice) — xác thực bằng khoá API riêng (SHA-256 hash, không lưu khoá thô). Không
/// gắn JWT — thiết bị tự quản lý qua header X-Device-Code/X-Device-Key (xem AccessControlService). Khác bản gốc: thẻ quét được
/// coi là CardNo của bạn đọc (nền tảng chưa có khái niệm CardUid tách riêng khỏi CardNo).
/// </summary>
public sealed class AccessDevice : TenantEntity, IHasStatus
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 250;

    private AccessDevice() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";

    /// <summary>null = cửa chung (không gắn 1 không gian cụ thể).</summary>
    public long? SpaceId { get; private set; }

    public string ApiKeyHash { get; private set; } = "";
    public DateTimeOffset? LastSeenAt { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public static AccessDevice Create(string code, string name, long? spaceId, string apiKeyHash)
    {
        var device = new AccessDevice();
        device.Update(code, name, spaceId);
        device.SetApiKeyHash(apiKeyHash);
        return device;
    }

    public void Update(string code, string name, long? spaceId)
    {
        var c = (code ?? "").Trim();
        Code = c.Length is > 0 and <= MaxCodeLength ? c : throw new BusinessRuleException("CODE_INVALID", $"Mã thiết bị bắt buộc, tối đa {MaxCodeLength} ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= MaxNameLength ? n : throw new BusinessRuleException("NAME_INVALID", $"Tên thiết bị bắt buộc, tối đa {MaxNameLength} ký tự.");
        SpaceId = spaceId;
    }

    public void SetApiKeyHash(string hash) => ApiKeyHash = hash;

    public void Touch(DateTimeOffset now) => LastSeenAt = now;
}

/// <summary>Thẻ cán bộ mở cửa (monolith: map.AccessStaffCard) — luôn mở, không gắn lượt đặt phòng nào.</summary>
public sealed class AccessStaffCard : TenantEntity, IHasStatus
{
    public const int MaxCardUidLength = 64;
    public const int MaxLabelLength = 200;

    private AccessStaffCard() { }

    public string CardUid { get; private set; } = "";
    public string? Label { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    public static AccessStaffCard Create(string cardUid, string? label)
    {
        var card = new AccessStaffCard();
        card.Update(cardUid, label);
        return card;
    }

    public void Update(string cardUid, string? label)
    {
        var u = (cardUid ?? "").Trim();
        CardUid = u.Length is > 0 and <= MaxCardUidLength ? u : throw new BusinessRuleException("CARD_UID_INVALID", $"Mã thẻ bắt buộc, tối đa {MaxCardUidLength} ký tự.");
        var l = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        Label = l is null || l.Length <= MaxLabelLength ? l : throw new BusinessRuleException("LABEL_TOO_LONG", $"Nhãn tối đa {MaxLabelLength} ký tự.");
    }
}

/// <summary>Nhật ký quyết định mở/từ chối cửa (monolith: map.AccessScanLog) — bất biến, chỉ ghi, không sửa/xoá.</summary>
public sealed class AccessScanLog : TenantEntity
{
    public const int MaxReasonLength = 250;

    private AccessScanLog() { }

    public long DeviceId { get; private set; }
    public string CardUid { get; private set; } = "";
    public Guid? ReaderPublicId { get; private set; }
    public Guid? BookingPublicId { get; private set; }
    public bool Allowed { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTimeOffset ScannedAt { get; private set; }

    public static AccessScanLog Create(long deviceId, string cardUid, Guid? readerPublicId, Guid? bookingPublicId, bool allowed, string reason, DateTimeOffset scannedAt) => new()
    {
        DeviceId = deviceId, CardUid = cardUid, ReaderPublicId = readerPublicId, BookingPublicId = bookingPublicId, Allowed = allowed,
        Reason = reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength], ScannedAt = scannedAt,
    };
}

/// <summary>Lệnh mở cửa từ xa do thủ thư gửi (monolith: map.AccessCommand) — thiết bị poll <see cref="AccessControlService"/>, hết hạn sau 2 phút.</summary>
public sealed class AccessCommand : TenantEntity
{
    public const int LifetimeMinutes = 2;
    public const string Open = "OPEN";

    private AccessCommand() { }

    public long DeviceId { get; private set; }
    public string Command { get; private set; } = Open;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AckedAt { get; private set; }

    public bool IsPending(DateTimeOffset now) => AckedAt is null && ExpiresAt > now;

    public static AccessCommand Create(long deviceId, DateTimeOffset now) => new()
    {
        DeviceId = deviceId, Command = Open, ExpiresAt = now.AddMinutes(LifetimeMinutes),
    };

    public void Ack(DateTimeOffset now) => AckedAt = now;
}
