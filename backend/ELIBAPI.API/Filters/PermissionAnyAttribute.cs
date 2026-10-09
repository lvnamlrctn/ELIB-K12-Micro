using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ELIBAPI.API;

namespace ELIBAPI.API.Filters;

/// <summary>
/// Giống <see cref="PermissionAttribute"/> nhưng nhận nhiều mã module và chỉ cần THOẢ MỘT là qua (OR).
/// Dùng cho các endpoint từ điển dùng chung: người biên mục / lập đơn đặt / lập phiếu nhập đều cần đọc
/// cùng một danh mục, nhưng không ai được cấp riêng quyền trên module từ điển đó.
///
/// Mỗi tham số có dạng "MODULE_CODE:action", ví dụ "CATALOG_BIBS:edit". Thiếu ":action" thì mặc định "view".
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PermissionAnyAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly (string ModuleCode, string Action)[] _specs;

    public PermissionAnyAttribute(params string[] specs)
    {
        _specs = specs.Select(s =>
        {
            var parts = s.Split(':', 2);
            return (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : "view");
        }).ToArray();
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var localizer = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();

        if (!long.TryParse(userIdClaim, out var userId))
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(localizer["Unauthorized"], 401))
            { StatusCode = 401 };
            return;
        }

        var permService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();

        foreach (var (moduleCode, action) in _specs)
        {
            var fastResult = await PermissionClaimEvaluator.TryEvaluateAsync(
                context.HttpContext.User, permService, userId, moduleCode, action);
            if (fastResult ?? await permService.HasPermissionAsync(userId, moduleCode, action))
                return;
        }

        context.Result = new ObjectResult(ApiResponse<object>.Fail(localizer["Forbidden"], 403))
        { StatusCode = 403 };
    }
}
