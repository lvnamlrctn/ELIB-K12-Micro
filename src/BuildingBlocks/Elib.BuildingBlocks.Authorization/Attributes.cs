using Microsoft.AspNetCore.Authorization;

namespace Elib.BuildingBlocks.Authorization;

/// <summary>
/// Yêu cầu quyền <c>MODULE:action</c> — giữ nguyên cách dùng của monolith: <c>[Permission("NEWS", "add")]</c>.
/// Chỉ áp dụng cho nhân viên (sub_type=staff).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAttribute(string module, string action)
    : AuthorizeAttribute(ElibPolicyNames.Permission(PermissionCodes.Of(module, action)))
{
    public string Code { get; } = PermissionCodes.Of(module, action);
}

/// <summary>Đạt nếu có ÍT NHẤT một quyền trong danh sách. Mỗi phần tử dạng "MODULE:action".</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAnyAttribute(params string[] codes)
    : AuthorizeAttribute(ElibPolicyNames.PermissionAny(codes))
{
}

/// <summary>Endpoint chỉ dùng được khi đơn vị đã mua module (lớp thứ hai sau gateway — docs 04 §3).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresModuleAttribute(string moduleCode)
    : AuthorizeAttribute(ElibPolicyNames.Module(moduleCode))
{
    public string ModuleCode { get; } = moduleCode.Trim().ToUpperInvariant();
}

internal static class ElibPolicyNames
{
    public const string PermissionPrefix = "elib-perm:";
    public const string PermissionAnyPrefix = "elib-perm-any:";
    public const string ModulePrefix = "elib-module:";
    public const string SystemContext = "elib-system";
    public const string ServiceCaller = "elib-service";

    public static string Permission(string code) => PermissionPrefix + code;

    public static string PermissionAny(string[] codes)
    {
        if (codes.Length == 0) throw new ArgumentException("Cần ít nhất một mã quyền.", nameof(codes));
        return PermissionAnyPrefix + string.Join('|', codes.Select(c =>
        {
            var parts = c.Split(':', 2);
            if (parts.Length != 2) throw new ArgumentException($"Mã quyền '{c}' phải có dạng MODULE:action.", nameof(codes));
            return PermissionCodes.Of(parts[0], parts[1]);
        }));
    }

    public static string Module(string moduleCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);
        return ModulePrefix + moduleCode.Trim().ToUpperInvariant();
    }
}
