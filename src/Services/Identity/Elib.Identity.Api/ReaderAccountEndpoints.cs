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
            return host is null ? NoTenantPage(ctx) : LoginPage(ctx, antiforgery, host, AccountEndpoints.SafeReturnUrl(returnUrl), null, null);
        }).ExcludeFromDescription();

        app.MapPost("/account/reader-login", async ([FromForm] ReaderLoginForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant,
            SignInService signIn, ReaderSignInService readers, IDataProtectionProvider protection, CancellationToken ct) =>
        {
            var host = await HostTenantAsync(tenant, signIn, ct);
            if (host is null) return NoTenantPage(ctx);
            var returnUrl = AccountEndpoints.SafeReturnUrl(form.ReturnUrl);

            var result = await readers.LoginAsync(host.TenantId, form.CardNo, form.Password, ct);
            if (!result.Succeeded)
                return LoginPage(ctx, antiforgery, host, returnUrl, form.CardNo, Message(result.Error!.Value), StatusCodes.Status401Unauthorized);

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
                : PasswordPage(ctx, antiforgery, null)).ExcludeFromDescription();

        app.MapPost("/account/reader-password", async ([FromForm] ReaderPasswordForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant,
            IDataProtectionProvider protection, ReaderSignInService readers, CancellationToken ct) =>
        {
            if (ReadTicket(ctx, tenant, protection) is not { } ticket) return Results.LocalRedirect("/account/reader-login");
            if (form.NewPassword != form.ConfirmPassword)
                return PasswordPage(ctx, antiforgery, "Hai lần nhập mật khẩu không khớp.", StatusCodes.Status400BadRequest);
            try
            {
                await readers.ChangePasswordAsync(ticket.TenantId, ticket.AccountId, null, form.NewPassword, requireCurrent: false, ct);
            }
            catch (BusinessRuleException ex) when (ex.StatusCode == StatusCodes.Status400BadRequest)
            {
                return PasswordPage(ctx, antiforgery, ex.Message, StatusCodes.Status400BadRequest);
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

    private static string Message(LoginError error) => error switch
    {
        LoginError.LockedOut => "Tài khoản tạm khoá do đăng nhập sai nhiều lần. Vui lòng thử lại sau 15 phút.",
        LoginError.AccountDisabled => "Thẻ bạn đọc đang bị khoá. Vui lòng liên hệ thư viện.",
        LoginError.TenantUnavailable => "Thư viện đang tạm ngưng phục vụ trực tuyến.",
        _ => "Số thẻ hoặc mật khẩu không đúng.",
    };

    private static IResult NoTenantPage(HttpContext ctx) =>
        AccountEndpoints.Html(ctx, AccountEndpoints.Shell("Đăng nhập bạn đọc", "Không xác định được thư viện",
            """<div class="err" role="alert">Vui lòng đăng nhập từ trang tra cứu (OPAC) của trường bạn.</div>"""), StatusCodes.Status404NotFound);

    private static IResult LoginPage(HttpContext ctx, IAntiforgery antiforgery, TenantReplicaRecord host, string returnUrl, string? cardNo, string? error,
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
                <label for="cardNo">Số thẻ hoặc email</label>
                <div class="box">{{AccountEndpoints.PersonIcon}}<input id="cardNo" name="cardNo" value="{{E.Encode(cardNo ?? "")}}" autocomplete="username" placeholder="Số thẻ bạn đọc" required autofocus></div>
              </div>
              <div class="field">
                <label for="password">Mật khẩu</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="password" name="password" type="password" autocomplete="current-password" placeholder="Mật khẩu" required></div>
                <div class="hint">Chưa có mật khẩu hoặc quên mật khẩu? Liên hệ thư viện của trường.</div>
              </div>
              <button type="submit">Đăng Nhập &rarr;</button>
            </form>
            """;
        return AccountEndpoints.Html(ctx, AccountEndpoints.Shell("Đăng nhập bạn đọc", "Dùng số thẻ thư viện và mật khẩu thư viện đã cấp", body), status);
    }

    private static IResult PasswordPage(HttpContext ctx, IAntiforgery antiforgery, string? error, int status = 200)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var body = $$"""
            <form method="post" action="/account/reader-password">
              {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{E.Encode(error)}</div>")}}
              <input type="hidden" name="{{E.Encode(tokens.FormFieldName)}}" value="{{E.Encode(tokens.RequestToken ?? "")}}">
              <div class="field">
                <label for="newPassword">Mật khẩu mới</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="newPassword" name="newPassword" type="password" autocomplete="new-password" minlength="{{Domain.ReaderAccount.MinPasswordLength}}" required autofocus></div>
                <div class="hint">Từ {{Domain.ReaderAccount.MinPasswordLength}} ký tự, không trùng số thẻ.</div>
              </div>
              <div class="field">
                <label for="confirmPassword">Nhập lại mật khẩu mới</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="confirmPassword" name="confirmPassword" type="password" autocomplete="new-password" required></div>
              </div>
              <button type="submit">Đổi mật khẩu &rarr;</button>
            </form>
            """;
        return AccountEndpoints.Html(ctx, AccountEndpoints.Shell("Đặt mật khẩu mới", "Lần đầu đăng nhập: hãy đổi mật khẩu thư viện đã cấp", body), status);
    }
}
