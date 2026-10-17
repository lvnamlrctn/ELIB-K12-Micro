using Elib.BuildingBlocks.Domain;

namespace Elib.Acquisition.Domain;

/// <summary>Ngày lịch Việt Nam (UTC+7) — ngày đặt, ngày nhận mặc định theo giờ VN.</summary>
public static class VietnamDate
{
    public static DateOnly Of(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime.AddHours(7));

    /// <summary>0 giờ ngày <paramref name="date"/> giờ VN, biểu diễn ở UTC (Npgsql chỉ nhận timestamptz offset 0).</summary>
    public static DateTimeOffset Start(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
}

internal static class TextRules
{
    public static string? Optional(string? value, int max, string code, string label)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} tối đa {max} ký tự.");
    }

    public static string Required(string? value, int max, string code, string label)
    {
        var v = (value ?? "").Trim();
        return v.Length is > 0 && v.Length <= max ? v : throw new BusinessRuleException(code, $"{label} bắt buộc, tối đa {max} ký tự.");
    }

    public static decimal? Money(decimal? value, string label) => value is null or >= 0
        ? value
        : throw new BusinessRuleException("AMOUNT_INVALID", $"{label} không được âm.");

    /// <summary>Tỷ giá quy đổi sang tiền gốc (monolith: Rate, mặc định 1). Phải lớn hơn 0.</summary>
    public static decimal Rate(decimal? value) => value switch
    {
        null => 1m,
        > 0 and <= 1_000_000_000m => value.Value,
        _ => throw new BusinessRuleException("RATE_INVALID", "Tỷ giá phải lớn hơn 0."),
    };
}

/// <summary>Nguồn bổ sung (monolith: ab_source, quyền AB_SOURCES) — vd Mua, Biếu tặng, Trao đổi.</summary>
public sealed class AcquisitionSource : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Nhà cung cấp (monolith: Supplier, quyền SUPPLIERS).</summary>
public sealed class Supplier : TenantEntity
{
    private Supplier() { }

    public string Name { get; private set; } = "";
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Fax { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }

    /// <summary>Mã số thuế (monolith: Mst).</summary>
    public string? TaxCode { get; private set; }

    public string? BankAccount { get; private set; }
    public string? BankName { get; private set; }
    public string? Representative { get; private set; }

    /// <summary>Chức vụ người đại diện.</summary>
    public string? Position { get; private set; }

    public static Supplier Create(SupplierData data)
    {
        var supplier = new Supplier();
        supplier.Update(data);
        return supplier;
    }

    public void Update(SupplierData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Name = TextRules.Required(data.Name, 250, "NAME_INVALID", "Tên nhà cung cấp");
        Address = TextRules.Optional(data.Address, 500, "ADDRESS_INVALID", "Địa chỉ");
        Phone = TextRules.Optional(data.Phone, 50, "PHONE_INVALID", "Điện thoại");
        Fax = TextRules.Optional(data.Fax, 50, "FAX_INVALID", "Fax");
        Email = TextRules.Optional(data.Email, 250, "EMAIL_INVALID", "Email");
        if (Email is not null && !Email.Contains('@', StringComparison.Ordinal))
            throw new BusinessRuleException("EMAIL_INVALID", $"Email '{Email}' không hợp lệ.");
        Website = TextRules.Optional(data.Website, 250, "WEBSITE_INVALID", "Website");
        TaxCode = TextRules.Optional(data.TaxCode, 50, "TAX_CODE_INVALID", "Mã số thuế");
        BankAccount = TextRules.Optional(data.BankAccount, 50, "BANK_ACCOUNT_INVALID", "Số tài khoản");
        BankName = TextRules.Optional(data.BankName, 250, "BANK_INVALID", "Ngân hàng");
        Representative = TextRules.Optional(data.Representative, 250, "REPRESENTATIVE_INVALID", "Người đại diện");
        Position = TextRules.Optional(data.Position, 250, "POSITION_INVALID", "Chức vụ");
    }
}

public sealed record SupplierData(
    string Name, string? Address = null, string? Phone = null, string? Fax = null, string? Email = null, string? Website = null, string? TaxCode = null,
    string? BankAccount = null, string? BankName = null, string? Representative = null, string? Position = null);

