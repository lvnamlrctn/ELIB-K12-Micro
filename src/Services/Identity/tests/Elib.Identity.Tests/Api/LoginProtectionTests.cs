using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Application;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Identity.Tests.Api;

/// <summary>CAPTCHA và OTP khi đăng nhập (tham số ADMIN_LOGIN_CAPTCHA_ENABLED / ADMIN_LOGIN_OTP_ENABLED của đơn vị).</summary>
public sealed partial class LoginProtectionTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>, IAsyncLifetime
{
    private static int _nextTenant = 700;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private readonly Dictionary<long, long> _admins = [];

    private async Task<long> NewTenantAsync()
    {
        var tenantId = Interlocked.Increment(ref _nextTenant);
        await factory.ProvisionTenantAsync(tenantId, "LP" + tenantId.ToString(CultureInfo.InvariantCulture));
        var admin = await factory.CreateClient().WithClaims(TestClaims.SuperAdmin)
            .PostAsJsonAsync($"/api/system/tenants/{tenantId}/admins", new CreateTenantAdminRequest("quantri", "Quản trị", null, "Matkhau123"));
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        _admins[tenantId] = (await admin.Content.ReadFromJsonAsync<UserDto>())!.Id;
        return tenantId;
    }

    private async Task CreateUserAsync(long tenantId, string userName, string? email)
    {
        var response = await factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, _admins[tenantId]))
            .PostAsJsonAsync("/api/users", new CreateUserRequest(userName, "Thủ thư " + userName, email, null, "Matkhau123", null));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private HttpClient BrowserOnTenantHost(long tenantId)
    {
        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        browser.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        browser.DefaultRequestHeaders.Add("X-Gw-Signature", Elib.BuildingBlocks.Tenancy.GatewaySignature.Compute(IdentityApiFactory.GatewayKey, tenantId));
        return browser;
    }

    private static async Task<string> Html(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    private static string Field(string html, string name) => Regex.Match(html, $"name=\"{Regex.Escape(name)}\" value=\"([^\"]*)\"").Groups[1].Value;

    private static string Antiforgery(string html) => AntiforgeryField().Match(html).Groups[1].Value;

    private static Task<HttpResponseMessage> Post(HttpClient browser, string url, Dictionary<string, string> form) =>
        browser.PostAsync(U(url), new FormUrlEncodedContent(form));

    private async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string userName, string? captchaAnswer = null, bool solveCaptcha = false)
    {
        var page = await Html(await browser.GetAsync(U("/account/login")));
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Antiforgery(page), ["userName"] = userName, ["password"] = "Matkhau123", ["returnUrl"] = "/",
        };
        if (page.Contains("name=\"captchaId\"", StringComparison.Ordinal))
        {
            var id = Field(page, "captchaId");
            form["captchaId"] = id;
            form["captcha"] = solveCaptcha
                ? (await factory.Services.GetRequiredService<IDistributedCache>().GetStringAsync(CaptchaService.Key(id)))!.ToLowerInvariant()
                : captchaAnswer ?? "";
        }
        return await Post(browser, "/account/login", form);
    }

    [Fact]
    public async Task Captcha_is_required_on_hosts_whose_tenant_enables_it()
    {
        var tenantId = await NewTenantAsync();
        var browser = BrowserOnTenantHost(tenantId);
        Assert.DoesNotContain("captchaId", await Html(await browser.GetAsync(U("/account/login"))), StringComparison.Ordinal);

        factory.LoginPolicies.Policies[tenantId] = new LoginPolicy(Captcha: true, Otp: false);
        var page = await browser.GetAsync(U("/account/login"));
        var html = await Html(page);
        Assert.Contains("default-src 'none'", page.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        var png = Convert.FromBase64String(Regex.Match(html, "data:image/png;base64,([A-Za-z0-9+/=]+)").Groups[1].Value);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);

        var wrong = await LoginAsync(browser, "quantri", captchaAnswer: "AAAAA");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Contains("Mã xác nhận không đúng", await Html(wrong), StringComparison.Ordinal);

        var ok = await LoginAsync(browser, "quantri", solveCaptcha: true); // không phân biệt hoa thường
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
    }

    [Fact]
    public void Captcha_image_covers_the_charset()
    {
        Assert.Equal(CaptchaService.Charset.Order(), CaptchaImage.Glyphs.Order());
        var png = CaptchaImage.Render("AB2Z9");
        Assert.True(png.Length > 500);
    }

    [Fact]
    public async Task Otp_is_sent_by_email_and_required_after_correct_password()
    {
        var tenantId = await NewTenantAsync();
        factory.LoginPolicies.Policies[tenantId] = new LoginPolicy(Captcha: false, Otp: true);
        await CreateUserAsync(tenantId, "thuthu", "thuthu@truong.edu.vn");

        // Tài khoản không có email: OTP không áp dụng, đăng nhập thẳng.
        Assert.Equal("/", (await LoginAsync(BrowserOnTenantHost(tenantId), "quantri")).Headers.Location!.OriginalString);

        var browser = BrowserOnTenantHost(tenantId);
        var login = await LoginAsync(browser, "thuthu");
        Assert.Equal("/account/otp", login.Headers.Location!.OriginalString);
        var sent = factory.Published.OfType<NotificationRequested>().Last(n => n.TenantId == tenantId);
        Assert.Equal(("LOGIN_OTP", "thuthu@truong.edu.vn"), (sent.TemplateCode, sent.Recipient.Email));
        var code = sent.Data["otp"];
        Assert.Matches("^[0-9]{6}$", code);

        var otpPage = await Html(await browser.GetAsync(U("/account/otp")));
        Assert.Contains("th****@truong.edu.vn", otpPage, StringComparison.Ordinal);
        Assert.DoesNotContain(code, otpPage, StringComparison.Ordinal);

        var wrongCode = code == "000000" ? "111111" : "000000";
        var wrong = await Post(browser, "/account/otp", new() { ["__RequestVerificationToken"] = Antiforgery(otpPage), ["code"] = wrongCode });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Contains("không đúng", await Html(wrong), StringComparison.Ordinal);

        var tooSoon = await Post(browser, "/account/otp/resend", new() { ["__RequestVerificationToken"] = Antiforgery(otpPage) });
        Assert.Contains("Vui lòng đợi", await Html(tooSoon), StringComparison.Ordinal);

        var ok = await Post(browser, "/account/otp", new() { ["__RequestVerificationToken"] = Antiforgery(otpPage), ["code"] = code });
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal("/", ok.Headers.Location!.OriginalString);

        // Đã có phiên: authorize cấp code ngay, không hỏi lại mật khẩu.
        var authorize = await browser.GetAsync(U("/connect/authorize?client_id=elib-admin&response_type=code&scope=openid&state=s"
            + "&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256&redirect_uri=" + Uri.EscapeDataString(IdentityApiFactory.AdminRedirect)));
        Assert.StartsWith(IdentityApiFactory.AdminRedirect, authorize.Headers.Location!.ToString(), StringComparison.Ordinal);

        // Dùng lại mã đã xác thực: thử thách đã bị xoá.
        var replay = await Post(browser, "/account/otp", new() { ["__RequestVerificationToken"] = Antiforgery(otpPage), ["code"] = code });
        Assert.Contains("hết hạn", await Html(replay), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Otp_challenge_is_dropped_after_too_many_wrong_codes()
    {
        var tenantId = await NewTenantAsync();
        factory.LoginPolicies.Policies[tenantId] = new LoginPolicy(Captcha: false, Otp: true);
        await CreateUserAsync(tenantId, "thuthu2", "tt2@truong.edu.vn");
        var browser = BrowserOnTenantHost(tenantId);
        await LoginAsync(browser, "thuthu2");
        var code = factory.Published.OfType<NotificationRequested>().Last(n => n.Recipient.Email == "tt2@truong.edu.vn").Data["otp"];
        var token = Antiforgery(await Html(await browser.GetAsync(U("/account/otp"))));
        var wrongCode = code == "000000" ? "111111" : "000000";

        HttpResponseMessage last = null!;
        for (var i = 0; i < LoginOtpService.MaxAttempts; i++)
            last = await Post(browser, "/account/otp", new() { ["__RequestVerificationToken"] = token, ["code"] = wrongCode });
        Assert.Contains("quá nhiều lần", await Html(last), StringComparison.Ordinal);

        var afterwards = await Post(browser, "/account/otp", new() { ["__RequestVerificationToken"] = token, ["code"] = code });
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode); // mã đúng cũng không còn dùng được
    }

    [Fact]
    public async Task System_admin_otp_goes_through_platform_notification()
    {
        factory.LoginPolicies.Policies[0] = new LoginPolicy(Captcha: false, Otp: true);
        try
        {
            var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var page = await Html(await browser.GetAsync(U("/account/login")));
            var login = await Post(browser, "/account/login", new()
            {
                ["__RequestVerificationToken"] = Antiforgery(page), ["tenant"] = "", ["userName"] = "sysadmin", ["password"] = "Sysadmin123", ["returnUrl"] = "/",
            });
            Assert.Equal("/account/otp", login.Headers.Location!.OriginalString);
            var sent = factory.Published.OfType<SystemNotificationRequested>().Last();
            Assert.Equal(("LOGIN_OTP", "sysadmin@platform.test"), (sent.TemplateCode, sent.Recipient.Email));
        }
        finally
        {
            factory.LoginPolicies.Policies.TryRemove(0, out _);
        }
    }

    [Fact]
    public async Task Logins_and_account_changes_are_audited_without_secrets()
    {
        var tenantId = await NewTenantAsync();
        await CreateUserAsync(tenantId, "audit1", null);
        var created = factory.Published.OfType<AuditRecorded>().Last(a => a.TenantId == tenantId && a.Action == "USER_CREATE");
        Assert.Equal(("identity", "Tạo tài khoản audit1 — Thủ thư audit1"), (created.Service, created.Summary));

        var browser = BrowserOnTenantHost(tenantId);
        var page = await Html(await browser.GetAsync(U("/account/login")));
        await Post(browser, "/account/login", new()
        {
            ["__RequestVerificationToken"] = Antiforgery(page), ["userName"] = "audit1", ["password"] = "SaiMatKhau99", ["returnUrl"] = "/",
        });
        var failed = factory.Published.OfType<AuditRecorded>().Last(a => a.TenantId == tenantId && a.Action == "LOGIN_FAILED");
        Assert.Equal("Đăng nhập thất bại 'audit1': sai tên đăng nhập hoặc mật khẩu", failed.Summary);
        Assert.DoesNotContain("SaiMatKhau99", failed.Summary, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(BrowserOnTenantHost(tenantId), "audit1")).StatusCode);
        var login = factory.Published.OfType<AuditRecorded>().Last(a => a.TenantId == tenantId && a.Action == "LOGIN");
        Assert.Equal(("Đăng nhập", "Thủ thư audit1 (audit1)"), (login.Summary, login.ActorName));

        // Đăng nhập tài khoản hệ thống → nhật ký nền tảng.
        var sys = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var sysPage = await Html(await sys.GetAsync(U("/account/login")));
        await Post(sys, "/account/login", new()
        {
            ["__RequestVerificationToken"] = Antiforgery(sysPage), ["tenant"] = "", ["userName"] = "sysadmin", ["password"] = "Sysadmin123", ["returnUrl"] = "/",
        });
        Assert.Contains(factory.Published.OfType<SystemAuditRecorded>(), a => a.Action == "LOGIN" && a.ActorName == "Quản trị hệ thống (sysadmin)");
    }

    [Fact]
    public void Email_is_masked()
    {
        Assert.Equal("ng*******@truong.edu.vn", LoginOtpService.Mask("nguyenvan@truong.edu.vn"));
        Assert.Equal("a***@b.vn", LoginOtpService.Mask("a@b.vn"));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryField();
}
