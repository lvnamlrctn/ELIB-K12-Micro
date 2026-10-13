using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Patron;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Application;
using Elib.Identity.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Elib.Identity.Tests.Api;

/// <summary>
/// Đăng nhập bạn đọc OPAC: CAPTCHA + OTP theo tham số READER_LOGIN_* của đơn vị, quên mật khẩu qua email (link dùng một lần), xác thực qua
/// LDAP/API của trường (READER_AUTH_CONFIG) kể cả lần phát token sau đó.
/// </summary>
public sealed partial class ReaderLoginProtectionTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>, IAsyncLifetime
{
    private const string Redirect = "https://th.truong.test/tai-khoan/callback";
    private static int _nextTenant = 1300;
    private readonly Dictionary<long, long> _admins = [];

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private async Task<long> NewTenantAsync()
    {
        var tenantId = Interlocked.Increment(ref _nextTenant);
        await factory.ProvisionTenantAsync(tenantId, "RP" + tenantId.ToString(CultureInfo.InvariantCulture));
        var admin = await factory.CreateClient().WithClaims(TestClaims.SuperAdmin)
            .PostAsJsonAsync($"/api/system/tenants/{tenantId}/admins", new CreateTenantAdminRequest("quantri", "Quản trị", null, "Matkhau123"));
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        _admins[tenantId] = (await admin.Content.ReadFromJsonAsync<UserDto>())!.Id;
        return tenantId;
    }

    /// <summary>Bạn đọc từ patron (+ mật khẩu do thư viện đặt — bạn đọc tự đổi để không bị bắt đổi ở lần đăng nhập đầu).</summary>
    private async Task ReaderAsync(long tenantId, string cardNo, string? email, string? password)
    {
        var readerId = Guid.NewGuid();
        await factory.Services.GetRequiredService<IBus>().Publish(new ReaderChanged
        {
            TenantId = tenantId, ReaderPublicId = readerId, CardNo = cardNo, FullName = "Học sinh " + cardNo, Email = email, Status = 2, Version = 1,
        });
        for (var i = 0; i < 100 && !await HasAccountAsync(tenantId, readerId); i++) await Task.Delay(50);
        if (password is null) return;
        await using var scope = factory.Services.CreateAsyncScope();
        using var _ = scope.ServiceProvider.GetRequiredService<Elib.BuildingBlocks.Tenancy.ITenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var account = await db.ReaderAccounts.FirstAsync(a => a.ReaderPublicId == readerId);
        account.ChangeOwnPassword(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(password));
        await db.SaveChangesAsync();
    }

