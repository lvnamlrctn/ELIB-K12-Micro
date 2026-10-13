using Elib.BuildingBlocks.Domain;

namespace Elib.Circulation.Domain;

/// <summary>
/// Trạm tự phục vụ / máy mượn trả tự động nói chuyện bằng SIP2 (3M SIP 2.00), quyền SIP2LOG. Trạm đăng nhập bằng lệnh 93 với
/// <see cref="LoginId"/> + mật khẩu — tài khoản đăng nhập duy nhất trên toàn nền tảng (một cổng SIP2 cho mọi đơn vị), từ đó biết đơn vị và
/// điểm lưu thông của mọi giao dịch. <see cref="RequirePatronPassword"/>: bắt bạn đọc nhập mật khẩu OPAC (trường AD) mới mượn/gia hạn được.
/// <see cref="AsciiOnly"/>: bỏ dấu tiếng Việt trong thông điệp trả về cho máy không hiển thị được UTF-8.
/// </summary>
public sealed class Sip2Station : TenantEntity
{
    public const int MaxLoginLength = 50;

    private Sip2Station() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string LoginId { get; private set; } = "";

    /// <summary>Tài khoản đăng nhập chuẩn hoá (chữ hoa) — duy nhất toàn nền tảng.</summary>
    public string LoginKey { get; private set; } = "";

    public string PasswordHash { get; private set; } = "";
    public long CircPlaceId { get; private set; }
    public bool RequirePatronPassword { get; private set; }
    public bool AsciiOnly { get; private set; }
    public bool Enabled { get; private set; } = true;
    public DateTimeOffset? LastSeenAt { get; private set; }

    public static Sip2Station Create(string code, string name, string loginId, long circPlaceId, bool requirePatronPassword, bool asciiOnly, bool enabled)
    {
        var station = new Sip2Station();
        station.Update(code, name, loginId, circPlaceId, requirePatronPassword, asciiOnly, enabled);
        return station;
    }

    public void Update(string code, string name, string loginId, long circPlaceId, bool requirePatronPassword, bool asciiOnly, bool enabled)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= 20 ? c : throw new BusinessRuleException("CODE_INVALID", "Mã trạm bắt buộc, tối đa 20 ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 250 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên trạm bắt buộc, tối đa 250 ký tự.");
        var login = (loginId ?? "").Trim();
        LoginId = login.Length is >= 3 and <= MaxLoginLength && login.All(ch => ch is > ' ' and < (char)127 and not '|')
            ? login
            : throw new BusinessRuleException("SIP2_LOGIN_INVALID", $"Tài khoản đăng nhập của trạm từ 3 đến {MaxLoginLength} ký tự ASCII, không dấu cách, không có '|'.");
        LoginKey = Key(login);
        CircPlaceId = circPlaceId > 0 ? circPlaceId : throw new BusinessRuleException("CIRC_PLACE_REQUIRED", "Chọn điểm lưu thông của trạm.");
        RequirePatronPassword = requirePatronPassword;
        AsciiOnly = asciiOnly;
        Enabled = enabled;
    }

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    public void Seen(DateTimeOffset at) => LastSeenAt = at;

    public static string Key(string? loginId) => (loginId ?? "").Trim().ToUpperInvariant();
}

/// <summary>Nhật ký giao dịch SIP2 (monolith: dbo.Sip2Log) — mật khẩu trạm/bạn đọc (CO, AD) đã che trước khi ghi.</summary>
public sealed class Sip2Log : Entity, ITenantOwned
{
    public const int MaxMessageLength = 4000;

    public long TenantId { get; set; }
    public long StationId { get; set; }
    public string Command { get; set; } = "";
    public string Request { get; set; } = "";
    public string Response { get; set; } = "";
    public bool Ok { get; set; }
    public int ElapsedMs { get; set; }
    public string? RemoteIp { get; set; }
    public DateTimeOffset At { get; set; }
}
