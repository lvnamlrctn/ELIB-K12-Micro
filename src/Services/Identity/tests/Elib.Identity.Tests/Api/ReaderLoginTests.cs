using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Elib.Identity.Tests.Api;

/// <summary>Đăng nhập OPAC của bạn đọc: Authorization Code + PKCE (client elib-opac), đổi mật khẩu lần đầu, token bạn đọc, tách phiên với nhân viên.</summary>
public sealed partial class ReaderLoginTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>, IAsyncLifetime
{
    private const string Redirect = "https://th.truong.test/tai-khoan/callback";
    private static int _nextTenant = 1100;
    private readonly Dictionary<long, long> _admins = [];

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private async Task<long> NewTenantAsync()
    {
        var tenantId = Interlocked.Increment(ref _nextTenant);
        await factory.ProvisionTenantAsync(tenantId, "RL" + tenantId.ToString(CultureInfo.InvariantCulture));
        var admin = await factory.CreateClient().WithClaims(TestClaims.SuperAdmin)
            .PostAsJsonAsync($"/api/system/tenants/{tenantId}/admins", new CreateTenantAdminRequest("quantri", "Quản trị", null, "Matkhau123"));
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        _admins[tenantId] = (await admin.Content.ReadFromJsonAsync<UserDto>())!.Id;
        return tenantId;
    }

