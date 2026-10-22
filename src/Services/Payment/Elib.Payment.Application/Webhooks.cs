using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Payment;
using Elib.Payment.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Payment.Application;

/// <summary>Phản hồi IPN của VNPAY: <c>RspCode</c> 00 thành công, 01 không thấy đơn, 02 đã xác nhận, 04 sai số tiền, 97 sai chữ ký, 99 lỗi khác.</summary>
public sealed record VnPayIpnResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("RspCode")] string RspCode,
    [property: System.Text.Json.Serialization.JsonPropertyName("Message")] string Message);

/// <summary>Webhook của Sepay (https://docs.sepay.vn) — các trường dùng để đối soát.</summary>
public sealed record SepayWebhook(
    long Id, string? Gateway, string? TransactionDate, string? AccountNumber, string? Code, string? Content, string? TransferType, decimal TransferAmount,
    string? ReferenceCode, string? Description);

public sealed record SepayResponse(bool Success, string Message);

/// <summary>
/// Nhận tiền về từ cổng thanh toán. Quy tắc đối soát (sửa các lỗ hổng của monolith): chữ ký/khoá kiểm bằng so sánh hằng thời gian; số tiền BẮT BUỘC
/// khớp giao dịch (VNPAY cũng kiểm); gửi lại không ghi hai lần (trạng thái là concurrency token); tiền về sau khi mã hết hạn vẫn được ghi nhận
/// và đánh dấu cần đối soát — không bao giờ để mất dấu tiền; khoản đã được thu bằng cách khác thì không báo nguồn ghi lần hai mà chỉ đánh dấu cần hoàn tiền.
/// </summary>
public sealed partial class WebhookService(
    ICrudDbContext db, PaymentAccountService accounts, IPublishEndpoint publisher, ITenantContext tenant, TimeProvider clock)
{
    [GeneratedRegex(@"PT\d{12}[0-9A-F]{6}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public async Task<VnPayIpnResponse> VnPayIpnAsync(IReadOnlyDictionary<string, string> query, string? remoteIp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var raw = Truncate(string.Join('&', query.Where(kv => kv.Key != "vnp_SecureHash").OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)));
        query.TryGetValue("vnp_TxnRef", out var code);
        var account = await db.Set<PaymentAccount>().AsNoTracking().FirstOrDefaultAsync(ct);
        var secret = account is { VnPayEnabled: true } ? accounts.Reveal(account.ProtectedVnPayHashSecret) : null;
        if (secret is null) return await Log(PaymentProviders.VnPay, raw, code, remoteIp, new VnPayIpnResponse("99", "VNPAY chưa được bật hoặc thiếu khoá"), ct);
        if (!VnPayUrl.Verify(query, secret)) return await Log(PaymentProviders.VnPay, raw, code, remoteIp, new VnPayIpnResponse("97", "Invalid signature"), ct);
        if (string.IsNullOrWhiteSpace(code)) return await Log(PaymentProviders.VnPay, raw, code, remoteIp, new VnPayIpnResponse("01", "Order not found"), ct);

        var txn = await db.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.TransactionCode == code && t.Provider == PaymentProviders.VnPay, ct);
        if (txn is null || (query.TryGetValue("vnp_TmnCode", out var tmn) && tmn != account!.VnPayTmnCode))
            return await Log(PaymentProviders.VnPay, raw, code, remoteIp, new VnPayIpnResponse("01", "Order not found"), ct);

        // Số tiền: vnp_Amount = đồng × 100. Lệch → không ghi nhận, đánh dấu để đối soát.
        if (!query.TryGetValue("vnp_Amount", out var amountText) || !decimal.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out var amount100) || amount100 != txn.Amount * 100)
        {
            txn.FlagReview($"VNPAY báo số tiền {amountText} (mong {txn.Amount * 100}) — không ghi nhận.");
            return await Log(PaymentProviders.VnPay, raw, code, remoteIp, new VnPayIpnResponse("04", "Invalid amount"), ct);
        }
        if (txn.Status is TransactionStatuses.Paid or TransactionStatuses.PaidLate) return await Log(PaymentProviders.VnPay, raw, code, remoteIp, new VnPayIpnResponse("02", "Order already confirmed"), ct);

        // Giao dịch thất bại / người dùng huỷ: chỉ xác nhận đã nhận thông báo, không đổi gì.
        var ok = query.GetValueOrDefault("vnp_ResponseCode") == "00" && query.GetValueOrDefault("vnp_TransactionStatus", "00") == "00";
        if (!ok) return await Log(PaymentProviders.VnPay, raw, code, remoteIp, new VnPayIpnResponse("00", "Confirm Success"), ct);

        return await SettleAsync(txn, query.GetValueOrDefault("vnp_TransactionNo"), txn.Amount, PaymentProviders.VnPay, raw, remoteIp,
            new VnPayIpnResponse("00", "Confirm Success"), new VnPayIpnResponse("02", "Order already confirmed"), ct);
    }

    /// <summary>Webhook Sepay. <paramref name="authorization"/> = header Authorization ("Apikey …").</summary>
    public async Task<(int Status, SepayResponse Body)> SepayAsync(SepayWebhook hook, string? authorization, string? remoteIp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hook);
        var raw = Truncate($"id={hook.Id};acc={hook.AccountNumber};type={hook.TransferType};amount={hook.TransferAmount};ref={hook.ReferenceCode};content={hook.Content};code={hook.Code}");
        var account = await db.Set<PaymentAccount>().AsNoTracking().FirstOrDefaultAsync(ct);
        var key = account is { VietQrEnabled: true } ? accounts.Reveal(account.ProtectedSepayApiKey) : null;
        var given = authorization is { } a && a.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase) ? a[7..].Trim() : "";
        if (key is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(given)))
        {
            await Log(PaymentProviders.VietQr, raw, null, remoteIp, new SepayResponse(false, "Unauthorized"), ct);
            return (401, new SepayResponse(false, "Unauthorized"));
        }
        if (!string.Equals(hook.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            return (200, await Log(PaymentProviders.VietQr, raw, null, remoteIp, new SepayResponse(true, "Ignored: not an incoming transfer"), ct));

        var code = CodePattern().Match(hook.Content ?? "").Value.ToUpperInvariant();
        if (code.Length == 0) code = CodePattern().Match(hook.Code ?? "").Value.ToUpperInvariant();
        var txn = code.Length == 0 ? null : await db.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.TransactionCode == code && t.Provider == PaymentProviders.VietQr, ct);
        if (txn is null) return (200, await Log(PaymentProviders.VietQr, raw, code.Length == 0 ? null : code, remoteIp, new SepayResponse(true, "Ignored: no matching transaction"), ct));

        if (hook.TransferAmount != txn.Amount)
        {
            txn.FlagReview($"Sepay báo {PaymentAccount.Money(hook.TransferAmount)} (mong {PaymentAccount.Money(txn.Amount)}) — không ghi nhận, cần đối soát/hoàn tiền.");
            return (200, await Log(PaymentProviders.VietQr, raw, code, remoteIp, new SepayResponse(true, "Amount mismatch — flagged for review"), ct));
        }
        var reference = string.IsNullOrWhiteSpace(hook.ReferenceCode) ? hook.Id.ToString(CultureInfo.InvariantCulture) : hook.ReferenceCode.Trim();
        var result = await SettleAsync(txn, reference, hook.TransferAmount, PaymentProviders.VietQr, raw, remoteIp,
            new SepayResponse(true, "OK"), new SepayResponse(true, "Already confirmed"), ct);
        return (200, result);
    }

    private async Task<T> SettleAsync<T>(PaymentTransaction txn, string? providerRef, decimal received, string provider, string raw, string? remoteIp, T success, T duplicate, CancellationToken ct)
        where T : class
    {
        var now = clock.GetUtcNow();
        var charge = await db.Set<Charge>().FirstAsync(c => c.Id == txn.ChargeId, ct);
        var chargeWasOpen = charge.IsOpen;
        var outcome = txn.Settle(now, providerRef, received);
        if (outcome == SettleOutcome.AlreadyPaid) return await Log(provider, raw, txn.TransactionCode, remoteIp, duplicate, ct);

        if (chargeWasOpen)
        {
            charge.MarkPaid(now, txn.TransactionCode);
            if (received != charge.Amount) txn.FlagReview($"Nhận {PaymentAccount.Money(received)} nhưng khoản hiện tại là {PaymentAccount.Money(charge.Amount)} — kiểm tra phần chênh lệch.");
            await publisher.Publish(new PaymentSucceeded
            {
                TenantId = tenant.RequireTenantId(), SourceService = charge.SourceService, SourceId = charge.SourceId, Kind = charge.Kind,
                TransactionCode = txn.TransactionCode, Provider = provider, Amount = received, PaidAt = now, ReaderPublicId = charge.ReaderPublicId,
                Late = outcome == SettleOutcome.PaidLate,
            }, ct);
        }
        else
        {
            // Khoản đã được thu bằng cách khác (tiền mặt, thanh toán khác) hoặc đã miễn: báo nguồn ghi nữa sẽ thu hai lần → chỉ ghi nhận và đánh dấu hoàn tiền.
            txn.FlagReview($"Tiền {PaymentAccount.Money(received)} về nhưng khoản \"{charge.Label}\" đã {StatusText(charge.Status)} — cần hoàn tiền cho bạn đọc.");
        }
        var log = WebhookLog.Create(provider, raw, outcome == SettleOutcome.PaidLate ? "Paid late — flagged" : "Paid", txn.TransactionCode, remoteIp, now);
        db.Set<WebhookLog>().Add(log);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Chỉ coi là "đã xử lý" khi thật sự có bên khác ghi nhận trước (Status là concurrency token / ProviderRef là duy nhất). Lỗi cơ sở dữ liệu khác
            // thì ném tiếp để cổng nhận 5xx và gửi lại — trả "02" ở đây sẽ làm cổng ngừng thử và mất khoản thu.
            var status = await db.Set<PaymentTransaction>().AsNoTracking().Where(t => t.TransactionCode == txn.TransactionCode).Select(t => t.Status).FirstOrDefaultAsync(CancellationToken.None);
            if (status is TransactionStatuses.Paid or TransactionStatuses.PaidLate) return duplicate;
            throw;
        }
        return success;
    }

    private async Task<T> Log<T>(string provider, string raw, string? code, string? remoteIp, T response, CancellationToken ct) where T : class
    {
        db.Set<WebhookLog>().Add(WebhookLog.Create(provider, raw, response switch
        {
            VnPayIpnResponse v => $"{v.RspCode} {v.Message}",
            SepayResponse s => s.Message,
            _ => response.ToString() ?? "",
        }, code, remoteIp, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return response;
    }

    private static string StatusText(string status) => status switch
    {
        ChargeStatuses.Paid => "được thanh toán qua giao dịch khác",
        ChargeStatuses.Settled => "thu bằng cách khác (tiền mặt)",
        _ => "được miễn / huỷ",
    };

    private static string Truncate(string text) => text.Length <= WebhookLog.MaxPayload ? text : text[..WebhookLog.MaxPayload];
}

/// <summary>Đóng mã hết hạn và báo nguồn; chạy theo đơn vị (job nền gọi cho từng đơn vị có license PAYMENT).</summary>
public sealed class PaymentExpiry(ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant, TimeProvider clock)
{
    public const int Batch = 500;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.Set<PaymentTransaction>().Where(t => t.Status == TransactionStatuses.Pending && t.ExpiresAt <= now).OrderBy(t => t.Id).Take(Batch).ToListAsync(ct);
        foreach (var txn in due)
        {
            if (!txn.Expire(now)) continue;
            var charge = await db.Set<Charge>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == txn.ChargeId, ct);
            if (charge is null) continue;
            await publisher.Publish(new PaymentExpired
            {
                TenantId = tenant.RequireTenantId(), SourceService = charge.SourceService, SourceId = charge.SourceId, TransactionCode = txn.TransactionCode,
            }, ct);
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