    private async Task<bool> HasAccountAsync(long tenantId, Guid readerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().ReaderAccounts.IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId && a.ReaderPublicId == readerId);
    }

    private HttpClient Browser(long tenantId)
    {
        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        browser.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        browser.DefaultRequestHeaders.Add("X-Gw-Signature", Elib.BuildingBlocks.Tenancy.GatewaySignature.Compute(IdentityApiFactory.GatewayKey, tenantId));
        return browser;
    }

    private static async Task<string> Html(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    private static string Field(string html, string name) => Regex.Match(html, $"name=\"{Regex.Escape(name)}\" value=\"([^\"]*)\"").Groups[1].Value;

    private async Task<string> SolveAsync(string html) =>
        (await factory.Services.GetRequiredService<IDistributedCache>().GetStringAsync(CaptchaService.Key(Field(html, "captchaId"))))!;

    private async Task<HttpResponseMessage> PostLoginAsync(HttpClient browser, string cardNo, string password, string returnUrl = "/", bool solveCaptcha = true)
    {
        var page = await Html(await browser.GetAsync(U("/account/reader-login?returnUrl=" + Uri.EscapeDataString(returnUrl))));
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Field(page, "__RequestVerificationToken"), ["cardNo"] = cardNo, ["password"] = password, ["returnUrl"] = returnUrl,
        };
        if (page.Contains("name=\"captchaId\"", StringComparison.Ordinal))
        {
            form["captchaId"] = Field(page, "captchaId");
            form["captcha"] = solveCaptcha ? await SolveAsync(page) : "XXXXX";
        }
        return await browser.PostAsync(U("/account/reader-login"), new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task Captcha_and_email_otp_protect_reader_sign_in_when_the_tenant_enables_them()
    {
        var tenantId = await NewTenantAsync();
        await ReaderAsync(tenantId, "OT-01", "ot01@truong.edu.vn", "Docsach2026");
        await ReaderAsync(tenantId, "OT-02", null, "Docsach2026");
        var browser = Browser(tenantId);
        Assert.DoesNotContain("captchaId", await Html(await browser.GetAsync(U("/account/reader-login"))), StringComparison.Ordinal);

        factory.LoginPolicies.Policies[tenantId] = new LoginPolicy(false, false, ReaderCaptcha: true, ReaderOtp: true);
        var wrong = await PostLoginAsync(browser, "OT-01", "Docsach2026", solveCaptcha: false);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Contains("Mã xác nhận không đúng", await Html(wrong), StringComparison.Ordinal);

        var login = await PostLoginAsync(browser, "OT-01", "Docsach2026", "/tai-khoan");
        Assert.Equal("/account/reader-otp", login.Headers.Location!.OriginalString);
        var sent = factory.Published.OfType<NotificationRequested>().Last(n => n.TenantId == tenantId && n.TemplateCode == "LOGIN_OTP");
        Assert.Equal(("ot01@truong.edu.vn", "reader"), (sent.Recipient.Email, sent.Actor?.Kind));
        var otpPage = await Html(await browser.GetAsync(U("/account/reader-otp")));
        Assert.Contains("ot*", otpPage, StringComparison.Ordinal);
        var bad = await browser.PostAsync(U("/account/reader-otp"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Field(otpPage, "__RequestVerificationToken"), ["code"] = sent.Data["otp"] == "000000" ? "111111" : "000000",
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        var ok = await browser.PostAsync(U("/account/reader-otp"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Field(otpPage, "__RequestVerificationToken"), ["code"] = sent.Data["otp"],
        }));
        Assert.Equal("/tai-khoan", ok.Headers.Location!.OriginalString);

        // Bạn đọc chưa có email: bỏ qua OTP như ELIB-LRC.
        var noEmail = await PostLoginAsync(Browser(tenantId), "OT-02", "Docsach2026", "/tai-khoan");
        Assert.Equal("/tai-khoan", noEmail.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Forgotten_password_is_reset_through_a_single_use_emailed_link_without_revealing_card_numbers()
    {
        var tenantId = await NewTenantAsync();
        await ReaderAsync(tenantId, "QM-01", "qm01@truong.edu.vn", "Cu-matkhau1");
        var browser = Browser(tenantId);
        Assert.Contains("/account/reader-forgot", await Html(await browser.GetAsync(U("/account/reader-login"))), StringComparison.Ordinal);

        async Task<string> RequestAsync(string card)
        {
            var page = await Html(await browser.GetAsync(U("/account/reader-forgot")));
            var response = await browser.PostAsync(U("/account/reader-forgot"), new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Field(page, "__RequestVerificationToken"), ["cardNo"] = card,
                ["captchaId"] = Field(page, "captchaId"), ["captcha"] = await SolveAsync(page),
            }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await Html(response);
        }

        var before = factory.Published.OfType<NotificationRequested>().Count(n => n.TemplateCode == ReaderPasswordResetService.TemplateCode);
        var unknown = await RequestAsync("KHONG-CO");
        var known = await RequestAsync("qm-01");
        Assert.Equal(Regex.Match(unknown, "<div class=\"ok\"[^<]*").Value, Regex.Match(known, "<div class=\"ok\"[^<]*").Value); // cùng một câu
        await RequestAsync("QM-01"); // trong 60 giây: không gửi thêm
        var mails = factory.Published.OfType<NotificationRequested>().Where(n => n.TemplateCode == ReaderPasswordResetService.TemplateCode).ToList();
        Assert.Equal(before + 1, mails.Count);
        var mail = mails[^1];
        Assert.Equal(("qm01@truong.edu.vn", "QM-01"), (mail.Recipient.Email, mail.Data["card_no"]));
        var link = new Uri(mail.Data["link"]);
        Assert.Equal("/account/reader-reset", link.AbsolutePath);
        var token = QueryHelpers.ParseQuery(link.Query)["token"].ToString();

        Assert.Equal(HttpStatusCode.BadRequest, (await Browser(tenantId + 1).GetAsync(U(link.PathAndQuery))).StatusCode); // host đơn vị khác
        var resetPage = await Html(await browser.GetAsync(U(link.PathAndQuery)));
        Assert.Contains("QM-01", resetPage, StringComparison.Ordinal);
        async Task<HttpResponseMessage> ResetAsync(string password, string confirm) =>
            await browser.PostAsync(U("/account/reader-reset"), new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Field(resetPage, "__RequestVerificationToken"), ["token"] = token, ["newPassword"] = password,
                ["confirmPassword"] = confirm,
            }));
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync("Moi-matkhau2", "khac")).StatusCode);
        var done = await ResetAsync("Moi-matkhau2", "Moi-matkhau2");
        Assert.Equal("/account/reader-login?reset=1", done.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync("Lan-hai-abc", "Lan-hai-abc")).StatusCode); // dùng một lần

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostLoginAsync(Browser(tenantId), "QM-01", "Cu-matkhau1")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostLoginAsync(Browser(tenantId), "QM-01", "Moi-matkhau2")).StatusCode);
        Assert.Contains(factory.Published.OfType<AuditRecorded>(), a => a.TenantId == tenantId && a.Action == "READER_PASSWORD_RESET");
    }

    [Fact]
    public async Task School_account_via_ldap_or_api_signs_in_and_gets_tokens_without_a_library_password()
    {
        var tenantId = await NewTenantAsync();
        await ReaderAsync(tenantId, "HS-EXT", null, password: null); // chưa có mật khẩu thư viện
        await ReaderAsync(tenantId, "HS-LOC", null, "Thuvien2026");
        factory.LoginPolicies.Policies[tenantId] = new LoginPolicy(false, false, ReaderAuth: """{ "Provider": "Api", "FallbackToLocal": false }""");
        factory.ExternalAuth.Results[("nguyenvana", "Truong@123")] = new ExternalAuthResult(ExternalAuthOutcome.Success, "hs-ext", "a@truong.edu.vn");
        factory.ExternalAuth.Results[("nguoila", "Truong@123")] = new ExternalAuthResult(ExternalAuthOutcome.Success, "KHONG-CO-HO-SO");

        // OPAC: authorize → đăng nhập bằng tài khoản trường → code (phát token cho phiên đăng nhập ngoài).
        var browser = Browser(tenantId);
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var authorize = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "elib-opac", ["response_type"] = "code", ["redirect_uri"] = Redirect, ["scope"] = "openid profile offline_access elib-api",
            ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))), ["code_challenge_method"] = "S256", ["state"] = "s1",
        });
        var toLogin = await browser.GetAsync(U(authorize));
        var returnUrl = QueryHelpers.ParseQuery(toLogin.Headers.Location!.Query)["ReturnUrl"].ToString();
        var login = await PostLoginAsync(browser, "nguyenvana", "Truong@123", returnUrl);
        Assert.Equal(returnUrl, login.Headers.Location!.OriginalString);
        var callback = (await browser.GetAsync(U(returnUrl))).Headers.Location!;
        Assert.StartsWith(Redirect, callback.ToString(), StringComparison.Ordinal);
        var token = await factory.CreateClient().PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = "elib-opac", ["redirect_uri"] = Redirect,
            ["code"] = QueryHelpers.ParseQuery(callback.Query)["code"].ToString(), ["code_verifier"] = verifier,
        }));
        Assert.True(token.IsSuccessStatusCode, await token.Content.ReadAsStringAsync());

        Assert.Contains("chưa có hồ sơ bạn đọc", await Html(await PostLoginAsync(Browser(tenantId), "nguoila", "Truong@123")), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostLoginAsync(Browser(tenantId), "HS-LOC", "Thuvien2026")).StatusCode); // không cho mật khẩu thư viện
        factory.ExternalAuth.Down = true;
        Assert.Contains("tạm thời không kết nối được", await Html(await PostLoginAsync(Browser(tenantId), "nguyenvana", "Truong@123")), StringComparison.Ordinal);
        factory.ExternalAuth.Down = false;

        // Cho phép mật khẩu thư viện thay thế (FallbackToLocal, mặc định như LRC).
        factory.LoginPolicies.Policies[tenantId] = new LoginPolicy(false, false, ReaderAuth: """{ "Provider": "Ldap" }""");
        Assert.Equal(HttpStatusCode.Redirect, (await PostLoginAsync(Browser(tenantId), "HS-LOC", "Thuvien2026")).StatusCode);

        // Kiểm tra kết nối (quản trị, quyền SYSTEM_PARAMS:edit).
        var admin = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, _admins[tenantId]));
        var test = await (await admin.PostAsJsonAsync(U("/api/reader-auth/Test"), new ReaderAuthTestRequest("nguyenvana", "Truong@123")))
            .Content.ReadFromJsonAsync<ReaderAuthTestResult>();
        Assert.Equal(("Ldap", "Success", "hs-ext", true), (test!.Provider, test.Outcome, test.CardNo, test.ProfileFound));
    }
}