    /// <summary>Bạn đọc đồng bộ từ patron + thư viện đặt mật khẩu (null = mật khẩu là số thẻ).</summary>
    private async Task<Guid> ReaderAsync(long tenantId, string cardNo, string? password, int status = 2, long version = 1)
    {
        var readerId = Guid.NewGuid();
        await PublishAsync(new ReaderChanged { TenantId = tenantId, ReaderPublicId = readerId, CardNo = cardNo, FullName = "Học sinh " + cardNo, Status = status, Version = version });
        var set = await factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, _admins[tenantId]))
            .PutAsJsonAsync(U($"/api/reader-accounts/{readerId}/password"), new SetReaderPasswordRequest(password, password is null));
        Assert.True(set.IsSuccessStatusCode, await set.Content.ReadAsStringAsync());
        return readerId;
    }

    private async Task PublishAsync(ReaderChanged e)
    {
        await factory.Services.GetRequiredService<IBus>().Publish(e);
        for (var i = 0; i < 100; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            if (await db.ReaderAccounts.IgnoreQueryFilters().AnyAsync(a => a.ReaderPublicId == e.ReaderPublicId && a.Version >= e.Version)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Bạn đọc không được đồng bộ vào identity.");
    }

    /// <summary>Trình duyệt trên host của đơn vị (gateway gắn X-Tenant-Id có chữ ký).</summary>
    private HttpClient Browser(long? tenantId)
    {
        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        if (tenantId is { } id)
        {
            browser.DefaultRequestHeaders.Add("X-Tenant-Id", id.ToString(CultureInfo.InvariantCulture));
            browser.DefaultRequestHeaders.Add("X-Gw-Signature", Elib.BuildingBlocks.Tenancy.GatewaySignature.Compute(IdentityApiFactory.GatewayKey, id));
        }
        return browser;
    }

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    private static string Authorize(string clientId, string redirect, string challenge) => QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
    {
        ["client_id"] = clientId, ["response_type"] = "code", ["redirect_uri"] = redirect, ["scope"] = "openid profile offline_access elib-api",
        ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["state"] = "s1",
    });

    private static async Task<string> Antiforgery(HttpResponseMessage page) => AntiforgeryField().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;

    /// <summary>OPAC: authorize → đăng nhập bạn đọc → (đổi mật khẩu lần đầu) → code → token.</summary>
    private static async Task<JsonElement> SignInAsync(HttpClient browser, string cardNo, string password, string? newPassword = null)
    {
        var (verifier, challenge) = Pkce();
        var toLogin = await browser.GetAsync(U(Authorize("elib-opac", Redirect, challenge)));
        Assert.Equal(HttpStatusCode.Redirect, toLogin.StatusCode);
        var loginUrl = toLogin.Headers.Location!.PathAndQuery;
        Assert.StartsWith("/account/reader-login", loginUrl, StringComparison.Ordinal);
        var page = await browser.GetAsync(U(loginUrl));
        var returnUrl = QueryHelpers.ParseQuery(new Uri("http://x" + loginUrl).Query)["ReturnUrl"].ToString();

        var login = await browser.PostAsync(U("/account/reader-login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(page), ["cardNo"] = cardNo, ["password"] = password, ["returnUrl"] = returnUrl,
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var next = login.Headers.Location!.OriginalString;
        if (newPassword is not null)
        {
            Assert.Equal("/account/reader-password", next);
            var form = await browser.GetAsync(U(next));
            var changed = await browser.PostAsync(U(next), new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await Antiforgery(form), ["newPassword"] = newPassword, ["confirmPassword"] = newPassword,
            }));
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
            next = changed.Headers.Location!.OriginalString;
        }

        var toClient = await browser.GetAsync(U(next));
        Assert.Equal(HttpStatusCode.Redirect, toClient.StatusCode);
        var callback = toClient.Headers.Location!;
        Assert.StartsWith(Redirect, callback.ToString(), StringComparison.Ordinal);
        var token = await browser.PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = QueryHelpers.ParseQuery(callback.Query)["code"]!, ["redirect_uri"] = Redirect,
            ["client_id"] = "elib-opac", ["code_verifier"] = verifier,
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        return JsonDocument.Parse(await token.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> PostLogin(HttpClient browser, string cardNo, string password)
    {
        var page = await browser.GetAsync(U("/account/reader-login"));
        return await browser.PostAsync(U("/account/reader-login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(page), ["cardNo"] = cardNo, ["password"] = password, ["returnUrl"] = "/",
        }));
    }

    private HttpClient Bearer(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient browser, string refreshToken) =>
        browser.PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken, ["client_id"] = "elib-opac",
        }));

    [Fact]
    public async Task Card_number_password_must_be_changed_then_reader_token_carries_reader_claims_only()
    {
        var tenantId = await NewTenantAsync();
        var readerId = await ReaderAsync(tenantId, "HS-100", password: null);
        var browser = Browser(tenantId);

        // Mật khẩu mới trùng số thẻ bị từ chối ngay trên trang đổi mật khẩu.
        var page = await browser.GetAsync(U("/account/reader-login"));
        var login = await browser.PostAsync(U("/account/reader-login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(page), ["cardNo"] = "hs-100", ["password"] = "HS-100", ["returnUrl"] = "/",
        }));
        Assert.Equal("/account/reader-password", login.Headers.Location!.OriginalString);
        var form = await browser.GetAsync(U("/account/reader-password"));
        var same = await browser.PostAsync(U("/account/reader-password"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(form), ["newPassword"] = "hs-100", ["confirmPassword"] = "hs-100",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Contains("không được trùng số thẻ", await same.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var tokens = await SignInAsync(Browser(tenantId), "HS-100", "HS-100", newPassword: "sachhay2026");
        var jwt = new JsonWebToken(tokens.GetProperty("access_token").GetString()!);
        Assert.Equal(("reader", tenantId.ToString(CultureInfo.InvariantCulture), readerId.ToString(), "HS-100"),
            (jwt.GetClaim("sub_type").Value, jwt.GetClaim("tenant_id").Value, jwt.GetClaim("reader_id").Value, jwt.GetClaim("card_no").Value));
        Assert.False(jwt.TryGetClaim("pstamp", out _));

        var api = Bearer(tokens.GetProperty("access_token").GetString()!);
        var me = await api.GetFromJsonAsync<ReaderMeDto>(U("/api/reader/me"));
        Assert.Equal(("HS-100", false), (me!.CardNo, me.MustChangePassword));
        // Token bạn đọc không dùng được API của nhân viên (sub là dãy số riêng — không được tra thành nhân viên).
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(U("/api/me"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.GetAsync(U("/api/users"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.PostAsJsonAsync(U("/api/reader-accounts/Status"), new ReaderAccountStatusRequest([readerId]))).StatusCode);

        // Mật khẩu cũ (số thẻ) hết dùng được; nhật ký có đăng nhập và đổi mật khẩu.
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostLogin(Browser(tenantId), "HS-100", "HS-100")).StatusCode);
        Assert.Contains(factory.Published.OfType<AuditRecorded>(), a => a.TenantId == tenantId && a.Action == "READER_PASSWORD_CHANGE" && a.Actor.Kind == "reader");
        Assert.Contains(factory.Published.OfType<AuditRecorded>(), a => a.TenantId == tenantId && a.Action == "READER_LOGIN");

        // Làm mới token được; thẻ bị khoá ở patron thì không cấp token mới.
        var refresh = await Refresh(browser, tokens.GetProperty("refresh_token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync()).RootElement;
        await PublishAsync(new ReaderChanged { TenantId = tenantId, ReaderPublicId = readerId, CardNo = "HS-100", FullName = "Học sinh HS-100", Status = 1, Version = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, (await Refresh(browser, refreshed.GetProperty("refresh_token").GetString()!)).StatusCode);
        var locked = await PostLogin(Browser(tenantId), "HS-100", "sachhay2026");
        Assert.Contains("Thẻ bạn đọc đang bị khoá", await locked.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_password_locks_reader_and_failures_are_logged_without_password()
    {
        var tenantId = await NewTenantAsync();
        await ReaderAsync(tenantId, "HS-200", "matkhau200");
        for (var i = 0; i < 5; i++) await PostLogin(Browser(tenantId), "HS-200", "doan-mo-" + i);
        var locked = await PostLogin(Browser(tenantId), "HS-200", "matkhau200");
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("tạm khoá", await locked.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var failed = factory.Published.OfType<AuditRecorded>().Where(a => a.TenantId == tenantId && a.Action == "READER_LOGIN_FAILED").ToList();
        Assert.True(failed.Count >= 6);
        Assert.All(failed, a => Assert.DoesNotContain("doan-mo", a.Summary ?? "", StringComparison.Ordinal));

        // Số thẻ không có: cùng thông báo như sai mật khẩu.
        var unknown = await PostLogin(Browser(tenantId), "KHONG-CO", "matkhau200");
        Assert.Contains("Số thẻ hoặc mật khẩu không đúng", await unknown.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_and_staff_sessions_do_not_cross_and_reader_login_needs_a_tenant_host()
    {
        var tenantId = await NewTenantAsync();
        await ReaderAsync(tenantId, "HS-300", "matkhau300");
        var browser = Browser(tenantId);
        await SignInAsync(browser, "HS-300", "matkhau300", newPassword: "matkhau300-moi"); // mật khẩu thư viện đặt cũng phải đổi lần đầu

        // Phiên bạn đọc không mở được app quản trị.
        var (_, challenge) = Pkce();
        var admin = await browser.GetAsync(U(Authorize("elib-admin", IdentityApiFactory.AdminRedirect, challenge)));
        Assert.StartsWith("/account/login", admin.Headers.Location!.PathAndQuery, StringComparison.Ordinal);

        // Phiên nhân viên không mở được OPAC dưới danh nghĩa bạn đọc.
        var staffBrowser = Browser(tenantId);
        var loginPage = await staffBrowser.GetAsync(U("/account/login"));
        var staffLogin = await staffBrowser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(loginPage), ["userName"] = "quantri", ["password"] = "Matkhau123", ["returnUrl"] = "/",
        }));
        Assert.Equal(HttpStatusCode.Redirect, staffLogin.StatusCode);
        var opac = await staffBrowser.GetAsync(U(Authorize("elib-opac", Redirect, challenge)));
        Assert.StartsWith("/account/reader-login", opac.Headers.Location!.PathAndQuery, StringComparison.Ordinal);

        // Host hệ thống: không có trang đăng nhập bạn đọc, authorize OPAC trả lỗi về app.
        Assert.Equal(HttpStatusCode.NotFound, (await Browser(null).GetAsync(U("/account/reader-login"))).StatusCode);
        var noTenant = await Browser(null).GetAsync(U(Authorize("elib-opac", Redirect, challenge)));
        Assert.Contains("error=", noTenant.Headers.Location!.Query, StringComparison.Ordinal);

        // Phiên bạn đọc của đơn vị khác không dùng được ở host này.
        var otherTenant = await NewTenantAsync();
        var crossed = await Browser(otherTenant).GetAsync(U(Authorize("elib-opac", Redirect, challenge)));
        Assert.StartsWith("/account/reader-login", crossed.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_changes_own_password_with_current_password()
    {
        var tenantId = await NewTenantAsync();
        await ReaderAsync(tenantId, "HS-400", null);
        var tokens = await SignInAsync(Browser(tenantId), "HS-400", "hs-400", newPassword: "lan-dau-400");
        var api = Bearer(tokens.GetProperty("access_token").GetString()!);

        var wrong = await api.PostAsJsonAsync(U("/api/reader/me/password"), new ReaderChangePasswordRequest("sai", "moi-hon-400"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("PASSWORD_INCORRECT", await wrong.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var ok = await api.PostAsJsonAsync(U("/api/reader/me/password"), new ReaderChangePasswordRequest("lan-dau-400", "moi-hon-400"));
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        await SignInAsync(Browser(tenantId), "HS-400", "moi-hon-400");
    }

    [Fact]
    public async Task Login_pages_follow_the_language_cookie_of_opac_and_admin()
    {
        var tenantId = await NewTenantAsync();
        await ReaderAsync(tenantId, "HS-500", null);

        var vi = await (await Browser(tenantId).GetAsync(U("/account/reader-login"))).Content.ReadAsStringAsync();
        Assert.Contains("<html lang=\"vi\">", vi, StringComparison.Ordinal);
        Assert.Contains("Số thẻ hoặc email", vi, StringComparison.Ordinal);

        // Cookie elib_lang do OPAC/Admin ghi khi chọn ngôn ngữ.
        var browser = Browser(tenantId);
        browser.DefaultRequestHeaders.Add("Cookie", "elib_lang=en");
        var en = await (await browser.GetAsync(U("/account/reader-login"))).Content.ReadAsStringAsync();
        Assert.Contains("<html lang=\"en\">", en, StringComparison.Ordinal);
        Assert.Contains("Card number or email", en, StringComparison.Ordinal);
        Assert.Contains("lang=vi", en, StringComparison.Ordinal); // link đổi ngôn ngữ

        // ?lang=en ghi cookie → các bước sau (POST, đổi mật khẩu lần đầu) vẫn tiếng Anh, kể cả lỗi nghiệp vụ.
        var switched = Browser(tenantId);
        var page = await switched.GetAsync(U("/account/reader-login?lang=en"));
        Assert.Contains("elib_lang=en", string.Join(';', page.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
        var wrong = await switched.PostAsync(U("/account/reader-login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(page), ["cardNo"] = "HS-500", ["password"] = "sai", ["returnUrl"] = "/",
        }));
        Assert.Contains("Incorrect card number or password.", await wrong.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await PostLogin(switched, "HS-500", "HS-500");
        var form = await switched.GetAsync(U("/account/reader-password"));
        var same = await switched.PostAsync(U("/account/reader-password"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(form), ["newPassword"] = "hs-500", ["confirmPassword"] = "hs-500",
        }));
        Assert.Contains("must not be your card number", await same.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryField();
}
