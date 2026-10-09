using Elib.Identity.Application;
using Elib.Identity.Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Elib.Identity.Api;

/// <summary>Endpoint OIDC (passthrough): OpenIddict kiểm tra request, ở đây chỉ quyết định AI được phát token với claim gì.</summary>
public static class ConnectEndpoints
{
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], (Delegate)AuthorizeAsync).ExcludeFromDescription().DisableAntiforgery();
        app.MapPost("/connect/token", (Delegate)TokenAsync).ExcludeFromDescription().DisableAntiforgery();
        app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], (Delegate)LogoutAsync).ExcludeFromDescription().DisableAntiforgery();
        return app;
    }

    /// <summary>Tải lại người dùng của phiên: phiên đóng vai đơn vị còn hạn → tài khoản hệ thống + đơn vị; còn lại → người dùng thường.</summary>
    private static async Task<(SignInIdentity? User, DateTimeOffset? ImpersonationExpiresAt)> ReloadAsync(
        System.Security.Claims.ClaimsPrincipal principal, (long? TenantId, long UserId) who, SignInService signIn,
        ImpersonationService impersonation, TimeProvider clock, CancellationToken ct)
    {
        if (ElibPrincipals.ImpersonationExpiry(principal) is not { } expiresAt)
            return (await signIn.ReloadAsync(who.TenantId, who.UserId, ct), null);
        if (clock.GetUtcNow() >= expiresAt || who.TenantId is not { } tenantId) return (null, null);
        return (await impersonation.ReloadAsync(tenantId, who.UserId, ct), expiresAt);
    }

    private static async Task<IResult> AuthorizeAsync(HttpContext ctx, SignInService signIn, ImpersonationService impersonation, TimeProvider clock, CancellationToken ct)
    {
        var request = ctx.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Không đọc được yêu cầu OIDC.");
        var requestedTenant = (string?)request["tenant"];

        var session = await ctx.AuthenticateAsync(IdentityCookie.Scheme);
        if (session.Succeeded && !ElibPrincipals.MatchesTenant(session.Principal!, requestedTenant))
        {
            // Đang đăng nhập đơn vị khác → bắt đăng nhập lại đúng đơn vị.
            await ctx.SignOutAsync(IdentityCookie.Scheme);
            return ChallengeLogin(ctx);
        }
        if (!session.Succeeded || ElibPrincipals.ReadSession(session.Principal!) is not { } who) return ChallengeLogin(ctx);

        var (user, impersonationExpiresAt) = await ReloadAsync(session.Principal!, who, signIn, impersonation, clock, ct);
        if (user is null)
        {
            await ctx.SignOutAsync(IdentityCookie.Scheme);
            return ChallengeLogin(ctx);
        }

        return Results.SignIn(ElibPrincipals.ForUserToken(user, request.GetScopes(), impersonationExpiresAt),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(HttpContext ctx, SignInService signIn, ImpersonationService impersonation, TimeProvider clock, CancellationToken ct)
    {
        var request = ctx.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Không đọc được yêu cầu OIDC.");

        if (request.IsClientCredentialsGrantType())
            return Results.SignIn(ElibPrincipals.ForService(request.ClientId!, request.GetScopes()), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var previous = (await ctx.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
            // Tải lại người dùng ở MỖI lần làm mới: bị khoá/vô hiệu hoá/đơn vị tạm ngưng thì không cấp token mới; stamp quyền luôn mới nhất.
            if (previous is not null && ElibPrincipals.ReadSession(previous) is { } who
                && await ReloadAsync(previous, who, signIn, impersonation, clock, ct) is ({ } user, var impersonationExpiresAt))
            {
                return Results.SignIn(ElibPrincipals.ForUserToken(user, previous.GetScopes(), impersonationExpiresAt),
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Tài khoản không còn hiệu lực.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        throw new InvalidOperationException("Grant type không được hỗ trợ.");
    }

    private static async Task<IResult> LogoutAsync(HttpContext ctx)
    {
        await ctx.SignOutAsync(IdentityCookie.Scheme);
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/account/login" }, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static IResult ChallengeLogin(HttpContext ctx)
    {
        var parameters = ctx.Request.HasFormContentType ? ctx.Request.Form.ToList() : ctx.Request.Query.ToList();
        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = ctx.Request.PathBase + ctx.Request.Path + QueryString.Create(parameters) },
            [IdentityCookie.Scheme]);
    }
}
