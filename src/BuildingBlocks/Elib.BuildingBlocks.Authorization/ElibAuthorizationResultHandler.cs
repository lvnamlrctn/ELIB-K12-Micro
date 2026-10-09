using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Elib.BuildingBlocks.Authorization;

/// <summary>Trả 401/403 dạng problem+json có <c>code</c> ổn định (PERMISSION_DENIED, MODULE_NOT_LICENSED…) để frontend dịch (docs 07 §4).</summary>
public sealed class ElibAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var reason = authorizeResult.AuthorizationFailure?.FailureReasons.OfType<ElibFailureReason>().FirstOrDefault();
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "about:blank",
                    title = "Forbidden",
                    status = 403,
                    code = reason?.Code ?? ElibErrorCodes.Forbidden,
                    detail = reason?.Message,
                },
                options: null, contentType: "application/problem+json");
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
