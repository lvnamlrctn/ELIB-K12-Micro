using Elib.BuildingBlocks.Domain;

namespace Elib.Tenant.Domain;

public enum LicenseStatus
{
    Trial = 0,
    Active = 1,
    Suspended = 2,
    Expired = 3,
}

/// <summary>Module bán được và service sở hữu nó (docs 02 §2). Danh mục cố định, seed bằng migration.</summary>
public sealed class ModuleDefinition
{
    private ModuleDefinition() { }

    public ModuleDefinition(string code, string package, string name, string service, int sortOrder, params string[] dependsOn)
    {
        Code = code;
        Package = package;
        Name = name;
        Service = service;
        SortOrder = sortOrder;
        DependsOn = dependsOn;
    }

    public string Code { get; private set; } = "";
    public string Package { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Service { get; private set; } = "";
    public int SortOrder { get; private set; }
    public string[] DependsOn { get; private set; } = [];
}

public static class ModuleCatalog
{
    public const string PrintPackage = "PRINT";
    public const string DigitalPackage = "DIGITAL";
    public const string SearchPackage = "SEARCH";
    public const string ExtensionPackage = "EXTENSION";
    public const string K12Package = "K12";

    public static IReadOnlyList<ModuleDefinition> All { get; } =
    [
        new("CATALOG", PrintPackage, "Biên mục", "catalog", 10),
        new("HOLDINGS", PrintPackage, "Quản lý kho", "holdings", 20, "CATALOG"),
        new("CIRCULATION", PrintPackage, "Lưu thông", "circulation", 30, "CATALOG", "HOLDINGS"),
        new("ACQUISITION", PrintPackage, "Bổ sung", "acquisition", 40, "CATALOG", "HOLDINGS"),
        new("SERIALS", PrintPackage, "Ấn phẩm định kỳ", "serials", 50, "CATALOG", "HOLDINGS"),
        new("DIGITAL", DigitalPackage, "Thư viện số", "digital", 60),
        new("SEARCH", SearchPackage, "Tra cứu OPAC", "search", 70),
        new("PAYMENT", ExtensionPackage, "Thanh toán trực tuyến", "payment", 80),
        new("SPACE", ExtensionPackage, "Không gian & đặt phòng", "space", 90),
        new("AI", ExtensionPackage, "Trợ lý AI", "ai", 100, "SEARCH"),
        new("PORTAL", ExtensionPackage, "Cổng thông tin (CMS)", "portal", 110),
        new("REPORTING", ExtensionPackage, "Báo cáo tổng hợp", "reporting", 120),
        new("SCHOOL", K12Package, "Chương trình, EOffice, Huy hiệu", "school", 130),
    ];

    /// <summary>Service nền tảng có mặt ở mọi đơn vị (không bán riêng) — cũng phải seed khi khởi tạo.</summary>
    public static IReadOnlyList<string> PlatformServices { get; } = ["identity", "patron", "notification"];

    public static ModuleDefinition Get(string code) =>
        All.FirstOrDefault(m => m.Code == code)
        ?? throw new BusinessRuleException("MODULE_UNKNOWN", $"Không có module '{code}'.");

    /// <summary>Kiểm tra mã hợp lệ và đủ module phụ thuộc. Trả về mã đã chuẩn hoá.</summary>
    public static IReadOnlyList<string> ValidateSelection(IEnumerable<string> moduleCodes)
    {
        var selected = moduleCodes.Select(c => (c ?? "").Trim().ToUpperInvariant()).Distinct().ToList();
        foreach (var code in selected) Get(code);

        var missing = selected
            .SelectMany(c => Get(c).DependsOn.Where(d => !selected.Contains(d)).Select(d => $"{c} cần {d}"))
            .ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException("MODULE_DEPENDENCY_MISSING", "Thiếu module phụ thuộc: " + string.Join("; ", missing) + ".");

        return selected;
    }
}

/// <summary>License một module của đơn vị. Thuộc đơn vị (RLS) — nhưng chỉ quản trị hệ thống được ghi.</summary>
public sealed class TenantModuleLicense : TenantEntity
{
    private TenantModuleLicense() { }

    public TenantModuleLicense(long tenantId, string moduleCode, LicenseStatus status, DateOnly? validFrom, DateOnly? validTo)
    {
        TenantId = tenantId;
        ModuleCode = moduleCode;
        Set(status, validFrom, validTo);
    }

    public string ModuleCode { get; private set; } = "";
    public LicenseStatus Status { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }

    public void Set(LicenseStatus status, DateOnly? validFrom, DateOnly? validTo)
    {
        if (validFrom is not null && validTo is not null && validTo < validFrom)
            throw new BusinessRuleException("LICENSE_PERIOD_INVALID", $"Ngày hết hạn của {ModuleCode} trước ngày bắt đầu.");
        Status = status;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    /// <summary>Đang có hiệu lực vào ngày <paramref name="today"/> (theo múi giờ đơn vị).</summary>
    public bool IsEffectiveOn(DateOnly today) =>
        Status is LicenseStatus.Active or LicenseStatus.Trial
        && (ValidFrom is null || ValidFrom <= today)
        && (ValidTo is null || today <= ValidTo);
}
