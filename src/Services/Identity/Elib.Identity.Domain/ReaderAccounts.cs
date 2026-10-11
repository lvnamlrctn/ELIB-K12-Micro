using Elib.BuildingBlocks.Domain;

namespace Elib.Identity.Domain;

/// <summary>
/// Tài khoản đăng nhập OPAC của bạn đọc (monolith: cột Reader.Password). Hồ sơ — số thẻ, họ tên, email, trạng thái, hạn thẻ — là bản sao
/// từ <c>ReaderChanged</c> của patron (chỉ ghi khi Version lớn hơn); mật khẩu chỉ nằm ở identity, không đi qua event hay API nào.
/// </summary>
/// <remarks>
/// "Mật khẩu = số thẻ" (<see cref="CardNoPassword"/>) không băm sẵn: đặt hàng loạt cho cả trường không phải băm BCrypt từng người,
/// số thẻ đổi thì mật khẩu mặc định đổi theo; luôn kèm bắt đổi mật khẩu ở lần đăng nhập đầu.
/// </remarks>
public sealed class ReaderAccount : Entity, ITenantOwned
{
    public const int MinPasswordLength = 6;
    public const int MaxPasswordLength = 128;

    public long TenantId { get; set; }
    public Guid ReaderPublicId { get; set; }
    public string CardNo { get; private set; } = "";

    /// <summary>Số thẻ chuẩn hoá (trim, chữ hoa) để tra khi đăng nhập — số thẻ chỉ duy nhất trong đơn vị.</summary>
    public string CardKey { get; private set; } = "";

    public string FullName { get; private set; } = "";
    public string? Email { get; private set; }

    /// <summary>2 = hoạt động, 1 = thẻ bị khoá (quy ước patron).</summary>
    public int Status { get; private set; }

    public DateOnly? ExpireDate { get; private set; }
    public bool Deleted { get; private set; }
    public long Version { get; private set; } = -1;

    public bool CardNoPassword { get; private set; }
    public Credentials Credentials { get; private set; } = new();

    public bool HasPassword => CardNoPassword || Credentials.PasswordHash.Length > 0;

    public static string Key(string? cardNo) => (cardNo ?? "").Trim().ToUpperInvariant();

    /// <summary>Ghi hồ sơ theo event của patron; event cũ hơn bản đang có thì bỏ qua (trả false).</summary>
    public bool ApplyProfile(long version, string cardNo, string fullName, string? email, int status, DateOnly? expireDate, bool deleted)
    {
        if (version <= Version) return false;
        CardNo = Cut(cardNo.Trim(), 50);
        CardKey = Key(CardNo);
        FullName = Cut(fullName.Trim(), 250);
        Email = string.IsNullOrWhiteSpace(email) ? null : Cut(email.Trim(), 250);
        Status = status;
        ExpireDate = expireDate;
        Deleted = deleted;
        Version = version;
        return true;
    }

    /// <summary>Cán bộ đặt mật khẩu cho bạn đọc — bạn đọc phải đổi ở lần đăng nhập đầu.</summary>
    public void SetPassword(string hash)
    {
        CardNoPassword = false;
        Credentials.SetPasswordHash(hash, mustChange: true);
    }

    /// <summary>Mật khẩu mặc định = số thẻ (không phân biệt hoa/thường) — bạn đọc phải đổi ở lần đăng nhập đầu.</summary>
    public void UseCardNoPassword()
    {
        CardNoPassword = true;
        Credentials.ClearPassword(mustChange: true);
    }

    /// <summary>So mật khẩu (dùng cho đăng nhập OPAC). Không đổi trạng thái khoá — nơi gọi ghi lượt đúng/sai vào <see cref="Credentials"/>.</summary>
    public bool VerifyPassword(string password, Func<string, string, bool> verifyHash)
    {
        ArgumentNullException.ThrowIfNull(verifyHash);
        if (string.IsNullOrEmpty(password)) return false;
        if (CardNoPassword) return CardKey.Length > 0 && Key(password) == CardKey;
        return Credentials.PasswordHash.Length > 0 && verifyHash(password, Credentials.PasswordHash);
    }

    public static void EnsurePasswordRules(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength || password.Length > MaxPasswordLength || string.IsNullOrWhiteSpace(password))
            throw new BusinessRuleException("READER_PASSWORD_INVALID", $"Mật khẩu bạn đọc từ {MinPasswordLength} đến {MaxPasswordLength} ký tự.");
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
