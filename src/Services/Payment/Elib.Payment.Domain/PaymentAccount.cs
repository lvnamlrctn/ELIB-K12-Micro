using System.Globalization;
using Elib.BuildingBlocks.Domain;

namespace Elib.Payment.Domain;

public static class PaymentProviders
{
    /// <summary>Chuyển khoản mã QR VietQR — xác nhận tiền về qua webhook Sepay.</summary>
    public const string VietQr = "VietQR";

    public const string VnPay = "VNPAY";

    public static readonly string[] All = [VietQr, VnPay];

    public static string Normalize(string? provider) =>
        All.FirstOrDefault(p => string.Equals(p, provider?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new BusinessRuleException("PROVIDER_INVALID", "Cổng thanh toán phải là VietQR hoặc VNPAY.");
}

/// <summary>
/// Tài khoản nhận tiền của đơn vị — tối đa một bản ghi mỗi đơn vị. Bí mật (khoá webhook Sepay, HashSecret VNPAY) chỉ lưu dạng đã mã hoá
/// (Data Protection); API không bao giờ trả lại.
/// </summary>
public sealed class PaymentAccount : TenantEntity
{
    public const int MinExpireMinutes = 5;
    public const int MaxExpireMinutes = 60;
    public const int DefaultExpireMinutes = 15;
    public const string SandboxVnPayUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    public bool VietQrEnabled { get; private set; }

    /// <summary>Mã BIN ngân hàng (6 chữ số theo Napas).</summary>
    public string? BankBin { get; private set; }

    public string? AccountNo { get; private set; }
    public string? AccountName { get; private set; }

    /// <summary>Khoá Sepay gọi webhook xác nhận tiền về tài khoản VietQR — đã mã hoá.</summary>
    public string? ProtectedSepayApiKey { get; private set; }

    public bool VnPayEnabled { get; private set; }
    public string? VnPayTmnCode { get; private set; }
    public string? ProtectedVnPayHashSecret { get; private set; }
    public string VnPayPaymentUrl { get; private set; } = SandboxVnPayUrl;

    /// <summary>Thời hạn một mã QR (phút).</summary>
    public int ExpireMinutes { get; private set; } = DefaultExpireMinutes;

    public static PaymentAccount Create() => new();

    public void UpdateVietQr(bool enabled, string? bankBin, string? accountNo, string? accountName)
    {
        VietQrEnabled = enabled;
        BankBin = Digits(bankBin, 6, 6, "BANK_BIN_INVALID", "Mã BIN ngân hàng gồm 6 chữ số");
        AccountNo = Digits(accountNo, 4, 19, "ACCOUNT_NO_INVALID", "Số tài khoản gồm 4–19 chữ số");
        AccountName = Text(accountName, 100, "ACCOUNT_NAME_INVALID", "Tên chủ tài khoản");
        if (enabled && (BankBin is null || AccountNo is null || AccountName is null))
            throw new BusinessRuleException("VIETQR_INCOMPLETE", "Bật VietQR cần mã BIN, số tài khoản và tên chủ tài khoản.");
    }

    public void UpdateVnPay(bool enabled, string? tmnCode, string? paymentUrl)
    {
        VnPayEnabled = enabled;
        var tmn = Text(tmnCode, 20, "TMN_CODE_INVALID", "Mã website VNPAY (TmnCode)");
        if (tmn is not null && !tmn.All(char.IsAsciiLetterOrDigit)) throw new BusinessRuleException("TMN_CODE_INVALID", "Mã website VNPAY chỉ gồm chữ và số.");
        VnPayTmnCode = tmn;
        var url = string.IsNullOrWhiteSpace(paymentUrl) ? SandboxVnPayUrl : paymentUrl.Trim();
        VnPayPaymentUrl = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && url.Length <= 300
            ? url
            : throw new BusinessRuleException("VNPAY_URL_INVALID", "Địa chỉ cổng VNPAY phải là https.");
        if (enabled && (VnPayTmnCode is null || ProtectedVnPayHashSecret is null))
            throw new BusinessRuleException("VNPAY_INCOMPLETE", "Bật VNPAY cần mã website và khoá bí mật (HashSecret).");
    }

    public void SetExpireMinutes(int minutes) =>
        ExpireMinutes = minutes is >= MinExpireMinutes and <= MaxExpireMinutes
            ? minutes
            : throw new BusinessRuleException("EXPIRE_INVALID", $"Thời hạn mã QR từ {MinExpireMinutes} đến {MaxExpireMinutes} phút.");

    /// <summary>Đặt bí mật đã mã hoá; null = xoá.</summary>
    public void SetProtectedSepayApiKey(string? value) => ProtectedSepayApiKey = string.IsNullOrEmpty(value) ? null : value;

    public void SetProtectedVnPayHashSecret(string? value) => ProtectedVnPayHashSecret = string.IsNullOrEmpty(value) ? null : value;

    public bool Supports(string provider) => provider switch
    {
        PaymentProviders.VietQr => VietQrEnabled,
        PaymentProviders.VnPay => VnPayEnabled,
        _ => false,
    };

    private static string? Text(string? value, int max, string code, string label)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} tối đa {max} ký tự.");
    }

    private static string? Digits(string? value, int min, int max, string code, string message)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length >= min && v.Length <= max && v.All(char.IsAsciiDigit) ? v : throw new BusinessRuleException(code, message + ".");
    }

    /// <summary>Số tiền VND làm tròn (VietQR/VNPAY không nhận phần lẻ).</summary>
    public static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}
