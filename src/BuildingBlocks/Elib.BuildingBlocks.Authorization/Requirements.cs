using System.Globalization;
using System.Security.Claims;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.Authorization;

public sealed class PermissionRequirement(IReadOnlyCollection<string> codes) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> Codes { get; } = codes;
}

public sealed class ModuleRequirement(string moduleCode) : IAuthorizationRequirement
{
    public string ModuleCode { get; } = moduleCode;
}

/// <summary>Lý do thất bại mang mã lỗi ổn định để <see cref="ElibAuthorizationResultHandler"/> trả problem+json.</summary>
public sealed class ElibFailureReason(IAuthorizationHandler handler, string code, string message)
    : AuthorizationFailureReason(handler, message)
{
    public string Code { get; } = code;
}

public sealed partial class PermissionHandler(IPermissionChecker checker, ITenantContext tenant, ILogger<PermissionHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true) return;

        if (user.FindFirstValue(ElibClaimTypes.SubjectType) is { } type && type != ElibSubjectTypes.Staff
            || !long.TryParse(user.FindFirstValue(ElibClaimTypes.Subject), NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            context.Fail(new ElibFailureReason(this, ElibErrorCodes.PermissionDenied, "Chỉ tài khoản nhân viên được dùng chức năng này."));
            return;
        }

        // Đóng vai đơn vị (chỉ đọc): quyền "view" của mọi module, không gì khác — không hỏi identity (sub là tài khoản hệ thống).
        if (user.FindFirstValue(ElibClaimTypes.Impersonation) is { } impersonation)
        {
            if (impersonation == ImpersonationModes.ReadOnly && requirement.Codes.Any(c => c.EndsWith(":view", StringComparison.Ordinal)))
                context.Succeed(requirement);
            else
                context.Fail(new ElibFailureReason(this, ElibErrorCodes.ImpersonationReadOnly,
                    "Đang xem đơn vị với vai quản trị nền tảng (chỉ đọc) — không thực hiện được thao tác này."));
            return;
        }

        bool allowed;
        try
        {
            allowed = await checker.HasAnyAsync(tenant.IsSystem ? null : tenant.TenantId, userId, user.FindFirstValue(ElibClaimTypes.PermissionStamp), requirement.Codes, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-closed: không lấy được quyền thì từ chối, không cho qua.
            LogSourceFailed(logger, ex, userId);
            allowed = false;
        }

        if (allowed) context.Succeed(requirement);
        else context.Fail(new ElibFailureReason(this, ElibErrorCodes.PermissionDenied,
            $"Thiếu quyền: {string.Join(" hoặc ", requirement.Codes)}."));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Không lấy được quyền của người dùng {UserId}; từ chối truy cập")]
    private static partial void LogSourceFailed(ILogger logger, Exception exception, long userId);
}

public sealed class ModuleHandler(ITenantContext tenant, IModuleLicenseSource licenses) : AuthorizationHandler<ModuleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ModuleRequirement requirement)
    {
        // Ngữ cảnh hệ thống (super-admin quản trị nền tảng) không bị giới hạn bởi license của một đơn vị.
        if (tenant.IsSystem)
        {
            context.Succeed(requirement);
            return;
        }

        if (tenant.TenantId is { } tenantId
            && await licenses.IsLicensedAsync(tenantId, requirement.ModuleCode, CancellationToken.None))
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new ElibFailureReason(this, ElibErrorCodes.ModuleNotLicensed,
            $"Đơn vị chưa đăng ký phân hệ {requirement.ModuleCode}."));
    }
}
