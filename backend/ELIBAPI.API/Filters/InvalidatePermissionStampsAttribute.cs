using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Filters;

/// <summary>
/// Gắn lên controller mà thao tác ghi làm đổi kết quả tra quyền của NHIỀU user cùng lúc (cms.Module: mã, xoá, ẩn;
/// cms.Roles: mã role quyết định bypass toàn quyền/chỉ-xem). Sau mỗi request ghi thành công (khác GET, mã 2xx) đặt
/// <c>Users.PermissionStamp = null</c> cho mọi user bằng 1 câu lệnh: claim JWT "perm" đang lưu hành bị coi là cũ
/// (<see cref="PermissionClaimEvaluator"/> fallback về DB), lần đăng nhập kế tiếp sinh stamp mới. Bổ sung so với ELIB-LRC
/// (bản LRC chỉ làm mới stamp khi lưu quyền / đổi role của 1 user).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InvalidatePermissionStampsAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || executed.Exception != null) return;

        var status = executed.Result switch
        {
            IStatusCodeActionResult { StatusCode: int code } => code,
            ObjectResult o => o.StatusCode ?? 200,
            _ => 200
        };
        if (status is < 200 or >= 300) return;

        var db = context.HttpContext.RequestServices.GetRequiredService<ELIBAPIDbContext>();
        await db.Users.Where(u => u.PermissionStamp != null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PermissionStamp, (string?)null));
    }
}
