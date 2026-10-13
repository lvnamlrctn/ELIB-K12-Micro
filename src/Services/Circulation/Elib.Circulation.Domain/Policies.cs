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

    /// <summary>
    /// Phí xử lý kỹ thuật và phí vận chuyển (ELIB-LRC: PolicyCircFine mã XLKY / VANCHUYEN) — cộng một lần vào phiếu phạt khi bạn đọc
    /// nợ tài liệu (phiếu có dòng "Mất tài liệu").
    /// </summary>
    public decimal TechFee { get; private set; }

    public decimal ShippingFee { get; private set; }

    /// <summary>Luật riêng theo nhóm tài liệu = loại biểu ghi (ELIB-LRC: PolicyCircDocGroup); trường trống = theo chính sách chung.</summary>
    public List<BibTypeRule> BibTypeRules { get; private set; } = [];

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

    public void SetFees(decimal techFee, decimal shippingFee)
    {
        TechFee = FineMoney.Validate(techFee, "Phí xử lý kỹ thuật");
        ShippingFee = FineMoney.Validate(shippingFee, "Phí vận chuyển");
    }

    public void SetBibTypeRules(IEnumerable<BibTypeRule>? rules)
    {
        var list = (rules ?? []).Select(BibTypeRule.Validate).ToList();
        if (list.Count > BibTypeRule.MaxRules) throw new BusinessRuleException("BIB_TYPE_RULES_TOO_MANY", $"Tối đa {BibTypeRule.MaxRules} nhóm tài liệu mỗi chính sách.");
        if (list.GroupBy(r => r.BibTypeId).Any(g => g.Count() > 1))
            throw new BusinessRuleException("BIB_TYPE_RULE_DUPLICATE", "Mỗi nhóm tài liệu chỉ khai báo một lần trong một chính sách.");
        BibTypeRules = [.. list.OrderBy(r => r.BibTypeId)];
    }

    /// <summary>Luật của nhóm tài liệu (loại biểu ghi) — null nếu chưa khai báo hoặc chưa biết loại.</summary>
    public BibTypeRule? RuleFor(long? bibTypeId) => bibTypeId is { } id ? BibTypeRules.FirstOrDefault(r => r.BibTypeId == id) : null;

    /// <summary>Số ngày mượn cho tài liệu thuộc nhóm (luật riêng nếu có).</summary>
    public int LoanDaysFor(long? bibTypeId) => RuleFor(bibTypeId)?.LoanDays ?? LoanDays;

    /// <summary>Độ cụ thể khi chọn chính sách — càng lớn càng ưu tiên.</summary>
    public int Specificity => (ReaderTypeId is null ? 0 : 2) + (CircPlaceId is null ? 0 : 1);

    public static LoanPolicy Default { get; } = new();
}

/// <summary>
/// Luật theo nhóm tài liệu trong một chính sách (ELIB-LRC: PolicyCircDocGroup — NumberOfBook, NumberOfRenewQty, NumberOfRequest; LRC chỉ
/// khai báo, ở đây áp thật khi mượn/gia hạn/đặt mượn). <see cref="LoanDays"/> thêm mới: sách tham khảo thường mượn ngắn hơn.
/// </summary>
public sealed class BibTypeRule
{
    public const int MaxRules = 100;

    public long BibTypeId { get; set; }

    /// <summary>Số ngày mượn; null = theo chính sách.</summary>
    public int? LoanDays { get; set; }

    /// <summary>Số tài liệu nhóm này được mượn cùng lúc; null = không giới hạn riêng (vẫn tính vào giới hạn chung).</summary>
    public int? MaxLoans { get; set; }

    /// <summary>Số lần gia hạn; null = theo chính sách.</summary>
    public int? MaxRenewals { get; set; }

    /// <summary>Số đặt mượn còn hiệu lực cho nhóm này; null = không giới hạn riêng.</summary>
    public int? MaxHolds { get; set; }

    public static BibTypeRule Validate(BibTypeRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.BibTypeId <= 0) throw new BusinessRuleException("BIB_TYPE_REQUIRED", "Chọn nhóm tài liệu (loại biểu ghi).");
        if (rule.LoanDays is { } d && d is < 1 or > 3650) throw new BusinessRuleException("LOAN_DAYS_INVALID", "Số ngày mượn từ 1 đến 3650.");
        if (rule.MaxLoans < 0 || rule.MaxRenewals < 0 || rule.MaxHolds < 0)
            throw new BusinessRuleException("BIB_TYPE_RULE_INVALID", "Số lượng trong luật nhóm tài liệu không được âm.");
        return new BibTypeRule
        {
            BibTypeId = rule.BibTypeId, LoanDays = rule.LoanDays, MaxLoans = rule.MaxLoans, MaxRenewals = rule.MaxRenewals, MaxHolds = rule.MaxHolds,
        };
    }
}
