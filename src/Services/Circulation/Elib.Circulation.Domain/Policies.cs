using Elib.BuildingBlocks.Domain;

namespace Elib.Circulation.Domain;

/// <summary>
/// Điểm lưu thông (monolith: CircPlace + CircPlaceStore, quyền CIRC_PLACES) — quầy mượn trả. <see cref="StoreIds"/>: kho (holdings)
/// được mượn tại điểm này; rỗng = mọi kho.
/// </summary>
public sealed class CircPlace : TenantEntity
{
    private CircPlace() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public List<long> StoreIds { get; private set; } = [];

    public static CircPlace Create(string code, string name, IEnumerable<long>? storeIds)
    {
        var place = new CircPlace();
        place.Update(code, name, storeIds);
        return place;
    }

    public void Update(string code, string name, IEnumerable<long>? storeIds)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= 20 ? c : throw new BusinessRuleException("CODE_INVALID", "Mã điểm lưu thông bắt buộc, tối đa 20 ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 250 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên điểm lưu thông bắt buộc, tối đa 250 ký tự.");
        StoreIds = [.. (storeIds ?? []).Where(id => id > 0).Distinct().Order()];
    }

    public bool Allows(long? storeId) => StoreIds.Count == 0 || (storeId is { } id && StoreIds.Contains(id));
}

/// <summary>
/// Chính sách lưu thông (monolith: PolicyCirc, quyền CIRC_POLICIES) theo loại bạn đọc × điểm lưu thông; null = áp cho mọi loại/điểm.
/// Chọn chính sách cụ thể nhất: (loại, điểm) → (loại, mọi điểm) → (mọi loại, điểm) → (mọi loại, mọi điểm) → mặc định 14 ngày.
/// </summary>
public sealed class LoanPolicy : TenantEntity
{
    public const int DefaultLoanDays = 14;
    public const int DefaultRenewDays = 7;
    public const int DefaultHoldDays = 2;

    private LoanPolicy() { }

    public long? ReaderTypeId { get; private set; }
    public long? CircPlaceId { get; private set; }

    /// <summary>Số ngày mượn (monolith: NumberOfDate).</summary>
    public int LoanDays { get; private set; } = DefaultLoanDays;

    /// <summary>Số tài liệu được mượn cùng lúc (monolith: NumberOfBook); null = không giới hạn.</summary>
    public int? MaxLoans { get; private set; }

    /// <summary>Số lần gia hạn tối đa mỗi lượt (monolith: NumberOfRenew); null = không giới hạn, 0 = không cho gia hạn.</summary>
    public int? MaxRenewals { get; private set; }

    /// <summary>Số ngày mỗi lần gia hạn (monolith: NumberOfRenewDays).</summary>
    public int RenewDays { get; private set; } = DefaultRenewDays;

    /// <summary>Tiền phạt mỗi ngày quá hạn (monolith: PolicyCircFine lý do QUAHAN).</summary>
    public decimal FinePerDay { get; private set; }

    /// <summary>Gia hạn tính từ hôm nay thay vì từ hạn cũ (monolith: tham số C_RENEW_DATE = 1).</summary>
    public bool RenewFromToday { get; private set; }

    /// <summary>Số đặt mượn còn hiệu lực cùng lúc (monolith: NumberOfRequest); null = không giới hạn, 0 = không cho đặt mượn.</summary>
    public int? MaxHolds { get; private set; }

    /// <summary>Số ngày giữ sách cho người đặt (monolith: 48 giờ cố định).</summary>
    public int HoldDays { get; private set; } = DefaultHoldDays;

    public static LoanPolicy Create(long? readerTypeId, long? circPlaceId, int loanDays, int? maxLoans, int? maxRenewals, int renewDays,
        decimal finePerDay = 0, bool renewFromToday = false, int? maxHolds = null, int holdDays = DefaultHoldDays)
    {
        var policy = new LoanPolicy();
        policy.Update(readerTypeId, circPlaceId, loanDays, maxLoans, maxRenewals, renewDays, finePerDay, renewFromToday, maxHolds, holdDays);
        return policy;
    }

    public void Update(long? readerTypeId, long? circPlaceId, int loanDays, int? maxLoans, int? maxRenewals, int renewDays,
        decimal finePerDay = 0, bool renewFromToday = false, int? maxHolds = null, int holdDays = DefaultHoldDays)
    {
        ReaderTypeId = readerTypeId;
        CircPlaceId = circPlaceId;
        LoanDays = loanDays is >= 1 and <= 3650 ? loanDays : throw new BusinessRuleException("LOAN_DAYS_INVALID", "Số ngày mượn từ 1 đến 3650.");
        MaxLoans = maxLoans is null or >= 0 ? maxLoans : throw new BusinessRuleException("MAX_LOANS_INVALID", "Số tài liệu được mượn không được âm.");
        MaxRenewals = maxRenewals is null or >= 0 ? maxRenewals : throw new BusinessRuleException("MAX_RENEWALS_INVALID", "Số lần gia hạn không được âm.");
        RenewDays = renewDays is >= 1 and <= 3650 ? renewDays : throw new BusinessRuleException("RENEW_DAYS_INVALID", "Số ngày gia hạn từ 1 đến 3650.");
        FinePerDay = FineMoney.Validate(finePerDay, "Tiền phạt mỗi ngày quá hạn");
        RenewFromToday = renewFromToday;
        MaxHolds = maxHolds is null or >= 0 ? maxHolds : throw new BusinessRuleException("MAX_HOLDS_INVALID", "Số đặt mượn không được âm.");
        HoldDays = holdDays is >= 1 and <= 30 ? holdDays : throw new BusinessRuleException("HOLD_DAYS_INVALID", "Số ngày giữ sách từ 1 đến 30.");
    }

    /// <summary>Độ cụ thể khi chọn chính sách — càng lớn càng ưu tiên.</summary>
    public int Specificity => (ReaderTypeId is null ? 0 : 2) + (CircPlaceId is null ? 0 : 1);

    public static LoanPolicy Default { get; } = new();
}
