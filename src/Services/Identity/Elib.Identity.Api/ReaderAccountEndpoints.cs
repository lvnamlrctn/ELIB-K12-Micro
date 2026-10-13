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
    public string? CaptchaId { get; set; }
    public string? Captcha { get; set; }
}

public sealed class ReaderForgotForm
{
    public string? CardNo { get; set; }
    public string? CaptchaId { get; set; }
    public string? Captcha { get; set; }
}

public sealed class ReaderResetForm
{
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
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

    /// <summary>Thử thách OTP của bạn đọc (READER_LOGIN_OTP_ENABLED) — tách với cookie OTP của nhân viên.</summary>
    public const string OtpCookie = "elib.identity.rotp";

    private static readonly TimeSpan FirstPasswordLifetime = TimeSpan.FromMinutes(10);
    private static readonly HtmlEncoder E = HtmlEncoder.Create(UnicodeRanges.All);

    public static IEndpointRouteBuilder MapReaderAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/account/reader-login", async (HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant, SignInService signIn,
            ILoginPolicySource policies, CaptchaService captchas, string? returnUrl, string? reset, CancellationToken ct) =>
        {
            var host = await HostTenantAsync(tenant, signIn, ct);
            var t = IdentityTexts.For(ctx);
            if (host is null) return NoTenantPage(ctx, t);
            var captcha = (await policies.GetAsync(host.TenantId, ct)).ReaderCaptcha ? await captchas.CreateAsync(ct) : null;
            return LoginPage(ctx, t, antiforgery, host, AccountEndpoints.SafeReturnUrl(returnUrl), captcha, null, null,
                info: reset == "1" ? t["READER_RESET_DONE"] : null);
        }).ExcludeFromDescription();

        app.MapPost("/account/reader-login", async ([FromForm] ReaderLoginForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant,
            SignInService signIn, ReaderSignInService readers, IDataProtectionProvider protection, ILoginPolicySource policies, CaptchaService captchas,
            LoginOtpService otp, CancellationToken ct) =>
        {
            var host = await HostTenantAsync(tenant, signIn, ct);
            var t = IdentityTexts.For(ctx);
            if (host is null) return NoTenantPage(ctx, t);
            var returnUrl = AccountEndpoints.SafeReturnUrl(form.ReturnUrl);
            var policy = await policies.GetAsync(host.TenantId, ct);

            async Task<IResult> Fail(string message) => LoginPage(ctx, t, antiforgery, host, returnUrl,
                policy.ReaderCaptcha ? await captchas.CreateAsync(ct) : null, form.CardNo, message, StatusCodes.Status401Unauthorized);

            // CAPTCHA trước khi tra tài khoản (READER_LOGIN_CAPTCHA_ENABLED): sai CAPTCHA không cho biết số thẻ có tồn tại hay không.
            if (policy.ReaderCaptcha && !await captchas.ValidateAsync(form.CaptchaId, form.Captcha, ct)) return await Fail(t["CAPTCHA_INVALID"]);

            var result = await readers.LoginAsync(host.TenantId, form.CardNo, form.Password, ct);
            if (!result.Succeeded) return await Fail(Message(t, result.Error!.Value));

            var who = result.Identity!;
            // OTP qua email (READER_LOGIN_OTP_ENABLED) — bạn đọc chưa có email thì bỏ qua, như ELIB-LRC.
            if (policy.ReaderOtp && await readers.EmailAsync(who.TenantId, who.AccountId, ct) is { } email)
            {
                var challenge = await otp.StartReaderAsync(who.TenantId, who.AccountId, email, who.FullName, returnUrl + (who.External ? "#ext" : ""), ct);
                ctx.Response.Cookies.Append(OtpCookie, challenge, OtpCookieOptions(ctx));
                return Results.LocalRedirect("/account/reader-otp");
            }
            return await CompleteAsync(ctx, who, returnUrl, protection);
        }).ExcludeFromDescription();

        app.MapGet("/account/reader-otp", async (HttpContext ctx, IAntiforgery antiforgery, LoginOtpService otp, CancellationToken ct) =>
            await otp.GetAsync(ctx.Request.Cookies[OtpCookie], ct) is { } pending
                ? AccountEndpoints.OtpPage(ctx, IdentityTexts.For(ctx), antiforgery, pending, null, null, basePath: "/account/reader-otp", loginPath: "/account/reader-login")
                : Results.LocalRedirect("/account/reader-login")).ExcludeFromDescription();

        app.MapPost("/account/reader-otp", async ([FromForm] OtpForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant, LoginOtpService otp,
            ReaderSignInService readers, IDataProtectionProvider protection, CancellationToken ct) =>
        {
            var challenge = ctx.Request.Cookies[OtpCookie];
            var t = IdentityTexts.For(ctx);
            var (passed, error) = await otp.VerifyAsync(challenge, form.Code, ct);
            if (passed is { Reader: true, TenantId: { } passedTenant } && passedTenant == tenant.TenantId)
            {
                ctx.Response.Cookies.Delete(OtpCookie, OtpCookieOptions(ctx));
                var external = passed.ReturnUrl.EndsWith("#ext", StringComparison.Ordinal);
                var returnUrl = external ? passed.ReturnUrl[..^4] : passed.ReturnUrl;
                if (await readers.ReloadAsync(passedTenant, passed.UserId, ct, external) is { } who)
                    return await CompleteAsync(ctx, who, returnUrl, protection);
                error = OtpError.Expired;
            }
            if (error == OtpError.Invalid && await otp.GetAsync(challenge, ct) is { } pending)
                return AccountEndpoints.OtpPage(ctx, t, antiforgery, pending, t["OTP_WRONG"], null, StatusCodes.Status401Unauthorized,
                    basePath: "/account/reader-otp", loginPath: "/account/reader-login");
            ctx.Response.Cookies.Delete(OtpCookie, OtpCookieOptions(ctx));
            return Results.LocalRedirect("/account/reader-login");
        }).ExcludeFromDescription();

        app.MapPost("/account/reader-otp/resend", async ([FromForm] OtpForm form, HttpContext ctx, IAntiforgery antiforgery, LoginOtpService otp, CancellationToken ct) =>
        {
            var challenge = ctx.Request.Cookies[OtpCookie];
            var error = await otp.ResendAsync(challenge, ct);
            if (await otp.GetAsync(challenge, ct) is not { } pending) return Results.LocalRedirect("/account/reader-login");
            var t = IdentityTexts.For(ctx);
            return error switch
            {
                null => AccountEndpoints.OtpPage(ctx, t, antiforgery, pending, null, t["OTP_RESENT"], basePath: "/account/reader-otp", loginPath: "/account/reader-login"),
                OtpError.TooSoon => AccountEndpoints.OtpPage(ctx, t, antiforgery, pending, t.Format("OTP_WAIT", pending.SecondsUntilResend), null,
                    basePath: "/account/reader-otp", loginPath: "/account/reader-login"),
                _ => AccountEndpoints.OtpPage(ctx, t, antiforgery, pending, t["OTP_RESEND_LIMIT"], null, basePath: "/account/reader-otp", loginPath: "/account/reader-login"),
            };
        }).ExcludeFromDescription();

        // Quên mật khẩu: số thẻ + CAPTCHA (luôn bật) → thư có link đặt lại tới email trong hồ sơ; luôn trả cùng một câu.
        app.MapGet("/account/reader-forgot", async (HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant, SignInService signIn, CaptchaService captchas,
            CancellationToken ct) =>
        {
            var host = await HostTenantAsync(tenant, signIn, ct);
            var t = IdentityTexts.For(ctx);
            return host is null ? NoTenantPage(ctx, t) : ForgotPage(ctx, t, antiforgery, host, await captchas.CreateAsync(ct), null, null, null);
        }).ExcludeFromDescription();

        app.MapPost("/account/reader-forgot", async ([FromForm] ReaderForgotForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant,
            SignInService signIn, CaptchaService captchas, ReaderPasswordResetService resets, CancellationToken ct) =>
        {
            var host = await HostTenantAsync(tenant, signIn, ct);
            var t = IdentityTexts.For(ctx);
            if (host is null) return NoTenantPage(ctx, t);
            if (!await captchas.ValidateAsync(form.CaptchaId, form.Captcha, ct))
                return ForgotPage(ctx, t, antiforgery, host, await captchas.CreateAsync(ct), form.CardNo, t["CAPTCHA_INVALID"], null, StatusCodes.Status400BadRequest);
            await resets.RequestAsync(host.TenantId, form.CardNo, $"{ctx.Request.Scheme}://{ctx.Request.Host}/account/reader-reset", ct);
            return ForgotPage(ctx, t, antiforgery, host, null, null, null, t["READER_FORGOT_SENT"]);
        }).ExcludeFromDescription();

        app.MapGet("/account/reader-reset", async (HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant, ReaderPasswordResetService resets,
            string? token, CancellationToken ct) =>
        {
            var t = IdentityTexts.For(ctx);
            var card = tenant.TenantId is { } id ? await resets.CheckAsync(id, token, ct) : null;
            return card is null ? ResetInvalidPage(ctx, t) : ResetPage(ctx, t, antiforgery, token!, card, null);
        }).ExcludeFromDescription();

        app.MapPost("/account/reader-reset", async ([FromForm] ReaderResetForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenant,
            ReaderPasswordResetService resets, CancellationToken ct) =>
        {
            var t = IdentityTexts.For(ctx);
            if (tenant.TenantId is not { } id || await resets.CheckAsync(id, form.Token, ct) is not { } card) return ResetInvalidPage(ctx, t);
            if (form.NewPassword != form.ConfirmPassword)
                return ResetPage(ctx, t, antiforgery, form.Token!, card, t["READER_PASSWORD_MISMATCH"], StatusCodes.Status400BadRequest);
            try
            {
                await resets.ResetAsync(id, form.Token, form.NewPassword, ct);
            }
            catch (BusinessRuleException ex) when (ex.StatusCode == StatusCodes.Status400BadRequest)
            {
                return ex.Code == "READER_RESET_INVALID"
                    ? ResetInvalidPage(ctx, t)
                    : ResetPage(ctx, t, antiforgery, form.Token!, card, t.Code(ex.Code) ?? ex.Message, StatusCodes.Status400BadRequest);
            }
            return Results.LocalRedirect("/account/reader-login?reset=1");
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

    /// <summary>Sau mật khẩu (+ OTP): còn bắt đổi mật khẩu → bước đổi mật khẩu; không thì đăng nhập vào OPAC.</summary>
    private static async Task<IResult> CompleteAsync(HttpContext ctx, ReaderIdentity who, string returnUrl, IDataProtectionProvider protection)
    {
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
    }

    private static CookieOptions OtpCookieOptions(HttpContext ctx) => new()
    {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/account",
        MaxAge = LoginOtpService.Lifetime,
    };

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
        LoginError.NoReaderProfile => t["READER_ERR_NO_PROFILE"],
        LoginError.ExternalUnavailable => t["READER_ERR_EXTERNAL"],
        _ => t["READER_ERR_CREDENTIALS"],
    };

    private static IResult NoTenantPage(HttpContext ctx, IdentityTexts t) =>
        AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_LOGIN_TITLE"], t["READER_NO_TENANT_SUBTITLE"],
            $"""<div class="err" role="alert">{E.Encode(t["READER_NO_TENANT"])}</div>"""), StatusCodes.Status404NotFound);

    private static IResult LoginPage(HttpContext ctx, IdentityTexts t, IAntiforgery antiforgery, TenantReplicaRecord host, string returnUrl,
        CaptchaService.Challenge? captcha, string? cardNo, string? error, int status = 200, string? info = null)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var refresh = "/account/reader-login?returnUrl=" + Uri.EscapeDataString(returnUrl);
        var body = $$"""
            <form method="post" action="/account/reader-login">
              {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{E.Encode(error)}</div>")}}
              {{(info is null ? "" : $"<div class=\"ok\" role=\"status\">{E.Encode(info)}</div>")}}
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
              {{AccountEndpoints.CaptchaBlock(t, captcha, refresh)}}
              <button type="submit">{{t["SIGN_IN_BTN"]}}</button>
              <div class="links"><a href="/account/reader-forgot">{{E.Encode(t["READER_FORGOT_LINK"])}}</a></div>
            </form>
            """;
        return AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_LOGIN_TITLE"], t["READER_LOGIN_SUBTITLE"], body), status);
    }

    private static IResult ForgotPage(HttpContext ctx, IdentityTexts t, IAntiforgery antiforgery, TenantReplicaRecord host, CaptchaService.Challenge? captcha,
        string? cardNo, string? error, string? info, int status = 200)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var form = info is not null
            ? $"""<div class="ok" role="status">{E.Encode(info)}</div>"""
            : $$"""
              <form method="post" action="/account/reader-forgot">
                {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{E.Encode(error)}</div>")}}
                <input type="hidden" name="{{E.Encode(tokens.FormFieldName)}}" value="{{E.Encode(tokens.RequestToken ?? "")}}">
                <div class="unit">{{AccountEndpoints.UnitIcon}}<span>{{E.Encode(host.Name)}}</span></div>
                <div class="field">
                  <label for="cardNo">{{E.Encode(t["READER_FORGOT_CARD"])}}</label>
                  <div class="box">{{AccountEndpoints.PersonIcon}}<input id="cardNo" name="cardNo" value="{{E.Encode(cardNo ?? "")}}" autocomplete="username" placeholder="{{E.Encode(t["READER_CARD_PLACEHOLDER"])}}" required autofocus></div>
                </div>
                {{AccountEndpoints.CaptchaBlock(t, captcha, "/account/reader-forgot")}}
                <button type="submit">{{t["READER_FORGOT_BTN"]}}</button>
              </form>
              """;
        var body = form + $"""<div class="links"><a href="/account/reader-login">{E.Encode(t["READER_BACK_TO_LOGIN"])}</a></div>""";
        return AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_FORGOT_TITLE"], t["READER_FORGOT_SUBTITLE"], body), status);
    }

    private static IResult ResetPage(HttpContext ctx, IdentityTexts t, IAntiforgery antiforgery, string token, string cardNo, string? error, int status = 200)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var body = $$"""
            <form method="post" action="/account/reader-reset">
              {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{E.Encode(error)}</div>")}}
              <input type="hidden" name="{{E.Encode(tokens.FormFieldName)}}" value="{{E.Encode(tokens.RequestToken ?? "")}}">
              <input type="hidden" name="token" value="{{E.Encode(token)}}">
              <div class="unit">{{AccountEndpoints.PersonIcon}}<span>{{E.Encode(cardNo)}}</span></div>
              <div class="field">
                <label for="newPassword">{{E.Encode(t["READER_NEW_PASSWORD"])}}</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="newPassword" name="newPassword" type="password" autocomplete="new-password" minlength="{{Domain.ReaderAccount.MinPasswordLength}}" required autofocus></div>
                <div class="hint">{{E.Encode(t.Format("READER_NEW_PASSWORD_HINT", Domain.ReaderAccount.MinPasswordLength))}}</div>
              </div>
              <div class="field">
                <label for="confirmPassword">{{E.Encode(t["READER_CONFIRM_PASSWORD"])}}</label>
                <div class="box">{{AccountEndpoints.LockIcon}}<input id="confirmPassword" name="confirmPassword" type="password" autocomplete="new-password" required></div>
              </div>
              <button type="submit">{{t["READER_RESET_BTN"]}}</button>
            </form>
            """;
        return AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_RESET_TITLE"], t["READER_RESET_SUBTITLE"], body), status);
    }

    private static IResult ResetInvalidPage(HttpContext ctx, IdentityTexts t) =>
        AccountEndpoints.Html(ctx, AccountEndpoints.Shell(ctx, t, t["READER_RESET_TITLE"], t["READER_RESET_SUBTITLE"],
            $"""
            <div class="err" role="alert">{E.Encode(t["READER_RESET_INVALID"])}</div>
            <div class="links"><a href="/account/reader-forgot">{E.Encode(t["READER_FORGOT_AGAIN"])}</a><a href="/account/reader-login">{E.Encode(t["READER_BACK_TO_LOGIN"])}</a></div>
            """), StatusCodes.Status400BadRequest);

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
