namespace Elib.BuildingBlocks.Authorization;

/// <summary>
/// Nguồn quyền hiệu lực của một người dùng — service identity cung cấp (docs 05 §2).
/// <paramref name="tenantId"/> null = tài khoản cấp hệ thống (bảng khác với nhân viên đơn vị, id có thể trùng).
/// Trả về tập mã dạng <c>MODULE:action</c>; chứa <see cref="PermissionCodes.All"/> nghĩa là toàn quyền.
/// </summary>
public interface IPermissionSource
{
    Task<IReadOnlySet<string>> GetEffectiveAsync(long? tenantId, long userId, string? permissionStamp, CancellationToken cancellationToken);
}

/// <summary>
/// Kiểm tra đơn vị có license module hay không — mỗi service cài đặt từ bảng TenantReplica (docs 04 §3).
/// </summary>
public interface IModuleLicenseSource
{
    Task<bool> IsLicensedAsync(long tenantId, string moduleCode, CancellationToken cancellationToken);
}

public static class PermissionCodes
{
    public const string All = "*";

    /// <summary>Chuẩn hoá: MODULE viết hoa, action viết thường — khớp ngữ nghĩa [Permission("MODULE","action")] của monolith.</summary>
    public static string Of(string module, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return $"{module.Trim().ToUpperInvariant()}:{action.Trim().ToLowerInvariant()}";
    }
}

public static class ElibErrorCodes
{
    public const string Forbidden = "FORBIDDEN";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string ModuleNotLicensed = "MODULE_NOT_LICENSED";
    public const string SystemOnly = "SYSTEM_ONLY";
    public const string ServiceOnly = "SERVICE_ONLY";
    public const string ImpersonationReadOnly = "IMPERSONATION_READONLY";
}
