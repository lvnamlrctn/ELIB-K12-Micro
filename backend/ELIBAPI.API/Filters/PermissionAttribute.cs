using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ELIBAPI.API;

namespace ELIBAPI.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _moduleCode;
    private readonly string _action;

    public PermissionAttribute(string moduleCode, string action)
    {
        _moduleCode = moduleCode;
        _action     = action;
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
        var fastResult = await PermissionClaimEvaluator.TryEvaluateAsync(
            context.HttpContext.User, permService, userId, _moduleCode, _action);
        var hasPermission = fastResult ?? await permService.HasPermissionAsync(userId, _moduleCode, _action);

        if (!hasPermission)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail(localizer["Forbidden"], 403))
            { StatusCode = 403 };
        }
    }
}
