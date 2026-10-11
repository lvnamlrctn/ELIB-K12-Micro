using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Identity.Application;
using Elib.Identity.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Elib.Identity.Api;

public sealed class ReaderLoginForm
{
    public string? CardNo { get; set; }
    public string? Password { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ReaderPasswordForm
{
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
}

/// <summary>
/// Đăng nhập OPAC của bạn đọc — đích chuyển hướng của /connect/authorize (client elib-opac) khi chưa có phiên bạn đọc. Chỉ trên host của
/// đơn vị (gateway gắn X-Tenant-Id có chữ ký). Mật khẩu do thư viện đặt (hoặc = số thẻ) phải đổi ngay ở bước kế tiếp, trước khi vào OPAC.
/// API của bạn đọc đã đăng nhập: /api/reader/** (gateway /api/opac/account/**, chỉ token bạn đọc).
/// </summary>
public static class ReaderAccountEndpoints
{
    /// <summary>Vé "vừa đăng nhập đúng, phải đổi mật khẩu" (mã hoá + hết hạn 10 phút) — không giữ mật khẩu cũ.</summary>
    public const string FirstPasswordCookie = "elib.identity.rpw";

    private static readonly TimeSpan FirstPasswordLifetime = TimeSpan.FromMinutes(10);
    private static readonly HtmlEncoder E = HtmlEncoder.Create(UnicodeRanges.All);

    public static IEndpointRouteBuilder MapReaderAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/account/reader-login", async (HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant, SignInService signIn, string? returnUrl,
            CancellationToken ct) =>
        {
            var host = await HostTenantAsync(tenant, signIn, ct);
            var t = IdentityTexts.For(ctx);
            return host is null ? NoTenantPage(ctx, t) : LoginPage(ctx, t, antiforgery, host, AccountEndpoints.SafeReturnUrl(returnUrl), null, null);
        }).ExcludeFromDescription();

        app.MapPost("/account/reader-login", async ([FromForm] ReaderLoginForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant,
            SignInService signIn, ReaderSignInService readers, IDataProtectionProvider protection, CancellationToken ct) =>
        {
            var host = await HostTenantAsync(tenant, signIn, ct);
            var t = IdentityTexts.For(ctx);
            if (host is null) return NoTenantPage(ctx, t);
            var returnUrl = AccountEndpoints.SafeReturnUrl(form.ReturnUrl);

            var result = await readers.LoginAsync(host.TenantId, form.CardNo, form.Password, ct);
            if (!result.Succeeded)
                return LoginPage(ctx, t, antiforgery, host, returnUrl, form.CardNo, Message(t, result.Error!.Value), StatusCodes.Status401Unauthorized);

            var who = result.Identity!;
            if (who.MustChangePassword)
            {
                var ticket = Protector(protection).Protect(
                    string.Join('|', who.TenantId.ToString(CultureInfo.InvariantCulture), who.AccountId.ToString(CultureInfo.InvariantCulture), returnUrl),
                    FirstPasswordLifetime);
                ctx.Response.Cookies.Append(FirstPasswordCookie, ticket, TicketCookieOptions(ctx));
                return Results.LocalRedirect("/account/reader-password");
            }

            await ctx.SignInAsync(IdentityCookie.ReaderScheme, ElibPrincipals.ForReaderCookie(who));
            return Results.LocalRedirect(returnUrl);
        }).ExcludeFromDescription();

        app.MapGet("/account/reader-password", (HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant, IDataProtectionProvider protection) =>
            ReadTicket(ctx, tenant, protection) is null
                ? Results.LocalRedirect("/account/reader-login")
                : PasswordPage(ctx, IdentityTexts.For(ctx), antiforgery, null)).ExcludeFromDescription();

