using System.Globalization;
using System.Security.Claims;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.Authorization;

/// <summary>
/// Hỏi quyền của người đang thao tác ngay trong nghiệp vụ (không qua attribute) — dùng cho phê duyệt theo thẩm quyền:
/// cùng một endpoint, người có quyền duyệt áp dụng ngay, người khác chỉ tạo yêu cầu. Quy tắc giống <see cref="PermissionHandler"/>:
/// chỉ nhân viên; đóng vai đơn vị (chỉ đọc) và lỗi lấy quyền đều tính là KHÔNG có quyền.
/// </summary>
public interface IActorPermissions
{
    /// <summary>Mã quyền dạng <c>MODULE:action</c>.</summary>
    Task<bool> HasAsync(string code, CancellationToken cancellationToken);
}

public sealed partial class ActorPermissions(
    IHttpContextAccessor http, IPermissionChecker checker, ITenantContext tenant, ILogger<ActorPermissions> logger) : IActorPermissions
{
    public async Task<bool> HasAsync(string code, CancellationToken cancellationToken)
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return false;
        if (user.FindFirstValue(ElibClaimTypes.SubjectType) is { } type && type != ElibSubjectTypes.Staff) return false;
        if (user.FindFirstValue(ElibClaimTypes.Impersonation) is not null) return false;
        if (!long.TryParse(user.FindFirstValue(ElibClaimTypes.Subject), NumberStyles.None, CultureInfo.InvariantCulture, out var userId)) return false;
        try
        {
            return await checker.HasAnyAsync(tenant.IsSystem ? null : tenant.TenantId, userId, user.FindFirstValue(ElibClaimTypes.PermissionStamp), [code], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, userId);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Không lấy được quyền của người dùng {UserId}; coi như không có quyền")]
    private static partial void LogFailed(ILogger logger, Exception exception, long userId);
}
