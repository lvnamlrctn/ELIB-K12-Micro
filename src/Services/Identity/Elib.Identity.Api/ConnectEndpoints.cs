using Elib.BuildingBlocks.Tenancy;
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
    /// <summary>App OPAC — chỉ phát token cho bạn đọc (phiên <see cref="IdentityCookie.ReaderScheme"/>); app khác chỉ cho nhân viên.</summary>
    public const string OpacClientId = "elib-opac";

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

    private static async Task<IResult> AuthorizeAsync(HttpContext ctx, SignInService signIn, ImpersonationService impersonation, TimeProvider clock,
        ReaderSignInService readers, ITenantContext tenant, CancellationToken ct)
    {
        var request = ctx.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Không đọc được yêu cầu OIDC.");
        if (request.ClientId == OpacClientId) return await AuthorizeReaderAsync(ctx, request, readers, tenant, ct);
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

    /// <summary>Bạn đọc: chỉ trên host của đơn vị, phiên phải cùng đơn vị với host; còn bị bắt đổi mật khẩu thì đăng nhập lại (bước đổi mật khẩu).</summary>
    private static async Task<IResult> AuthorizeReaderAsync(HttpContext ctx, OpenIddictRequest request, ReaderSignInService readers, ITenantContext tenant,
        CancellationToken ct)
    {
        if (tenant.TenantId is not { } hostTenant)
            return Results.Forbid(Error(Errors.InvalidRequest, "Bạn đọc đăng nhập trên trang tra cứu của trường."), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

        var session = await ctx.AuthenticateAsync(IdentityCookie.ReaderScheme);
        var reader = session.Succeeded && ElibPrincipals.ReadSession(session.Principal!) is (long sessionTenant, var accountId) && sessionTenant == hostTenant
            ? await readers.ReloadAsync(hostTenant, accountId, ct, ElibPrincipals.IsExternalReader(session.Principal!))
            : null;
        if (reader is null || reader.MustChangePassword)
        {
            if (session.Succeeded) await ctx.SignOutAsync(IdentityCookie.ReaderScheme);
            return ChallengeLogin(ctx, IdentityCookie.ReaderScheme);
        }
        return Results.SignIn(ElibPrincipals.ForReaderToken(reader, request.GetScopes()), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> TokenAsync(HttpContext ctx, SignInService signIn, ImpersonationService impersonation, TimeProvider clock,
        ReaderSignInService readers, CancellationToken ct)
    {
        var request = ctx.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Không đọc được yêu cầu OIDC.");

        if (request.IsClientCredentialsGrantType())
            return Results.SignIn(ElibPrincipals.ForService(request.ClientId!, request.GetScopes()), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var previous = (await ctx.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
            if (previous is not null && ElibPrincipals.IsReader(previous))
            {
                // Bạn đọc: thẻ bị khoá/xoá, đơn vị tạm ngưng, hoặc thư viện vừa đặt lại mật khẩu → không cấp token mới.
                return ElibPrincipals.ReadSession(previous) is (long readerTenant, var accountId)
                       && await readers.ReloadAsync(readerTenant, accountId, ct, ElibPrincipals.IsExternalReader(previous)) is { MustChangePassword: false } reader
                    ? Results.SignIn(ElibPrincipals.ForReaderToken(reader, previous.GetScopes()), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
                    : Results.Forbid(Error(Errors.InvalidGrant, "Tài khoản không còn hiệu lực."), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }
            // Tải lại người dùng ở MỖI lần làm mới: bị khoá/vô hiệu hoá/đơn vị tạm ngưng thì không cấp token mới; stamp quyền luôn mới nhất.
            if (previous is not null && ElibPrincipals.ReadSession(previous) is { } who
                && await ReloadAsync(previous, who, signIn, impersonation, clock, ct) is ({ } user, var impersonationExpiresAt))
            {
                return Results.SignIn(ElibPrincipals.ForUserToken(user, previous.GetScopes(), impersonationExpiresAt),
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.Forbid(Error(Errors.InvalidGrant, "Tài khoản không còn hiệu lực."), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        throw new InvalidOperationException("Grant type không được hỗ trợ.");
    }

    private static async Task<IResult> LogoutAsync(HttpContext ctx)
    {
        await ctx.SignOutAsync(IdentityCookie.Scheme);
        await ctx.SignOutAsync(IdentityCookie.ReaderScheme);
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/account/login" }, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static AuthenticationProperties Error(string error, string description) => new(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
    });

    private static IResult ChallengeLogin(HttpContext ctx, string scheme = IdentityCookie.Scheme)
    {
        var parameters = ctx.Request.HasFormContentType ? ctx.Request.Form.ToList() : ctx.Request.Query.ToList();
        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = ctx.Request.PathBase + ctx.Request.Path + QueryString.Create(parameters) },
            [scheme]);
    }
}
