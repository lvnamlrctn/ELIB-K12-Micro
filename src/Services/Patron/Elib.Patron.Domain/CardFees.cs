using Elib.BuildingBlocks.Domain;

namespace Elib.Patron.Domain;

public static class CardFeeStatuses
{
    /// <summary>Đã lập, chưa thu.</summary>
    public const string Open = "Open";

    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// Phí cấp lại thẻ của một bạn đọc (monolith: tham số PHI_CAP_LAI_THE + màn bạn đọc). Số tiền do cán bộ nhập lúc lập (có giới hạn) kèm lý do —
/// bản vi dịch vụ chưa có kho tham số phí để Patron đọc; thu bằng tiền mặt tại quầy hoặc qua mã QR của dịch vụ thanh toán
/// (<c>ChargeIssued</c> → <c>PaymentSucceeded</c>). <see cref="Version"/> bỏ qua event cũ ở payment.
/// </summary>
public sealed class CardFee : TenantEntity
{
    public const decimal MaxAmount = 10_000_000m;

    private CardFee() { }

    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";
    public string ReaderName { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = "";
    public string Status { get; private set; } = CardFeeStatuses.Open;
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>CASH | QR.</summary>
    public string? PaidVia { get; private set; }

    /// <summary>Mã giao dịch QR đã thu — khoá chống ghi hai lần và gửi kèm khoản thu để payment biết nguồn đã ghi nhận.</summary>
    public string? PaymentCode { get; private set; }

    public string? CreatedByName { get; private set; }
    public long Version { get; private set; }

    public static CardFee Create(Reader reader, decimal amount, string reason, string? byName)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (amount is <= 0 or > MaxAmount || decimal.Round(amount, 0) != amount)
            throw new BusinessRuleException("AMOUNT_INVALID", $"Phí cấp lại thẻ là số nguyên đồng từ 1 đến {MaxAmount:N0}.");
        var r = reason?.Trim() ?? "";
        if (r.Length is 0 or > 500) throw new BusinessRuleException("REASON_REQUIRED", "Nhập lý do cấp lại thẻ (tối đa 500 ký tự).");
        return new CardFee
        {
            ReaderPublicId = reader.PublicId, CardNo = reader.CardNo, ReaderName = reader.FullName, Amount = amount, Reason = r,
            CreatedByName = byName is { Length: > 300 } ? byName[..300] : byName, Version = 1,
        };
    }

    public bool IsOpen => Status == CardFeeStatuses.Open;

    /// <summary>Thu tiền mặt tại quầy.</summary>
    public void SettleCash(DateTimeOffset at)
    {
        EnsureOpen();
        (Status, PaidAt, PaidVia) = (CardFeeStatuses.Paid, at, "CASH");
        Version++;
    }

    /// <summary>Tiền về qua cổng. Trả false nếu mã này đã ghi nhận (event gửi lại).</summary>
    public bool SettleByGateway(DateTimeOffset at, string transactionCode)
    {
        if (PaymentCode == transactionCode) return false;
        (Status, PaidAt, PaidVia, PaymentCode) = (CardFeeStatuses.Paid, at, "QR", transactionCode);
        Version++;
        return true;
    }

    public void Cancel()
    {
        EnsureOpen();
        Status = CardFeeStatuses.Cancelled;
        Version++;
    }

    private void EnsureOpen()
    {
        if (!IsOpen) throw new BusinessRuleException("CARD_FEE_CLOSED", $"Phí cấp lại thẻ của {CardNo} không còn ở trạng thái chờ thu.", 409);
    }
}
