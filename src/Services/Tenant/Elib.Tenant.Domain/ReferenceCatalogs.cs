using Elib.BuildingBlocks.Domain;

namespace Elib.Tenant.Domain;

/// <summary>Quốc tịch (monolith: dbo.Nation).</summary>
public sealed class Nationality : NamedCatalogItem
{
    public override int MaxNameLength => 150;
}

/// <summary>Dân tộc (monolith: dbo.Ethenic).</summary>
public sealed class Ethnicity : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Học hàm học vị — GS, PGS… (monolith: dbo.Prof; admin cũ gọi "Học hàm học vị").</summary>
public sealed class AcademicTitle : NamedCatalogItem
{
    public override int MaxNameLength => 250;
}

/// <summary>Trình độ (monolith: dbo.Degree).</summary>
public sealed class Degree : NamedCatalogItem
{
    public override int MaxNameLength => 150;
}

/// <summary>Chức vụ (monolith: dbo.ChucVu).</summary>
public sealed class Position : NamedCatalogItem
{
    public override int MaxNameLength => 100;
}

/// <summary>Tiền tệ (monolith: dbo.Currency). Mã duy nhất trong đơn vị.</summary>
public sealed class Currency : TenantEntity, IHasStatus
{
    private Currency() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";

    /// <summary>Quy ra VND.</summary>
    public decimal ExchangeRate { get; private set; }

    public int Status { get; private set; } = IHasStatus.Active;

    public static Currency Create(string code, string name, decimal exchangeRate, int? status)
    {
        var currency = new Currency();
        currency.Update(code, name, exchangeRate, status);
        return currency;
    }

    public void Update(string code, string name, decimal exchangeRate, int? status)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        Code = c.Length is > 0 and <= 10 ? c : throw new BusinessRuleException("CURRENCY_CODE_INVALID", "Mã tiền tệ bắt buộc, tối đa 10 ký tự.");
        var n = (name ?? "").Trim();
        Name = n.Length is > 0 and <= 150 ? n : throw new BusinessRuleException("NAME_INVALID", "Tên bắt buộc, tối đa 150 ký tự.");
        ExchangeRate = exchangeRate > 0 ? exchangeRate : throw new BusinessRuleException("EXCHANGE_RATE_INVALID", "Tỷ giá phải lớn hơn 0.");
        if (status is not null) ChangeStatus(status.Value);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>Bộ danh mục mặc định chép cho đơn vị mới.</summary>
public static class ReferenceDefaults
{
    public static IReadOnlyList<string> Nationalities { get; } = ["Việt Nam"];

    /// <summary>54 dân tộc Việt Nam theo danh mục của Tổng cục Thống kê, cộng "Người nước ngoài".</summary>
    public static IReadOnlyList<string> Ethnicities { get; } =
    [
        "Kinh", "Tày", "Thái", "Mường", "Khmer", "Hoa", "Nùng", "Mông", "Dao", "Gia Rai", "Ê Đê", "Ba Na", "Sán Chay",
        "Chăm", "Cơ Ho", "Xơ Đăng", "Sán Dìu", "Hrê", "Ra Glai", "Mnông", "Thổ", "Stiêng", "Khơ mú", "Bru - Vân Kiều",
        "Cơ Tu", "Giáy", "Tà Ôi", "Mạ", "Giẻ-Triêng", "Co", "Chơ Ro", "Xinh Mun", "Hà Nhì", "Chu Ru", "Lào", "La Chí",
        "Kháng", "Phù Lá", "La Hủ", "La Ha", "Pà Thẻn", "Lự", "Ngái", "Chứt", "Lô Lô", "Mảng", "Cơ Lao", "Bố Y", "Cống",
        "Si La", "Pu Péo", "Rơ Măm", "Brâu", "Ơ Đu", "Người nước ngoài",
    ];

    public static IReadOnlyList<string> Degrees { get; } = ["Tiến sĩ", "Thạc sĩ", "Đại học", "Cao đẳng", "Trung cấp", "Khác"];

    public static IReadOnlyList<string> Positions { get; } =
        ["Hiệu trưởng", "Phó Hiệu trưởng", "Tổ trưởng chuyên môn", "Giáo viên", "Nhân viên thư viện", "Nhân viên"];

    /// <summary>Như migration SeedCurrency của monolith.</summary>
    public static IReadOnlyList<(string Code, string Name, decimal Rate)> Currencies { get; } =
        [("VND", "Việt Nam Đồng", 1m), ("USD", "Đô la Mỹ", 25000m), ("EUR", "Euro", 27000m)];
}