        app.MapPost("/account/reader-password", async ([FromForm] ReaderPasswordForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant,
            IDataProtectionProvider protection, ReaderSignInService readers, CancellationToken ct) =>
        {
            if (ReadTicket(ctx, tenant, protection) is not { } ticket) return Results.LocalRedirect("/account/reader-login");
            var t = IdentityTexts.For(ctx);
            if (form.NewPassword != form.ConfirmPassword)
                return PasswordPage(ctx, t, antiforgery, t["READER_PASSWORD_MISMATCH"], StatusCodes.Status400BadRequest);
            try
            {
                await readers.ChangePasswordAsync(ticket.TenantId, ticket.AccountId, null, form.NewPassword, requireCurrent: false, ct);
            }
            catch (BusinessRuleException ex) when (ex.StatusCode == StatusCodes.Status400BadRequest)
            {
                return PasswordPage(ctx, t, antiforgery, t.Code(ex.Code) ?? ex.Message, StatusCodes.Status400BadRequest);
            }

            ctx.Response.Cookies.Delete(FirstPasswordCookie, TicketCookieOptions(ctx));
            if (await readers.ReloadAsync(ticket.TenantId, ticket.AccountId, ct) is not { } who) return Results.LocalRedirect("/account/reader-login");
            await ctx.SignInAsync(IdentityCookie.ReaderScheme, ElibPrincipals.ForReaderCookie(who));
            return Results.LocalRedirect(ticket.ReturnUrl);
        }).ExcludeFromDescription();

        var reader = app.MapGroup("/api/reader").WithTags("Reader").RequireAuthorization(new RequireReaderAttribute());
        reader.MapGet("/me", (ClaimsPrincipal principal, ReaderSignInService readers, CancellationToken ct) =>
            Session(principal) is var (tenantId, accountId) ? readers.MeAsync(tenantId, accountId, ct) : throw Forbidden());
        reader.MapPost("/me/password", async (ReaderChangePasswordRequest request, ClaimsPrincipal principal, ReaderSignInService readers, CancellationToken ct) =>
        {
            if (Session(principal) is not var (tenantId, accountId)) throw Forbidden();
            await readers.ChangePasswordAsync(tenantId, accountId, request.CurrentPassword, request.NewPassword, requireCurrent: true, ct);
            return Results.NoContent();
        });

