using System.Text.Encodings.Web;
using System.Text.Unicode;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Identity.Application;
using Elib.Identity.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Elib.Identity.Api;

/// <summary>
/// Class (không phải record positional): binding form của minimal API bắt buộc mọi tham số constructor có mặt,
/// kể cả khi có giá trị mặc định — trường thiếu sẽ thành 400.
/// </summary>
public sealed class LoginForm
{
    public string? Tenant { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? ReturnUrl { get; set; }
    public string? CaptchaId { get; set; }
    public string? Captcha { get; set; }
}

public sealed class OtpForm
{
    public string? Code { get; set; }
}

/// <summary>
/// Trang đăng nhập của identity — đích chuyển hướng của /connect/authorize khi chưa có phiên.
/// Lớp bảo vệ theo tham số đơn vị (monolith: AuthService): CAPTCHA kiểm tra TRƯỚC khi tra tài khoản; OTP qua email SAU khi mật khẩu đúng.
/// </summary>
public static class AccountEndpoints
{
    /// <summary>Mã thử thách OTP đang chờ nhập (chỉ là định danh — mã OTP và người dùng nằm ở server).</summary>
    public const string OtpCookie = "elib.identity.otp";

    private static readonly HtmlEncoder VietnameseHtml = HtmlEncoder.Create(UnicodeRanges.All);

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/account/login", async (HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenantContext, SignInService signIn,
            ILoginPolicySource policies, CaptchaService captchas, string? returnUrl, string? tenant, CancellationToken ct) =>
        {
            var safeReturn = SafeReturnUrl(returnUrl);
            var hostTenant = await HostTenantAsync(tenantContext, signIn, ct);
            var captcha = (await policies.GetAsync(tenantContext.TenantId, ct)).Captcha ? await captchas.CreateAsync(ct) : null;
            return LoginPage(ctx, IdentityTexts.For(ctx), antiforgery, safeReturn, tenant ?? TenantFromReturnUrl(safeReturn), hostTenant, captcha, userName: null, error: null);
        }).ExcludeFromDescription();

        app.MapPost("/account/login", async ([FromForm] LoginForm form, HttpContext ctx, IAntiforgery antiforgery, ITenantContext tenantContext,
            SignInService signIn, ILoginPolicySource policies, CaptchaService captchas, LoginOtpService otp, CancellationToken ct) =>
        {
            var returnUrl = SafeReturnUrl(form.ReturnUrl);
            // Trên host của đơn vị: luôn đăng nhập vào ĐÚNG đơn vị của host (header gateway có chữ ký) — bỏ qua trường "tenant" của form.
            var hostTenant = await HostTenantAsync(tenantContext, signIn, ct);
            var hostPolicy = await policies.GetAsync(tenantContext.TenantId, ct);
            var t = IdentityTexts.For(ctx);

            async Task<IResult> Fail(string message) => LoginPage(ctx, t, antiforgery, returnUrl, form.Tenant, hostTenant,
                hostPolicy.Captcha ? await captchas.CreateAsync(ct) : null, form.UserName, message, StatusCodes.Status401Unauthorized);

            if (tenantContext.TenantId is not null && hostTenant is null) // đơn vị của host chưa đồng bộ về identity
                return await Fail(Message(t, LoginError.TenantUnavailable));
            // CAPTCHA trước khi tra tài khoản: sai CAPTCHA không cho biết tài khoản có tồn tại hay không.
            if (hostPolicy.Captcha && !await captchas.ValidateAsync(form.CaptchaId, form.Captcha, ct))
                return await Fail(t["CAPTCHA_INVALID"]);

            var result = await signIn.LoginAsync(hostTenant?.Code ?? form.Tenant, form.UserName ?? "", form.Password ?? "", ct);
            if (!result.Succeeded)
            {
                var failedTenant = hostTenant ?? (string.IsNullOrWhiteSpace(form.Tenant) ? null : await signIn.FindTenantAsync(form.Tenant, ct));
                await signIn.RecordLoginFailedAsync(failedTenant?.TenantId, form.UserName, result.Error!.Value, ct);
                return await Fail(Message(t, result.Error!.Value));
            }

            var who = result.Identity!;
            var userPolicy = who.TenantId == tenantContext.TenantId ? hostPolicy : await policies.GetAsync(who.TenantId, ct);
            if (userPolicy.Otp && LoginOtpService.Applies(who))
            {
                var challenge = await otp.StartAsync(who, returnUrl, ct);
                ctx.Response.Cookies.Append(OtpCookie, challenge, OtpCookieOptions(ctx));
                return Results.LocalRedirect("/account/otp");
            }

            await signIn.RecordLoginAsync(who, viaOtp: false, ct);
            await ctx.SignInAsync(IdentityCookie.Scheme, ElibPrincipals.ForLoginCookie(who));
            return Results.LocalRedirect(returnUrl);
        }).ExcludeFromDescription();

        app.MapGet("/account/otp", async (HttpContext ctx, IAntiforgery antiforgery, LoginOtpService otp, CancellationToken ct) =>
            await otp.GetAsync(ctx.Request.Cookies[OtpCookie], ct) is { } pending
                ? OtpPage(ctx, IdentityTexts.For(ctx), antiforgery, pending, error: null, info: null)
                : Results.LocalRedirect("/account/login")).ExcludeFromDescription();

        app.MapPost("/account/otp", async ([FromForm] OtpForm form, HttpContext ctx, IAntiforgery antiforgery, SignInService signIn,
            LoginOtpService otp, ITenantContext tenantContext, ILoginPolicySource policies, CaptchaService captchas, CancellationToken ct) =>
        {
            var challenge = ctx.Request.Cookies[OtpCookie];
            var t = IdentityTexts.For(ctx);
            var (passed, error) = await otp.VerifyAsync(challenge, form.Code, ct);
            if (passed is not null)
            {
                ctx.Response.Cookies.Delete(OtpCookie, OtpCookieOptions(ctx));
                var who = await signIn.ReloadAsync(passed.TenantId, passed.UserId, ct);
                if (who is not null)
                {
                    await signIn.RecordLoginAsync(who, viaOtp: true, ct);
                    await ctx.SignInAsync(IdentityCookie.Scheme, ElibPrincipals.ForLoginCookie(who));
                    return Results.LocalRedirect(passed.ReturnUrl);
                }
                error = OtpError.Expired;
            }

            if (error == OtpError.Invalid && await otp.GetAsync(challenge, ct) is { } pending)
                return OtpPage(ctx, t, antiforgery, pending, t["OTP_WRONG"], info: null, StatusCodes.Status401Unauthorized);

            // Hết hạn / sai quá nhiều lần: bỏ thử thách, đăng nhập lại từ đầu.
            ctx.Response.Cookies.Delete(OtpCookie, OtpCookieOptions(ctx));
            var hostTenant = await HostTenantAsync(tenantContext, signIn, ct);
            var captcha = (await policies.GetAsync(tenantContext.TenantId, ct)).Captcha ? await captchas.CreateAsync(ct) : null;
            var message = error == OtpError.TooManyAttempts ? t["OTP_TOO_MANY"] : t["OTP_EXPIRED"];
            return LoginPage(ctx, t, antiforgery, "/", null, hostTenant, captcha, null, message, StatusCodes.Status401Unauthorized);
        }).ExcludeFromDescription();

        // [FromForm] để minimal API kiểm tra antiforgery cho cả nút "Gửi lại mã".
        app.MapPost("/account/otp/resend", async ([FromForm] OtpForm form, HttpContext ctx, IAntiforgery antiforgery, LoginOtpService otp, CancellationToken ct) =>
        {
            var challenge = ctx.Request.Cookies[OtpCookie];
            var error = await otp.ResendAsync(challenge, ct);
            if (await otp.GetAsync(challenge, ct) is not { } pending) return Results.LocalRedirect("/account/login");
            var t = IdentityTexts.For(ctx);
            return error switch
            {
                null => OtpPage(ctx, t, antiforgery, pending, null, t["OTP_RESENT"]),
                OtpError.TooSoon => OtpPage(ctx, t, antiforgery, pending, t.Format("OTP_WAIT", pending.SecondsUntilResend), null),
                _ => OtpPage(ctx, t, antiforgery, pending, t["OTP_RESEND_LIMIT"], null),
            };
        }).ExcludeFromDescription();

        // Đóng vai đơn vị: vé do quản trị nền tảng tạo trên host hệ thống, mở trên host của đơn vị (xem ImpersonationService).
        app.MapGet("/account/impersonate", async (string? ticket, HttpContext ctx, ITenantContext tenantContext, ImpersonationService impersonation,
            CancellationToken ct) =>
        {
            var session = await impersonation.RedeemAsync(ticket, tenantContext.TenantId, ctx.Connection.RemoteIpAddress?.ToString(), ct);
            if (session is null)
            {
                var t = IdentityTexts.For(ctx);
                return Html(ctx, Shell(ctx, t, t["IMP_INVALID_TITLE"], t["IMP_INVALID_SUBTITLE"],
                    $"""
                    <div class="err" role="alert">{VietnameseHtml.Encode(t["IMP_INVALID_TEXT"])}</div>
                    <p class="sub">{VietnameseHtml.Encode(t["IMP_INVALID_HINT"])}</p>
                    """), StatusCodes.Status400BadRequest);
            }

            await ctx.SignInAsync(IdentityCookie.Scheme, ElibPrincipals.ForLoginCookie(session.Identity, session.ExpiresAt),
                new AuthenticationProperties { IsPersistent = false, ExpiresUtc = session.ExpiresAt, AllowRefresh = false });
            return Results.LocalRedirect("/admin/");
        }).ExcludeFromDescription();

        app.MapPost("/account/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(IdentityCookie.Scheme);
            return Results.LocalRedirect("/account/login");
        }).ExcludeFromDescription();

        return app;
    }

    /// <summary>Chỉ chấp nhận đường dẫn nội bộ — chặn open redirect.</summary>
    internal static string SafeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            ? url
            : "/";

    private static CookieOptions OtpCookieOptions(HttpContext ctx) => new()
    {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/account",
        MaxAge = LoginOtpService.Lifetime,
    };

    /// <summary>Đơn vị xác định theo host (gateway gắn X-Tenant-Id có chữ ký cho request ẩn danh). Null trên host hệ thống.</summary>
    private static async Task<TenantReplicaRecord?> HostTenantAsync(ITenantContext tenant, SignInService signIn, CancellationToken ct) =>
        tenant.TenantId is { } id ? await signIn.FindTenantAsync(id, ct) : null;

    private static string? TenantFromReturnUrl(string returnUrl)
    {
        var q = returnUrl.IndexOf('?', StringComparison.Ordinal);
        return q < 0 ? null : QueryHelpers.ParseQuery(returnUrl[q..]).TryGetValue("tenant", out var t) ? t.ToString() : null;
    }

    private static string Message(IdentityTexts t, LoginError error) => error switch
    {
        LoginError.LockedOut => t["ERR_LOCKED_OUT"],
        LoginError.AccountDisabled => t["ERR_DISABLED"],
        LoginError.TenantUnavailable => t["ERR_TENANT"],
        _ => t["ERR_CREDENTIALS"],
    };

    // Icon SVG nhúng — không tải font/icon từ CDN.
    internal const string PersonIcon = """<svg viewBox="0 0 24 24"><path d="M12 12c2.2 0 4-1.8 4-4s-1.8-4-4-4-4 1.8-4 4 1.8 4 4 4zm0 2c-2.7 0-8 1.3-8 4v2h16v-2c0-2.7-5.3-4-8-4z"/></svg>""";
    internal const string LockIcon = """<svg viewBox="0 0 24 24"><path d="M18 8h-1V6c0-2.8-2.2-5-5-5S7 3.2 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.7 1.4-3.1 3.1-3.1s3.1 1.4 3.1 3.1v2z"/></svg>""";
    internal const string UnitIcon = """<svg viewBox="0 0 24 24"><path d="M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10z"/></svg>""";
    private const string ShieldIcon = """<svg viewBox="0 0 24 24"><path d="M12 1 3 5v6c0 5.6 3.8 10.7 9 12 5.2-1.3 9-6.4 9-12V5l-9-4zm-2 16-4-4 1.4-1.4L10 14.2l6.6-6.6L18 9l-8 8z"/></svg>""";
    private const string LibraryIcon = """<svg viewBox="0 0 24 24"><path d="M12 11.55C9.64 9.35 6.48 8 3 8v11c3.48 0 6.64 1.35 9 3.55 2.36-2.19 5.52-3.55 9-3.55V8c-3.48 0-6.64 1.35-9 3.55zM12 8c1.66 0 3-1.34 3-3s-1.34-3-3-3-3 1.34-3 3 1.34 3 3 3z"/></svg>""";

    private static IResult LoginPage(HttpContext ctx, IdentityTexts t, IAntiforgery antiforgery, string returnUrl, string? tenant, TenantReplicaRecord? hostTenant,
        CaptchaService.Challenge? captcha, string? userName, string? error, int status = 200)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var e = VietnameseHtml; // vẫn mã hoá < > " & ' — chỉ giữ nguyên chữ có dấu
        var tenantBlock = hostTenant is not null
            ? $"""<div class="unit">{UnitIcon}<span>{e.Encode(hostTenant.Name)}</span></div>"""
            : $"""
               <div class="field">
                 <label for="tenant">{e.Encode(t["TENANT_CODE"])}</label>
                 <div class="box">{UnitIcon}<input id="tenant" name="tenant" value="{e.Encode(tenant ?? "")}" autocomplete="organization" placeholder="{e.Encode(t["TENANT_CODE"])}"></div>
                 <div class="hint">{e.Encode(t["TENANT_HINT"])}</div>
               </div>
               """;
        var refresh = QueryHelpers.AddQueryString("/account/login", new Dictionary<string, string?> { ["returnUrl"] = returnUrl, ["tenant"] = tenant });
        var captchaBlock = captcha is null
            ? ""
            : $"""
               <div class="field">
                 <label for="captcha">{e.Encode(t["CAPTCHA"])}</label>
                 <div class="captcha"><img src="data:image/png;base64,{captcha.PngBase64}" width="{CaptchaImage.Width}" height="{CaptchaImage.Height}" alt="{e.Encode(t["CAPTCHA_ALT"])}">
                   <a href="{e.Encode(refresh)}">{e.Encode(t["CAPTCHA_REFRESH"])}</a></div>
                 <div class="box">{ShieldIcon}<input id="captcha" name="captcha" autocomplete="off" maxlength="{CaptchaService.Length}" placeholder="{e.Encode(t.Format("CAPTCHA_PLACEHOLDER", CaptchaService.Length))}" required></div>
                 <input type="hidden" name="captchaId" value="{e.Encode(captcha.Id)}">
               </div>
               """;
        var body = $$"""
            <form method="post" action="/account/login">
              {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{e.Encode(error)}</div>")}}
              <input type="hidden" name="{{e.Encode(tokens.FormFieldName)}}" value="{{e.Encode(tokens.RequestToken ?? "")}}">
              <input type="hidden" name="returnUrl" value="{{e.Encode(returnUrl)}}">
              {{tenantBlock}}
              <div class="field">
                <label for="userName">{{e.Encode(t["USERNAME"])}}</label>
                <div class="box">{{PersonIcon}}<input id="userName" name="userName" value="{{e.Encode(userName ?? "")}}" autocomplete="username" placeholder="{{e.Encode(t["USERNAME"])}}" required></div>
              </div>
              <div class="field">
                <label for="password">{{e.Encode(t["PASSWORD"])}}</label>
                <div class="box">{{LockIcon}}<input id="password" name="password" type="password" autocomplete="current-password" placeholder="{{e.Encode(t["PASSWORD"])}}" required></div>
              </div>
              {{captchaBlock}}
              <button type="submit">{{t["SIGN_IN_BTN"]}}</button>
            </form>
            """;
        return Html(ctx, Shell(ctx, t, t["LOGIN_TITLE"], t["LOGIN_SUBTITLE"], body), status);
    }

    private static IResult OtpPage(HttpContext ctx, IdentityTexts t, IAntiforgery antiforgery, OtpPending pending, string? error, string? info, int status = 200)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        var e = VietnameseHtml;
        var token = $"""<input type="hidden" name="{e.Encode(tokens.FormFieldName)}" value="{e.Encode(tokens.RequestToken ?? "")}">""";
        var body = $$"""
            {{(error is null ? "" : $"<div class=\"err\" role=\"alert\">{e.Encode(error)}</div>")}}
            {{(info is null ? "" : $"<div class=\"ok\" role=\"status\">{e.Encode(info)}</div>")}}
            <p class="sub">{{t.Format("OTP_SENT", e.Encode(pending.MaskedEmail))}}</p>
            <form method="post" action="/account/otp">
              {{token}}
              <div class="field">
                <label for="code">{{e.Encode(t["OTP_CODE"])}}</label>
                <div class="box">{{ShieldIcon}}<input id="code" name="code" class="otp" inputmode="numeric" autocomplete="one-time-code" maxlength="6" placeholder="••••••" required autofocus></div>
              </div>
              <button type="submit">{{t["OTP_CONFIRM"]}}</button>
            </form>
            <form method="post" action="/account/otp/resend" class="links">
              {{token}}
              <button type="submit" class="link">{{e.Encode(t["OTP_RESEND"])}}</button>
              <a href="/account/login">{{e.Encode(t["OTP_OTHER_ACCOUNT"])}}</a>
            </form>
            """;
        return Html(ctx, Shell(ctx, t, t["OTP_TITLE"], t["OTP_SUBTITLE"], body), status);
    }

    internal static IResult Html(HttpContext ctx, string html, int status)
    {
        // Trang không có script: CSP chặt — chỉ cho style nội tuyến, ảnh data: (CAPTCHA), gửi form về chính identity.
        ctx.Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; style-src 'unsafe-inline'; img-src data:; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
        ctx.Response.Headers.XFrameOptions = "DENY";
        return Results.Content(html, "text/html; charset=utf-8", statusCode: status);
    }

    /// <summary>Khung trang như trang đăng nhập admin của frontend monolith (pages/login.html): nửa trái thương hiệu, nửa phải nội dung.</summary>
    internal static string Shell(HttpContext ctx, IdentityTexts t, string heading, string subtitle, string body)
    {
        var e = VietnameseHtml;
        string Switch(string lang) =>
            QueryHelpers.AddQueryString(ctx.Request.Path, ctx.Request.Query.Where(q => q.Key != "lang")
                .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v))).Append(new("lang", lang)));
        var langs = $"""<nav class="langs" aria-label="{e.Encode(t["LANGUAGE"])}"><a href="{e.Encode(Switch("vi"))}"{(t.English ? "" : " aria-current=\"true\"")}>VI</a><a href="{e.Encode(Switch("en"))}"{(t.English ? " aria-current=\"true\"" : "")}>EN</a></nav>""";
        return $$"""
            <!doctype html>
            <html lang="{{t.Lang}}">
            <head>
              <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{e.Encode(heading)}} — {{e.Encode(t["SYSTEM_NAME"])}}</title>
              <style>
                *{box-sizing:border-box}
                body{margin:0;font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;background:#fff;color:#111827}
                .page{display:flex;min-height:100vh}
                .brand{display:none;width:50%;background:#2563eb;color:#fff;position:relative;overflow:hidden;align-items:center;justify-content:center;padding:80px}
                .brand:before,.brand:after{content:"";position:absolute;width:384px;height:384px;border-radius:50%;filter:blur(64px)}
                .brand:before{top:-128px;left:-128px;background:#3b82f6;opacity:.5}
                .brand:after{bottom:-128px;right:-128px;background:#60a5fa;opacity:.3}
                .brand .inner{position:relative;z-index:1;max-width:512px}
                .badge{display:inline-flex;align-items:center;justify-content:center;width:64px;height:64px;border-radius:16px;background:rgba(255,255,255,.1);border:1px solid rgba(255,255,255,.2);margin-bottom:32px}
                .badge svg{width:32px;height:32px;fill:#fff}
                .brand h1{font-size:48px;line-height:1.15;font-weight:700;margin:0 0 24px}
                .brand p{color:#dbeafe;font-size:18px;line-height:1.6;margin:0}
                .main{width:100%;display:flex;align-items:center;justify-content:center;padding:32px}
                .card{width:100%;max-width:448px}
                .logo{display:flex;align-items:center;justify-content:center;gap:12px;margin-bottom:40px;color:#2563eb;font-size:24px;font-weight:700}
                .logo svg{width:36px;height:36px;fill:#2563eb}
                .logo span{color:#1f2937}
                h2{font-size:30px;font-weight:700;margin:0 0 8px}
                .sub{color:#6b7280;margin:0 0 32px;line-height:1.5}
                .field{margin-bottom:24px}
                label{display:block;margin-bottom:8px;font-size:14px;font-weight:600;color:#1f2937}
                .box{position:relative}
                .box svg{position:absolute;left:16px;top:50%;transform:translateY(-50%);width:22px;height:22px;fill:#9ca3af;pointer-events:none}
                .box:focus-within svg{fill:#3b82f6}
                input{width:100%;padding:14px 16px 14px 48px;background:#f9fafb;border:1px solid #e5e7eb;border-radius:12px;font-size:15px;outline:none;transition:all .15s}
                input:focus{background:#fff;border-color:#3b82f6;box-shadow:0 0 0 3px rgba(59,130,246,.2)}
                input.otp{font-size:22px;letter-spacing:10px;font-weight:600}
                .hint{font-size:12px;color:#6b7280;margin-top:6px}
                .unit{display:flex;align-items:center;gap:10px;padding:12px 16px;margin-bottom:24px;border-radius:12px;background:#eff6ff;border:1px solid #dbeafe;color:#1d4ed8;font-weight:600}
                .unit svg{width:22px;height:22px;fill:#2563eb;flex-shrink:0}
                .captcha{display:flex;align-items:center;gap:16px;margin-bottom:10px}
                .captcha img{border-radius:10px;border:1px solid #e5e7eb}
                a{color:#2563eb;font-size:14px;text-decoration:none}
                a:hover{text-decoration:underline}
                .err,.ok{display:flex;gap:8px;padding:16px;margin-bottom:24px;font-size:14px;font-weight:500;border-radius:12px}
                .err{background:#fef2f2;border:1px solid #fee2e2;color:#dc2626}
                .ok{background:#ecfdf5;border:1px solid #d1fae5;color:#047857}
                button{width:100%;padding:14px 16px;border:0;border-radius:12px;background:#2563eb;color:#fff;font-size:16px;font-weight:600;cursor:pointer;box-shadow:0 10px 15px -3px rgba(37,99,235,.2);transition:all .15s}
                button:hover{background:#1d4ed8;box-shadow:0 20px 25px -5px rgba(37,99,235,.3)}
                .links{display:flex;justify-content:space-between;align-items:center;margin-top:20px}
                button.link{width:auto;padding:0;background:none;box-shadow:none;color:#2563eb;font-size:14px;font-weight:500}
                button.link:hover{background:none;box-shadow:none;text-decoration:underline}
                .langs{position:fixed;top:16px;right:16px;display:flex;border:1px solid #e5e7eb;border-radius:8px;overflow:hidden;background:#fff;z-index:2}
                .langs a{padding:6px 10px;font-size:12px;font-weight:600;color:#4b5563}
                .langs a:hover{text-decoration:none;background:#f3f4f6}
                .langs a[aria-current]{background:#2563eb;color:#fff}
                @media (min-width:1024px){ .brand{display:flex} .main{width:50%;padding:48px} .logo{display:none} }
              </style>
            </head>
            <body>
              {{langs}}
              <div class="page">
                <div class="brand"><div class="inner">
                  <div class="badge">{{LibraryIcon}}</div>
                  <h1>{{t["BRAND_TITLE"]}}</h1>
                  <p>{{e.Encode(t["BRAND_TEXT"])}}</p>
                </div></div>
                <div class="main"><div class="card">
                  <div class="logo">{{LibraryIcon}}<span>ELIB-K12</span></div>
                  <h2>{{e.Encode(heading)}}</h2>
                  <p class="sub">{{e.Encode(subtitle)}}</p>
                  {{body}}
                </div></div>
              </div>
            </body>
            </html>
            """;
    }
}
