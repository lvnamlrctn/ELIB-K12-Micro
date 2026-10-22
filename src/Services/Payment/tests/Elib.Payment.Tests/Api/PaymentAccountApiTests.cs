using System.Net;
using System.Net.Http.Json;
using Elib.Payment.Application;
using Elib.Payment.Domain;

namespace Elib.Payment.Tests.Api;

public sealed class PaymentAccountApiTests(PaymentApiFactory factory) : PaymentTestBase(factory)
{
    private static PaymentAccountRequest Full(string? sepay = "sepay-secret-key", string? hash = "vnpay-hash-secret") => new(
        VietQrEnabled: true, BankBin: "970422", AccountNo: "0123456789", AccountName: "THU VIEN TRUONG A", SepayApiKey: sepay, ClearSepayApiKey: false,
        VnPayEnabled: true, VnPayTmnCode: "ELIBTEST", VnPayHashSecret: hash, ClearVnPayHashSecret: false, VnPayPaymentUrl: null, ExpireMinutes: 20);

    [Fact]
    public async Task A_new_unit_has_no_account_and_saving_keeps_secrets_out_of_every_response()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var none = await Read<PaymentAccountDto>(await staff.GetAsync(U("/api/payment-account")));
        Assert.False(none.Configured);

        var saved = await staff.PutAsJsonAsync(U("/api/payment-account"), Full(), Json);
        var text = await saved.Content.ReadAsStringAsync();
        Assert.True(saved.IsSuccessStatusCode, text);
        Assert.DoesNotContain("sepay-secret-key", text);
        Assert.DoesNotContain("vnpay-hash-secret", text);
        var dto = await Read<PaymentAccountDto>(await staff.GetAsync(U("/api/payment-account")));
        Assert.Equal((true, true, true, true, "970422", 20), (dto.Configured, dto.HasSepayApiKey, dto.HasVnPayHashSecret, dto.VietQrEnabled, dto.BankBin, dto.ExpireMinutes));
        Assert.Equal(PaymentAccount.SandboxVnPayUrl, dto.VnPayPaymentUrl);

        // Lưu lại không gửi bí mật → giữ nguyên; muốn xoá phải nói rõ.
        var keep = await Read<PaymentAccountDto>(await staff.PutAsJsonAsync(U("/api/payment-account"), Full(null, null), Json));
        Assert.True(keep.HasSepayApiKey && keep.HasVnPayHashSecret);
        var cleared = await Read<PaymentAccountDto>(await staff.PutAsJsonAsync(U("/api/payment-account"),
            Full(null, null) with { VnPayEnabled = false, ClearVnPayHashSecret = true }, Json));
        Assert.False(cleared.HasVnPayHashSecret);

        // Đơn vị khác không thấy gì.
        Assert.False((await Read<PaymentAccountDto>(await Staff(NewTenant()).GetAsync(U("/api/payment-account")))).Configured);
    }

    [Fact]
    public async Task Incomplete_or_invalid_settings_are_rejected_with_stable_codes()
    {
        var staff = Staff(NewTenant());
        Assert.Equal("SEPAY_KEY_REQUIRED", await ErrorCode(await staff.PutAsJsonAsync(U("/api/payment-account"), Full(sepay: null) with { VnPayEnabled = false }, Json)));
        Assert.Equal("BANK_BIN_INVALID", await ErrorCode(await staff.PutAsJsonAsync(U("/api/payment-account"), Full() with { BankBin = "12" }, Json)));
        Assert.Equal("VNPAY_INCOMPLETE", await ErrorCode(await staff.PutAsJsonAsync(U("/api/payment-account"), Full(hash: null), Json)));
        Assert.Equal("VNPAY_URL_INVALID", await ErrorCode(await staff.PutAsJsonAsync(U("/api/payment-account"), Full() with { VnPayPaymentUrl = "http://evil.test/pay" }, Json)));
        Assert.Equal("EXPIRE_INVALID", await ErrorCode(await staff.PutAsJsonAsync(U("/api/payment-account"), Full() with { ExpireMinutes = 1 }, Json)));
    }

    [Fact]
    public async Task Permission_and_license_are_enforced()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "PAYMENT_ACCOUNTS:view").PutAsJsonAsync(U("/api/payment-account"), Full(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "CIRC_POLICIES:view").GetAsync(U("/api/payment-account"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(NewTenant(licensed: false)).GetAsync(U("/api/payment-account"))).StatusCode);
    }
}
