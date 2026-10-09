using Elib.Tenant.Domain;

namespace Elib.Tenant.Application;

public sealed record LicenseInput(string ModuleCode, LicenseStatus Status = LicenseStatus.Active, DateOnly? ValidFrom = null, DateOnly? ValidTo = null);

public sealed record CreateTenantRequest(
    string Code, string Name, string Subdomain, string? TimeZone, long? ParentOrgId, IReadOnlyList<LicenseInput> Licenses);

public sealed record UpdateTenantRequest(string Name, string Subdomain, string? TimeZone, long? ParentOrgId);

public sealed record SetLicensesRequest(IReadOnlyList<LicenseInput> Licenses);

public sealed record LicenseDto(string ModuleCode, string ModuleName, LicenseStatus Status, DateOnly? ValidFrom, DateOnly? ValidTo, bool EffectiveToday);

public sealed record ProvisioningStepDto(string Service, StepStatus Status, string? Error, DateTimeOffset UpdatedAt);

public sealed record TenantSummaryDto(Guid PublicId, long Id, string Code, string Name, string Subdomain, TenantState Status);

public sealed record TenantDto(
    Guid PublicId, long Id, string Code, string Name, string Subdomain, string TimeZone, long? ParentOrgId,
    TenantState Status, long Version, DateTimeOffset? ProvisioningStartedAt, string? ProvisioningError,
    IReadOnlyList<ProvisioningStepDto> ProvisioningSteps, IReadOnlyList<LicenseDto> Licenses, string? LogoUrl, string? LogoText);

/// <summary>logoUrl: url file đã upload qua media (purpose tenant-logo) hoặc null để bỏ logo.</summary>
public sealed record SetBrandingRequest(string? LogoText, string? LogoUrl);

public sealed record ModuleDto(string Code, string Package, string Name, string Service, IReadOnlyList<string> DependsOn);

/// <summary>Trả cho OPAC/Admin để ẩn/hiện chức năng (docs 08 §6). Đơn vị bị khoá → không có module nào.</summary>
public sealed record FeaturesDto(long TenantId, string Code, string Name, TenantState Status, IReadOnlyList<string> Modules, string? LogoUrl, string? LogoText);

/// <summary>Gateway dùng để ánh xạ host → đơn vị (docs 03 §2).</summary>
public sealed record TenantHostDto(long TenantId, string Code, string Subdomain, TenantState Status, IReadOnlyList<string> Modules);