        return app;
    }

    private sealed record FirstPasswordTicket(long TenantId, long AccountId, string ReturnUrl);

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider protection) =>
        protection.CreateProtector("Elib.Identity.ReaderFirstPassword").ToTimeLimitedDataProtector();

    /// <summary>Vé còn hạn và đúng đơn vị của host — mở link ở host khác không đổi được mật khẩu.</summary>
    private static FirstPasswordTicket? ReadTicket(HttpContext ctx, ITenantContext tenant, IDataProtectionProvider protection)
    {
        if (ctx.Request.Cookies[FirstPasswordCookie] is not { Length: > 0 } raw || tenant.TenantId is not { } host) return null;
        try
        {
            var parts = Protector(protection).Unprotect(raw).Split('|', 3);
            return parts.Length == 3
                   && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId) && tenantId == host
                   && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var accountId)
                ? new FirstPasswordTicket(tenantId, accountId, AccountEndpoints.SafeReturnUrl(parts[2]))
                : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    /// <summary>(đơn vị, id tài khoản bạn đọc) từ token — gateway đã kiểm tra token cùng đơn vị với host.</summary>
    private static (long TenantId, long AccountId)? Session(ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue(ElibClaimTypes.TenantId), NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId)
        && long.TryParse(principal.FindFirstValue(ElibClaimTypes.Subject), NumberStyles.None, CultureInfo.InvariantCulture, out var accountId)
            ? (tenantId, accountId)
            : null;

    private static BusinessRuleException Forbidden() => new(ElibErrorCodes.PermissionDenied, "Token bạn đọc không hợp lệ.", 403);

    private static CookieOptions TicketCookieOptions(HttpContext ctx) => new()
    {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/account",
        MaxAge = FirstPasswordLifetime,
    };

    private static async Task<TenantReplicaRecord?> HostTenantAsync(ITenantContext tenant, SignInService signIn, CancellationToken ct) =>
        tenant.TenantId is { } id ? await signIn.FindTenantAsync(id, ct) : null;

    private static string Message(IdentityTexts t, LoginError error) => error switch
    {
        LoginError.LockedOut => t["READER_ERR_LOCKED_OUT"],
        LoginError.AccountDisabled => t["READER_ERR_DISABLED"],
        LoginError.TenantUnavailable => t["READER_ERR_TENANT"],
        _ => t["READER_ERR_CREDENTIALS"],
    };

    private static IResult NoTenantPage(HttpContext ctx, IdentityTexts t) =>
        AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_LOGIN_TITLE"], t["READER_NO_TENANT_SUBTITLE"],
            $"""<div class="err" role="alert">{E.Encode(t["READER_NO_TENANT"])}</div>"""), StatusCodes.Status404NotFound);

    private static IResult LoginPage(HttpContext ctx, IdentityTexts t, IAntiforgery antiforgery, TenantReplicaRecord host, string returnUrl, string? cardNo, string? error,
        int status = 200)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var body = $$"""
            <form method="post" action="/account/reader-login">
              {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{E.Encode(error)}</div>")}}
              <input type="hidden" name="{{E.Encode(tokens.FormFieldName)}}" value="{{E.Encode(tokens.RequestToken ?? "")}}">
              <input type="hidden" name="returnUrl" value="{{E.Encode(returnUrl)}}">
              <div class="unit">{{AccountEndpoints.UnitIcon}}<span>{{E.Encode(host.Name)}}</span></div>
              <div class="field">
                <label for="cardNo">{{E.Encode(t["READER_CARD"])}}</label>
                <div class="box">{{AccountEndpoints.PersonIcon}}<input id="cardNo" name="cardNo" value="{{E.Encode(cardNo ?? "")}}" autocomplete="username" placeholder="{{E.Encode(t["READER_CARD_PLACEHOLDER"])}}" required autofocus></div>
              </div>
              <div class="field">
                <label for="password">{{E.Encode(t["PASSWORD"])}}</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="password" name="password" type="password" autocomplete="current-password" placeholder="{{E.Encode(t["PASSWORD"])}}" required></div>
                <div class="hint">{{E.Encode(t["READER_HINT"])}}</div>
              </div>
              <button type="submit">{{t["SIGN_IN_BTN"]}}</button>
            </form>
            """;
        return AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_LOGIN_TITLE"], t["READER_LOGIN_SUBTITLE"], body), status);
    }

    private static IResult PasswordPage(HttpContext ctx, IdentityTexts t, IAntiforgery antiforgery, string? error, int status = 200)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var body = $$"""
            <form method="post" action="/account/reader-password">
              {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{E.Encode(error)}</div>")}}
              <input type="hidden" name="{{E.Encode(tokens.FormFieldName)}}" value="{{E.Encode(tokens.RequestToken ?? "")}}">
              <div class="field">
                <label for="newPassword">{{E.Encode(t["READER_NEW_PASSWORD"])}}</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="newPassword" name="newPassword" type="password" autocomplete="new-password" minlength="{{Domain.ReaderAccount.MinPasswordLength}}" required autofocus></div>
                <div class="hint">{{E.Encode(t.Format("READER_NEW_PASSWORD_HINT", Domain.ReaderAccount.MinPasswordLength))}}</div>
              </div>
              <div class="field">
                <label for="confirmPassword">{{E.Encode(t["READER_CONFIRM_PASSWORD"])}}</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="confirmPassword" name="confirmPassword" type="password" autocomplete="new-password" required></div>
              </div>
              <button type="submit">{{t["READER_CHANGE_BTN"]}}</button>
            </form>
            """;
        return AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_NEW_PASSWORD_TITLE"], t["READER_NEW_PASSWORD_SUBTITLE"], body), status);
    }
}
