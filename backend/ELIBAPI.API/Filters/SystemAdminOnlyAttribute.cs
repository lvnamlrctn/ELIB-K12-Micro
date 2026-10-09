using System.Security.Claims;
using ELIBAPI.Core.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Filters;

/// <summary>
/// Đợt 18 — chỉ tài khoản hệ thống (JWT không có claim <c>TenantId</c>) được gọi. Dùng cho thao tác ảnh hưởng
/// MỌI đơn vị (dựng lại index gộp dùng chung, dọn lịch sử AdminTask toàn hệ thống...) — quyền module thôi
/// là chưa đủ vì admin của 1 đơn vị cũng có thể được cấp cùng module. Dùng kèm <see cref="PermissionAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class SystemAdminOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return; // [Authorize]/[Permission] tự trả 401

        if (!string.IsNullOrEmpty(user.FindFirstValue("TenantId")))
        {
            var localizer = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            context.Result = new ObjectResult(ApiResponse<object>.Fail(localizer["Forbidden"], 403))
            { StatusCode = 403 };
        }
    }
}
