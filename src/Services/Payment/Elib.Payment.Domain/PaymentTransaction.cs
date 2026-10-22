using System.Security.Cryptography;
using Elib.BuildingBlocks.Domain;

namespace Elib.Payment.Domain;

public static class TransactionStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";

    /// <summary>Tiền về sau khi mã QR đã hết hạn / bị huỷ — vẫn ghi nhận, cần đối soát (không được để mất dấu tiền).</summary>
    public const string PaidLate = "PaidLate";
}

public enum SettleOutcome
{
    /// <summary>Thu đúng hạn.</summary>
    Paid,

    /// <summary>Tiền về muộn (mã đã hết hạn/huỷ) — ghi nhận, đánh dấu cần đối soát.</summary>
    PaidLate,

    /// <summary>Đã ghi nhận trước đó (IPN/webhook gửi lại) — không làm gì.</summary>
    AlreadyPaid,
}

/// <summary>
/// Một lần sinh mã thanh toán cho một khoản thu. <see cref="Status"/> là concurrency token: hai webhook đến cùng lúc thì chỉ một bên ghi
/// nhận thành công, bên còn lại thấy trạng thái đã đổi và coi như đã xử lý.
/// </summary>
public sealed class PaymentTransaction : TenantEntity
{
    private PaymentTransaction() { }

    public long ChargeId { get; private set; }

    /// <summary>Mã giao dịch — cũng là nội dung chuyển khoản VietQR / vnp_TxnRef.</summary>
    public string TransactionCode { get; private set; } = "";

    public string Provider { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Status { get; private set; } = TransactionStatuses.Pending;
    public DateTimeOffset ExpiresAt { get; private set; }
    public string QrContent { get; private set; } = "";

    /// <summary>Mã giao dịch phía cổng (vnp_TransactionNo / ReferenceCode của Sepay).</summary>
    public string? ProviderRef { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>Số tiền thực nhận (chỉ khác <see cref="Amount"/> khi cần đối soát tay).</summary>
    public decimal? PaidAmount { get; private set; }

    public bool NeedsReview { get; private set; }
    public string? ReviewNote { get; private set; }

    public static PaymentTransaction Create(Charge charge, string provider, string qrContent, string transactionCode, DateTimeOffset now, int expireMinutes)
    {
        ArgumentNullException.ThrowIfNull(charge);
        return new PaymentTransaction
        {
            ChargeId = charge.Id, Provider = provider, Amount = charge.Amount, TransactionCode = transactionCode, QrContent = qrContent,
            ExpiresAt = now.AddMinutes(expireMinutes),
        };
    }

    /// <summary>Mã giao dịch: <c>PT</c> + yyMMddHHmmss (giờ UTC) + 6 hex ngẫu nhiên — dài 20 ký tự, không bị cắt ở nội dung chuyển khoản.</summary>
    public static string NewCode(DateTimeOffset now) =>
        "PT" + now.UtcDateTime.ToString("yyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + Convert.ToHexString(RandomNumberGenerator.GetBytes(3));

    public bool IsPending(DateTimeOffset now) => Status == TransactionStatuses.Pending && ExpiresAt > now;

    /// <summary>Tiền đã về với số tiền <paramref name="received"/>. Sai số tiền là lỗi của người gọi (webhook phải từ chối trước).</summary>
    public SettleOutcome Settle(DateTimeOffset at, string? providerRef, decimal received)
    {
        if (Status is TransactionStatuses.Paid or TransactionStatuses.PaidLate) return SettleOutcome.AlreadyPaid;
        var late = Status != TransactionStatuses.Pending || ExpiresAt < at;
        (PaidAt, ProviderRef, PaidAmount) = (at, providerRef, received);
        if (!late)
        {
            Status = TransactionStatuses.Paid;
            return SettleOutcome.Paid;
        }
        Status = TransactionStatuses.PaidLate;
        NeedsReview = true;
        ReviewNote = "Tiền về sau khi mã QR đã hết hạn hoặc bị huỷ.";
        return SettleOutcome.PaidLate;
    }

    public bool Expire(DateTimeOffset now)
    {
        if (Status != TransactionStatuses.Pending || ExpiresAt > now) return false;
        Status = TransactionStatuses.Expired;
        return true;
    }

    public bool Cancel()
    {
        if (Status != TransactionStatuses.Pending) return false;
        Status = TransactionStatuses.Cancelled;
        return true;
    }

    public void ResolveReview(string note)
    {
        NeedsReview = false;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 500)];
    }

    public void FlagReview(string note)
    {
        NeedsReview = true;
        ReviewNote = note.Length <= 500 ? note : note[..500];
    }
}

/// <summary>Nhật ký nguyên văn lời gọi webhook (đối soát, truy vết). Không chứa khoá bí mật — chỉ phần query/body cổng gửi.</summary>
public sealed class WebhookLog : TenantEntity
{
    public const int MaxPayload = 4000;

    private WebhookLog() { }

    public string Provider { get; private set; } = "";
    public string? TransactionCode { get; private set; }
    public string Payload { get; private set; } = "";
    public string Result { get; private set; } = "";
    public string? RemoteIp { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static WebhookLog Create(string provider, string payload, string result, string? transactionCode, string? remoteIp, DateTimeOffset at) => new()
    {
        Provider = provider, Payload = payload.Length <= MaxPayload ? payload : payload[..MaxPayload], Result = result.Length <= 200 ? result : result[..200],
        TransactionCode = transactionCode, RemoteIp = remoteIp, ReceivedAt = at,
    };
}