/// <summary>Xác thực qua API của trường: trường lồng nhau, SuccessField, 401; thoát ký tự bộ lọc LDAP.</summary>
public sealed class ExternalReaderAuthTests
{
    private sealed class Stub(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, await request.Content!.ReadAsStringAsync(cancellationToken));
    }

    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static ReaderAuthSettings Api() => ReaderAuthSettings.Parse("""
        { "Provider": "Api", "MatchBy": "Cardno",
          "Api": { "Url": "https://sso.truong.test/login", "UsernameField": "user", "PasswordField": "pass", "SuccessField": "ok",
                   "CardNoField": "data.maSo", "EmailField": "data.email", "ApiKeyHeader": "X-Key", "ApiKey": "k1" } }
        """)!;

    [Fact]
    public async Task Api_provider_posts_credentials_and_reads_nested_fields()
    {
        string? body = null, key = null;
        using var handler = new Stub((r, b) =>
        {
            (body, key) = (b, r.Headers.GetValues("X-Key").Single());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(b.Contains("\"pass\":\"dung\"", StringComparison.Ordinal)
                    ? """{"ok":true,"data":{"maSo":"SV001","email":"sv@truong.edu.vn"}}""" : """{"ok":false}"""),
            };
        });
        var auth = new ExternalReaderAuth(new Clients(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<ExternalReaderAuth>.Instance);

        var ok = await auth.AuthenticateAsync(Api(), " sv001 ", "dung", CancellationToken.None);
        Assert.Equal((ExternalAuthOutcome.Success, "SV001", "sv@truong.edu.vn"), (ok.Outcome, ok.CardNo, ok.Email));
        Assert.Equal(("""{"user":"sv001","pass":"dung"}""", "k1"), (body, key));
        Assert.Equal(ExternalAuthOutcome.InvalidCredentials, (await auth.AuthenticateAsync(Api(), "sv001", "sai", CancellationToken.None)).Outcome);
        Assert.True(Api().IsExternal);
        Assert.Null(ReaderAuthSettings.Parse("không phải json"));
    }

    [Fact]
    public async Task Rejected_or_failing_api_maps_to_invalid_or_unavailable()
    {
        using var denied = new Stub((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var broken = new Stub((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var log = Microsoft.Extensions.Logging.Abstractions.NullLogger<ExternalReaderAuth>.Instance;
        Assert.Equal(ExternalAuthOutcome.InvalidCredentials,
            (await new ExternalReaderAuth(new Clients(denied), log).AuthenticateAsync(Api(), "a", "b", CancellationToken.None)).Outcome);
        Assert.Equal(ExternalAuthOutcome.Unavailable,
            (await new ExternalReaderAuth(new Clients(broken), log).AuthenticateAsync(Api(), "a", "b", CancellationToken.None)).Outcome);
        Assert.Equal(@"a\2a\28uid=x\29\5c", ExternalReaderAuth.EscapeFilter(@"a*(uid=x)\"));
    }
}
