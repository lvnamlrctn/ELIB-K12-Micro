using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Payment.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Payment.Application;

/// <summary>Mã hoá bí mật cổng thanh toán lưu trong DB (Infrastructure: ASP.NET Data Protection, khoá lưu trong DB của service).</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>Null nếu không giải mã được (khoá đã mất/xoay) — coi như chưa có bí mật.</summary>
    string? Unprotect(string protectedValue);
}

/// <summary>Cấu hình hiện tại. Bí mật không bao giờ trả lại — chỉ báo có hay chưa.</summary>
public sealed record PaymentAccountDto(
    bool Configured, bool VietQrEnabled, string? BankBin, string? AccountNo, string? AccountName, bool HasSepayApiKey,
    bool VnPayEnabled, string? VnPayTmnCode, bool HasVnPayHashSecret, string VnPayPaymentUrl, int ExpireMinutes);

/// <summary>
/// <see cref="SepayApiKey"/> / <see cref="VnPayHashSecret"/>: rỗng = giữ bí mật cũ; <c>Clear…</c> = xoá.
/// </summary>
public sealed record PaymentAccountRequest(
    bool VietQrEnabled, string? BankBin, string? AccountNo, string? AccountName, string? SepayApiKey, bool ClearSepayApiKey,
    bool VnPayEnabled, string? VnPayTmnCode, string? VnPayHashSecret, bool ClearVnPayHashSecret, string? VnPayPaymentUrl, int ExpireMinutes = PaymentAccount.DefaultExpireMinutes);

/// <summary>Tài khoản nhận tiền của đơn vị (mỗi đơn vị một bản ghi).</summary>
public sealed class PaymentAccountService(ICrudDbContext db, ISecretProtector secrets, ICrudAuditSink? audit = null)
{
    private const int MaxSecret = 300;

    public async Task<PaymentAccountDto> GetAsync(CancellationToken ct) => ToDto(await db.Set<PaymentAccount>().AsNoTracking().FirstOrDefaultAsync(ct));

    public async Task<PaymentAccountDto> SaveAsync(PaymentAccountRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await db.Set<PaymentAccount>().FirstOrDefaultAsync(ct);
        var isNew = account is null;
        if (isNew) db.Set<PaymentAccount>().Add(account = PaymentAccount.Create());

        // Bí mật trước (VNPAY bật cần có HashSecret), rồi mới kiểm tính đầy đủ.
        if (request.ClearSepayApiKey) account!.SetProtectedSepayApiKey(null);
        else if (!string.IsNullOrEmpty(request.SepayApiKey)) account!.SetProtectedSepayApiKey(secrets.Protect(Checked(request.SepayApiKey, "Khoá Sepay")));
        if (request.ClearVnPayHashSecret) account!.SetProtectedVnPayHashSecret(null);
        else if (!string.IsNullOrEmpty(request.VnPayHashSecret)) account!.SetProtectedVnPayHashSecret(secrets.Protect(Checked(request.VnPayHashSecret, "HashSecret VNPAY")));

        account!.UpdateVietQr(request.VietQrEnabled, request.BankBin, request.AccountNo, request.AccountName);
        account.UpdateVnPay(request.VnPayEnabled, request.VnPayTmnCode, request.VnPayPaymentUrl);
        if (request.VietQrEnabled && account.ProtectedSepayApiKey is null)
            throw new BusinessRuleException("SEPAY_KEY_REQUIRED", "Bật VietQR cần khoá Sepay để xác nhận tiền về (khai báo webhook Sepay với khoá này).");
        account.SetExpireMinutes(request.ExpireMinutes);

        if (audit is not null)
            await audit.RecordAsync(new CrudAuditEntry(nameof(PaymentAccount), "Tài khoản nhận tiền", account.PublicId == Guid.Empty ? Guid.CreateVersion7() : account.PublicId,
                isNew ? CrudChange.Added : CrudChange.Updated,
                $"VietQR {(account.VietQrEnabled ? "bật" : "tắt")}, VNPAY {(account.VnPayEnabled ? "bật" : "tắt")}"), ct); // không ghi bí mật vào nhật ký
        await db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    /// <summary>Giải mã bí mật để ký/xác thực — chỉ dùng nội bộ service, không bao giờ ra API.</summary>
    public string? Reveal(string? protectedValue) => string.IsNullOrEmpty(protectedValue) ? null : secrets.Unprotect(protectedValue);

    private static string Checked(string secret, string what) =>
        secret.Length <= MaxSecret ? secret.Trim() : throw new BusinessRuleException("SECRET_INVALID", $"{what} tối đa {MaxSecret} ký tự.");

    private static PaymentAccountDto ToDto(PaymentAccount? a) => a is null
        ? new PaymentAccountDto(false, false, null, null, null, false, false, null, false, PaymentAccount.SandboxVnPayUrl, PaymentAccount.DefaultExpireMinutes)
        : new PaymentAccountDto(true, a.VietQrEnabled, a.BankBin, a.AccountNo, a.AccountName, a.ProtectedSepayApiKey is not null,
            a.VnPayEnabled, a.VnPayTmnCode, a.ProtectedVnPayHashSecret is not null, a.VnPayPaymentUrl, a.ExpireMinutes);
}
