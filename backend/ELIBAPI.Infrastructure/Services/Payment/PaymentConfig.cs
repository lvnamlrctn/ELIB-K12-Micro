using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace ELIBAPI.Infrastructure.Services.Payment;

/// <summary>Cấu hình cổng thanh toán theo đơn vị. Mỗi đơn vị (trường) có tài khoản nhận tiền riêng, khai báo bằng
/// SystemParameter của đơn vị (trang Tham số hệ thống); đơn vị chưa khai báo thì dùng mục VietQr/VnPay/Sepay trong appsettings.
/// Mỗi nhóm lấy trọn bộ từ MỘT nguồn (đơn vị có mã khoá chính → dùng các trường của đơn vị) để không trộn tài khoản đơn vị
/// với ngân hàng của cấu hình chung.</summary>
public class PaymentConfig(ELIBAPIDbContext db, IOptions<VietQrOptions> vietQr, IOptions<VnPayOptions> vnPay, IOptions<SepayOptions> sepay)
{
    public const string VietQrBankBinKey     = "PAYMENT_VIETQR_BANK_BIN";
    public const string VietQrAccountNoKey   = "PAYMENT_VIETQR_ACCOUNT_NO";
    public const string VietQrAccountNameKey = "PAYMENT_VIETQR_ACCOUNT_NAME";
    public const string VnPayTmnCodeKey      = "PAYMENT_VNPAY_TMN_CODE";
    public const string VnPayHashSecretKey   = "PAYMENT_VNPAY_HASH_SECRET";
    public const string VnPayReturnUrlKey    = "PAYMENT_VNPAY_RETURN_URL";
    public const string SepayApiKeyKey       = "PAYMENT_SEPAY_APIKEY";
    public const string CardReissueFeeKey    = "PHI_CAP_LAI_THE";

    public async Task<VietQrOptions> VietQrAsync(long? tenantId)
    {
        var accountNo = await TenantParams.PlainAsync(db, VietQrAccountNoKey, tenantId);
        if (accountNo == "") return vietQr.Value;
        return new VietQrOptions
        {
            AccountNo   = accountNo,
            BankBin     = await TenantParams.PlainAsync(db, VietQrBankBinKey, tenantId),
            AccountName = await TenantParams.PlainAsync(db, VietQrAccountNameKey, tenantId),
        };
    }

    public async Task<VnPayOptions> VnPayAsync(long? tenantId)
    {
        var tmnCode = await TenantParams.PlainAsync(db, VnPayTmnCodeKey, tenantId);
        if (tmnCode == "") return vnPay.Value;
        var returnUrl = await TenantParams.PlainAsync(db, VnPayReturnUrlKey, tenantId);
        return new VnPayOptions
        {
            TmnCode    = tmnCode,
            HashSecret = await TenantParams.PlainAsync(db, VnPayHashSecretKey, tenantId),
            PaymentUrl = vnPay.Value.PaymentUrl,
            ReturnUrl  = returnUrl == "" ? vnPay.Value.ReturnUrl : returnUrl,
        };
    }

    /// <summary>Khoá webhook Sepay của đơn vị; tenantId null = khoá chung trong appsettings.</summary>
    public async Task<string> SepayApiKeyAsync(long? tenantId)
    {
        if (tenantId == null) return sepay.Value.ApiKey;
        var key = await TenantParams.PlainAsync(db, SepayApiKeyKey, tenantId);
        return key == "" ? sepay.Value.ApiKey : key;
    }

    /// <summary>Phí cấp lại thẻ (VND, số nguyên) — chấp nhận "50000", "50.000", "50,000 đ".</summary>
    public async Task<double> CardReissueFeeAsync(long? tenantId)
    {
        var digits = new string((await TenantParams.PlainAsync(db, CardReissueFeeKey, tenantId)).Where(char.IsAsciiDigit).ToArray());
        return double.TryParse(digits, out var fee) ? fee : 0;
    }
}
