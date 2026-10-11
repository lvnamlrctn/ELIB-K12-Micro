using System.Security.Claims;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Elib.BuildingBlocks.Authorization;

/// <summary>
/// Endpoint quản trị nền tảng: chỉ tài khoản NHÂN VIÊN cấp hệ thống (không thuộc đơn vị). Ví dụ: tạo/khoá đơn vị, bán license.
/// Service token KHÔNG dùng được — service gọi nhau qua endpoint /internal/** với <see cref="RequireServiceCallerAttribute"/>.
/// (Nếu cho service vào đây, lộ secret của bất kỳ service nào = nắm quyền quản trị cả nền tảng.)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSystemContextAttribute() : AuthorizeAttribute(ElibPolicyNames.SystemContext);

public sealed class SystemContextRequirement : IAuthorizationRequirement;

public sealed class SystemContextHandler(ITenantContext tenant) : AuthorizationHandler<SystemContextRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SystemContextRequirement requirement)
    {
        var type = context.User.FindFirstValue(ElibClaimTypes.SubjectType) ?? ElibSubjectTypes.Staff;
        if (tenant.IsSystem && type == ElibSubjectTypes.Staff) context.Succeed(requirement);
        else context.Fail(new ElibFailureReason(this, ElibErrorCodes.SystemOnly, "Chức năng chỉ dành cho quản trị hệ thống."));
        return Task.CompletedTask;
    }
}

/// <summary>Endpoint nội bộ (/internal/**): chỉ service token (client_credentials, sub_type=service). Gateway không công bố các route này.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireServiceCallerAttribute() : AuthorizeAttribute(ElibPolicyNames.ServiceCaller);

public sealed class ServiceCallerRequirement : IAuthorizationRequirement;

public sealed class ServiceCallerHandler : AuthorizationHandler<ServiceCallerRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ServiceCallerRequirement requirement)
    {
        if (context.User.FindFirstValue(ElibClaimTypes.SubjectType) == ElibSubjectTypes.Service) context.Succeed(requirement);
        else context.Fail(new ElibFailureReason(this, ElibErrorCodes.ServiceOnly, "Endpoint nội bộ chỉ dành cho service."));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Dùng tạm khi service chưa nối tới identity: mọi kiểm tra [Permission] đều bị từ chối (fail-closed).
/// Thay bằng client gRPC của identity khi service identity sẵn sàng.
/// </summary>
public sealed class IdentityNotConnectedPermissionSource : IPermissionSource
{
    public Task<IReadOnlySet<string>> GetEffectiveAsync(long? tenantId, long userId, string? permissionStamp, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Chưa cấu hình nguồn quyền (identity) cho service này.");
}

/// <summary>
/// Cho service/gateway không dùng [RequiresModule] (gateway kiểm license bằng dữ liệu tra host; media không gắn với phân hệ):
/// mọi kiểm tra license đều trả "chưa đăng ký" (fail-closed) nếu lỡ có endpoint dùng.
/// </summary>
public sealed class NoModuleLicenseSource : IModuleLicenseSource
{
    public Task<bool> IsLicensedAsync(long tenantId, string moduleCode, CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>
/// Tên policy theo loại tài khoản — dùng cả ở route gateway ("AuthorizationPolicy": "elib-staff") lẫn endpoint của service.
/// Token bạn đọc và token nhân viên cùng ký bởi identity, cùng audience: route quản trị phải chặn theo sub_type, không chỉ "đã đăng nhập".
/// </summary>
public static class ElibPolicies
{
    public const string Staff = "elib-staff";
    public const string Reader = "elib-reader";
}

/// <summary>Chỉ tài khoản nhân viên (đơn vị hoặc hệ thống) — endpoint quản trị không gắn [Permission].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireStaffAttribute() : AuthorizeAttribute(ElibPolicyNames.Staff);

/// <summary>Chỉ bạn đọc đã đăng nhập OPAC (sub_type=reader, sub = id tài khoản bạn đọc ở identity).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireReaderAttribute() : AuthorizeAttribute(ElibPolicyNames.Reader);

public sealed class SubjectTypeRequirement(string subjectType) : IAuthorizationRequirement
{
    public string SubjectType { get; } = subjectType;
}

public sealed class SubjectTypeHandler : AuthorizationHandler<SubjectTypeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SubjectTypeRequirement requirement)
    {
        if (context.User.FindFirstValue(ElibClaimTypes.SubjectType) == requirement.SubjectType) context.Succeed(requirement);
        else context.Fail(new ElibFailureReason(this, ElibErrorCodes.PermissionDenied,
            requirement.SubjectType == ElibSubjectTypes.Reader ? "Chức năng dành cho bạn đọc đã đăng nhập." : "Chỉ tài khoản nhân viên được dùng chức năng này."));
        return Task.CompletedTask;
    }
}
