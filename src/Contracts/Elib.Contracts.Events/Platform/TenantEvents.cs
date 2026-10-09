namespace Elib.Contracts.Events.Platform;

/// <summary>Trạng thái vòng đời đơn vị.</summary>
public enum TenantStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    ProvisioningFailed = 3,
}

/// <summary>License một module của đơn vị (docs 04 §3).</summary>
public sealed record ModuleLicense(string ModuleCode, string Status, DateOnly? ValidFrom, DateOnly? ValidTo);

/// <summary>Đơn vị mới được tạo — mở đầu saga khởi tạo: service thuộc module được mua seed dữ liệu mặc định rồi trả <see cref="TenantSeeded"/>.</summary>
public sealed record TenantProvisioned : IntegrationEvent
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Subdomain { get; init; }
    public string TimeZone { get; init; } = "Asia/Ho_Chi_Minh";
    public long? ParentOrgId { get; init; }
    public required IReadOnlyList<ModuleLicense> Modules { get; init; }
}

/// <summary>Một service đã seed xong cho đơn vị (idempotent — gửi lại khi seed lại).</summary>
public sealed record TenantSeeded : IntegrationEvent
{
    public required string Service { get; init; }
    public bool Succeeded { get; init; } = true;
    public string? Error { get; init; }
}

public sealed record TenantUpdated : IntegrationEvent
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Subdomain { get; init; }
    public required string TimeZone { get; init; }
    public long? ParentOrgId { get; init; }
    public required TenantStatus Status { get; init; }

    /// <summary>Số tăng dần do service tenant gán — bản sao chỉ ghi đè khi version mới hơn.</summary>
    public required long SourceVersion { get; init; }
}

public sealed record TenantSuspended : IntegrationEvent
{
    public string? Reason { get; init; }
}

/// <summary>Toàn bộ license hiện tại của đơn vị (không phải phần chênh lệch) — consumer thay thế nguyên tập.</summary>
public sealed record ModuleLicenseChanged : IntegrationEvent
{
    public required IReadOnlyList<ModuleLicense> Modules { get; init; }
    public required long SourceVersion { get; init; }
}

public sealed record SystemParameterChanged : IntegrationEvent
{
    public required string Service { get; init; }
    public required IReadOnlyList<string> Keys { get; init; }
}