/// <summary>Kinh phí bổ sung theo kỳ (monolith: Budget, quyền BUDGETS). Ngừng hoạt động thì không chọn được cho đơn mới.</summary>
public sealed class Budget : TenantEntity, IHasStatus
{
    private Budget() { }

    public string Name { get; private set; } = "";

    /// <summary>Số tiền (monolith: Blance).</summary>
    public decimal? Amount { get; private set; }

    public DateOnly? StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public string? Note { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static Budget Create(string name, decimal? amount, DateOnly? startsOn, DateOnly? endsOn, string? note)
    {
        var budget = new Budget();
        budget.Update(name, amount, startsOn, endsOn, note);
        return budget;
    }

    public void Update(string name, decimal? amount, DateOnly? startsOn, DateOnly? endsOn, string? note)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên kinh phí");
        Amount = TextRules.Money(amount, "Số tiền");
        if (startsOn is { } s && endsOn is { } e && e < s)
            throw new BusinessRuleException("DATE_RANGE_INVALID", "Ngày kết thúc không được trước ngày bắt đầu.");
        (StartsOn, EndsOn) = (startsOn, endsOn);
        Note = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú");
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>Quỹ (monolith: Fund, quyền FUNDS) — thuộc một kinh phí (tuỳ chọn).</summary>
public sealed class Fund : TenantEntity
{
    private Fund() { }

    public string Name { get; private set; } = "";
    public long? BudgetId { get; private set; }

    /// <summary>Số tiền (monolith: Blane).</summary>
    public decimal? Amount { get; private set; }

    public string? Manager { get; private set; }
    public string? Purpose { get; private set; }
    public string? Note { get; private set; }

    public static Fund Create(string name, long? budgetId, decimal? amount, string? manager, string? purpose, string? note)
    {
        var fund = new Fund();
        fund.Update(name, budgetId, amount, manager, purpose, note);
        return fund;
    }

    public void Update(string name, long? budgetId, decimal? amount, string? manager, string? purpose, string? note)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên quỹ");
        BudgetId = budgetId;
        Amount = TextRules.Money(amount, "Số tiền");
        Manager = TextRules.Optional(manager, 250, "MANAGER_INVALID", "Người quản lý");
        Purpose = TextRules.Optional(purpose, 500, "PURPOSE_INVALID", "Mục đích");
        Note = TextRules.Optional(note, 500, "NOTE_INVALID", "Ghi chú");
    }
}

/// <summary>
/// Bản sao biểu ghi từ catalog (BibChanged) — chọn biểu ghi cho dòng đơn đặt / phiếu nhận và hiện nhan đề, không gọi catalog đồng bộ.
/// </summary>
public sealed class BibSnapshot : Entity, ITenantOwned
{
    public long TenantId { get; set; }
    public Guid BibPublicId { get; set; }
    public long Mfn { get; set; }
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? PublishYear { get; set; }
    public string? Isbns { get; set; }

    /// <summary>Loại biểu ghi (loại tài liệu) ở catalog — lọc "Loại tài liệu" khi tra trùng.</summary>
    public long? BibTypeId { get; set; }

    public string? BibTypeName { get; set; }

    /// <summary>Nhan đề / tác giả / nhà xuất bản viết thường, bỏ dấu (<see cref="Fold"/>) — tra trùng không phân biệt dấu như ELIB-LRC.</summary>
    public string TitleKey { get; set; } = "";

    public string AuthorKey { get; set; } = "";
    public string PublisherKey { get; set; } = "";

    /// <summary>
    /// Đã ghi từ BibChanged sau khi bản sao có loại biểu ghi + khoá tìm kiếm. False = dòng có từ trước — job đồng bộ lúc khởi động chép lại
    /// từ catalog (cả khi cùng version) rồi đặt true.
    /// </summary>
    public bool Synced { get; set; }

    /// <summary>Ngày biên mục ở catalog (null = chưa rõ) — "thư mục sách mới" sắp theo ngày này, không theo ngày chép bản sao.</summary>
    public DateTimeOffset? CatalogedAt { get; set; }

    public bool Deleted { get; set; }
    public long Version { get; set; }

    /// <summary>Chữ thường không dấu (đ → d), gộp khoảng trắng — "de men" khớp "Dế mèn" (như Marc.Fold của catalog).</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch));
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
