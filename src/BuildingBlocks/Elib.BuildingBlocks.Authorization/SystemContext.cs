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
