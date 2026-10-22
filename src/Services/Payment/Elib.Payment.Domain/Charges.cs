using Elib.BuildingBlocks.Domain;

namespace Elib.Payment.Domain;

public static class ChargeKinds
{
    public const string FineTicket = "FINE_TICKET";
    public const string Photocopy = "PHOTOCOPY";
    public const string CardReissue = "CARD_REISSUE";

    public static readonly string[] All = [FineTicket, Photocopy, CardReissue];
}

public static class ChargeStatuses
{
    /// <summary>Còn phải thu — có thể sinh mã QR.</summary>
    public const string Open = "Open";

    /// <summary>Đã thu đủ qua cổng thanh toán.</summary>
    public const string Paid = "Paid";

    /// <summary>Đã thu bằng cách khác (tiền mặt tại quầy) — mã QR đang chờ bị đóng.</summary>
    public const string Settled = "Settled";

    /// <summary>Được miễn / huỷ khoản thu.</summary>
    public const string Waived = "Waived";
}

/// <summary>
/// Một khoản cần thu do service nguồn (circulation: phiếu phạt, sao chụp; patron: cấp lại thẻ) phát qua <c>ChargeIssued</c>.
/// Số tiền luôn do nguồn quyết định, không nhận từ client. <see cref="SourceVersion"/> bỏ qua event cũ hơn.
/// </summary>
/// <summary>Đã dựng khoản thu từ dữ liệu có sẵn của một service nguồn cho đơn vị này lúc nào — chỉ cần làm một lần rồi event là đủ.</summary>
public sealed class ChargeSyncState : TenantEntity
{
    private ChargeSyncState() { }

    public string SourceService { get; private set; } = "";
    public DateTimeOffset SyncedAt { get; private set; }

    public static ChargeSyncState Create(string sourceService, DateTimeOffset at) => new() { SourceService = sourceService, SyncedAt = at };

    public void Touch(DateTimeOffset at) => SyncedAt = at;
}

public sealed class Charge : TenantEntity
{
    private Charge() { }

    public string Kind { get; private set; } = "";
    public string SourceService { get; private set; } = "";
    public Guid SourceId { get; private set; }
    public long SourceVersion { get; private set; }
    public Guid ReaderPublicId { get; private set; }
    public string CardNo { get; private set; } = "";

    /// <summary>Nhãn hiển thị ("Phiếu phạt PT012"…).</summary>
    public string Label { get; private set; } = "";

    public decimal Amount { get; private set; }
    public string Status { get; private set; } = ChargeStatuses.Open;

    /// <summary>Mã giao dịch đã thu khoản này — chỉ event của nguồn xác nhận đúng mã này mới được mở lại khoản.</summary>
    public string? PaidTransactionCode { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsOpen => Status == ChargeStatuses.Open;

    public static Charge Create(string kind, string sourceService, Guid sourceId, long version, Guid readerPublicId, string cardNo, string label, decimal amount)
    {
        var charge = new Charge();
        charge.Apply(kind, version, readerPublicId, cardNo, label, amount, null);
        charge.SourceService = sourceService.Trim().ToLowerInvariant();
        charge.SourceId = sourceId;
        return charge;
    }

    /// <summary>
    /// Cập nhật theo event mới hơn. Trả false nếu event cũ (bỏ qua). Khoản đã thu qua cổng (<see cref="ChargeStatuses.Paid"/>) CHỈ theo số tiền của
    /// nguồn khi event xác nhận đã ghi đúng giao dịch đó (<paramref name="lastPaymentCode"/>) — event cũ còn đang bay không mở lại khoản; sau khi nguồn
    /// đã ghi nhận mà số còn phải thu lại &gt; 0 (phiếu mở lại, thêm dòng phạt) thì khoản mở lại để sinh mã mới.
    /// </summary>
    public bool Apply(string kind, long version, Guid readerPublicId, string cardNo, string label, decimal amount, string? lastPaymentCode)
    {
        if (SourceService.Length > 0 && version <= SourceVersion) return false;
        if (Status == ChargeStatuses.Paid && lastPaymentCode != PaidTransactionCode)
        {
            SourceVersion = version; // đã thấy event này, nhưng nguồn chưa phản ánh khoản đã thu: giữ nguyên "đã thu"
            return false;
        }
        if (!ChargeKinds.All.Contains(kind)) throw new BusinessRuleException("CHARGE_KIND_INVALID", $"Loại khoản thu '{kind}' không hợp lệ.");
        if (amount < 0 || amount > 1_000_000_000m || decimal.Round(amount, 0) != amount)
            throw new BusinessRuleException("AMOUNT_INVALID", "Số tiền khoản thu là số nguyên đồng, từ 0 đến 1.000.000.000.");
        (Kind, SourceVersion, ReaderPublicId, CardNo, Label, Amount) = (kind, version, readerPublicId, cardNo.Trim(), label.Trim(), amount);
        if (Status == ChargeStatuses.Paid && amount > 0) PaidTransactionCode = null; // nguồn đã ghi khoản thu nhưng lại nợ thêm → mở lại
        if (Status != ChargeStatuses.Paid || amount > 0) Status = amount > 0 ? ChargeStatuses.Open : ChargeStatuses.Settled; // thu đủ ở nguồn → không còn gì phải thu
        ClosedAt = Status == ChargeStatuses.Open ? null : ClosedAt ?? DateTimeOffset.UtcNow;
        return true;
    }

    public void MarkPaid(DateTimeOffset at, string transactionCode)
    {
        PaidTransactionCode = transactionCode;
        Close(ChargeStatuses.Paid, at);
    }

    public void MarkSettled(DateTimeOffset at) => Close(ChargeStatuses.Settled, at);

    public void MarkWaived(DateTimeOffset at) => Close(ChargeStatuses.Waived, at);

    private void Close(string status, DateTimeOffset at)
    {
        if (Status == ChargeStatuses.Paid && status != ChargeStatuses.Paid) return; // đã thu qua cổng: không bị miễn/đóng đè
        Status = status;
        ClosedAt = at;
    }
}
