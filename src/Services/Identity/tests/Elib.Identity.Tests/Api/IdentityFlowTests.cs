using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Application;
using Elib.Identity.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Elib.Identity.Tests.Api;

public sealed partial class IdentityFlowTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>, IAsyncLifetime
{
    private static int _nextTenant = 100;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient Browser() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static Uri U(string path) => new(path, UriKind.Relative);

    /// <summary>Đơn vị mới + quản trị đơn vị đầu tiên (do quản trị hệ thống tạo).</summary>
    private async Task<(long TenantId, string Code, UserDto Admin)> NewTenantWithAdminAsync(string password = "Matkhau123")
    {
        var tenantId = Interlocked.Increment(ref _nextTenant);
        var code = "TH" + tenantId.ToString(CultureInfo.InvariantCulture);
        await factory.ProvisionTenantAsync(tenantId, code);

        var response = await factory.CreateClient().WithClaims(TestClaims.SuperAdmin)
            .PostAsJsonAsync($"/api/system/tenants/{tenantId}/admins", new CreateTenantAdminRequest("quantri", "Quản trị " + code, null, password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (tenantId, code, (await response.Content.ReadFromJsonAsync<UserDto>())!);
    }

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    private static async Task<string> AntiforgeryTokenAsync(HttpResponseMessage page)
    {
        var html = await page.Content.ReadAsStringAsync();
        return AntiforgeryField().Match(html).Groups[1].Value;
    }

    /// <summary>Luồng Authorization Code + PKCE như Admin SPA: authorize → trang đăng nhập → authorize → code → token.</summary>
    private static async Task<JsonElement> SignInWithCodeFlowAsync(HttpClient browser, string tenant, string userName, string password,
        string redirectUri = IdentityApiFactory.AdminRedirect)
    {
        var (verifier, challenge) = Pkce();
        var authorize = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "elib-admin",
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid profile offline_access elib-api",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = "xyz",
            ["tenant"] = tenant,
        });

        var toLogin = await browser.GetAsync(U(authorize));
        Assert.Equal(HttpStatusCode.Redirect, toLogin.StatusCode);
        var loginUrl = toLogin.Headers.Location!.PathAndQuery;
        Assert.StartsWith("/account/login", loginUrl, StringComparison.Ordinal);

        var page = await browser.GetAsync(U(loginUrl));
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{tenant}\"", html, StringComparison.Ordinal); // mã đơn vị điền sẵn từ yêu cầu authorize
        var returnUrl = QueryHelpers.ParseQuery(new Uri("http://x" + loginUrl).Query)["ReturnUrl"].ToString();

        var login = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryField().Match(html).Groups[1].Value,
            ["tenant"] = tenant,
            ["userName"] = userName,
            ["password"] = password,
            ["returnUrl"] = returnUrl,
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var toClient = await browser.GetAsync(U(login.Headers.Location!.OriginalString));
        Assert.Equal(HttpStatusCode.Redirect, toClient.StatusCode);
        var callback = toClient.Headers.Location!;
        Assert.StartsWith(redirectUri, callback.ToString(), StringComparison.Ordinal);
        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.Equal("xyz", query["state"]);

        var token = await browser.PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = query["code"]!,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = "elib-admin",
            ["code_verifier"] = verifier,
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        return JsonDocument.Parse(await token.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    /// <summary>Xác minh access token như một service khác: chữ ký RS256 qua JWKS, issuer, audience elib-api.</summary>
    private async Task<JsonWebToken> ValidateLikeAnotherServiceAsync(string accessToken)
    {
        var jwks = new JsonWebKeySet(await factory.CreateClient().GetStringAsync(U("/.well-known/jwks")));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidIssuer = IdentityApiFactory.Issuer,
            ValidAudience = "elib-api",
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidAlgorithms = ["RS256"],
        });
        Assert.True(result.IsValid, result.Exception?.Message);
        return (JsonWebToken)result.SecurityToken;
    }

    [Fact]
    public async Task Code_flow_issues_verifiable_jwt_with_tenant_claims_and_refresh_works()
    {
        var (tenantId, code, admin) = await NewTenantWithAdminAsync();
        var browser = Browser();

        var tokens = await SignInWithCodeFlowAsync(browser, code.ToLowerInvariant(), "QuanTri", "Matkhau123");
        var jwt = await ValidateLikeAnotherServiceAsync(tokens.GetProperty("access_token").GetString()!);

        Assert.Equal(admin.Id.ToString(CultureInfo.InvariantCulture), jwt.Subject);
        Assert.Equal(tenantId.ToString(CultureInfo.InvariantCulture), jwt.GetClaim("tenant_id").Value);
        Assert.Equal("staff", jwt.GetClaim("sub_type").Value);
        Assert.False(string.IsNullOrEmpty(jwt.GetClaim("pstamp").Value));
        Assert.True(tokens.TryGetProperty("id_token", out _));

        // Token dùng được cho API (OpenIddict validation), quyền từ vai trò "Quản trị đơn vị".
        var api = factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        var me = (await api.GetFromJsonAsync<MeDto>(U("/api/me")))!;
        Assert.Equal(tenantId, me.TenantId);
        Assert.Equal(["*"], me.Permissions);
        Assert.True(me.MustChangePassword);

        var refreshed = await browser.PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.GetProperty("refresh_token").GetString()!,
            ["client_id"] = "elib-admin",
        }));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task Code_flow_accepts_redirect_uri_on_tenant_subdomain_pattern()
    {
        var (_, code, _) = await NewTenantWithAdminAsync();
        var redirect = $"https://{code.ToLowerInvariant()}.truong.test/admin/callback";

        var tokens = await SignInWithCodeFlowAsync(Browser(), code, "quantri", "Matkhau123", redirect);

        Assert.True(tokens.TryGetProperty("access_token", out _));
    }

    [Theory]
    [InlineData("https://evil.example/admin/callback")]
    [InlineData("https://a.b.truong.test/admin/callback")]       // '*' chỉ là MỘT nhãn
    [InlineData("https://x@th1.truong.test/admin/callback")]     // userinfo
    [InlineData("https://th1.truong.test.evil.example/admin/callback")]
    [InlineData("https://th1.truong.test/admin/callback/../../steal")]
    [InlineData("http://th1.truong.test/admin/callback")]        // sai scheme
    [InlineData("https://th1.truong.test/admin/callback?next=x")]
    public async Task Authorize_rejects_redirect_uri_outside_registered_pattern(string redirect)
    {
        var (_, challenge) = Pkce();
        var response = await Browser().GetAsync(U(QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "elib-admin", ["response_type"] = "code", ["redirect_uri"] = redirect, ["scope"] = "openid elib-api",
            ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["state"] = "s",
        })));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location); // tuyệt đối không chuyển hướng về URI lạ
    }

    /// <summary>Trình duyệt trên host của đơn vị: gateway gắn X-Tenant-Id có chữ ký cho mọi request.</summary>
    private HttpClient BrowserOnTenantHost(long tenantId)
    {
        var browser = Browser();
        browser.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        browser.DefaultRequestHeaders.Add("X-Gw-Signature", Elib.BuildingBlocks.Tenancy.GatewaySignature.Compute(IdentityApiFactory.GatewayKey, tenantId));
        return browser;
    }

    [Fact]
    public async Task Login_on_tenant_host_is_bound_to_that_tenant()
    {
        var (tenantA, _, _) = await NewTenantWithAdminAsync();
        var (_, codeB, _) = await NewTenantWithAdminAsync();
        var browser = BrowserOnTenantHost(tenantA);

        var page = await browser.GetAsync(U("/account/login"));
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Trường TH" + tenantA, html, StringComparison.Ordinal); // tên đơn vị của host
        Assert.DoesNotContain("name=\"tenant\"", html, StringComparison.Ordinal); // không cho chọn đơn vị
        var token = AntiforgeryField().Match(html).Groups[1].Value;

        // Gửi kèm mã đơn vị B (cùng tên đăng nhập/mật khẩu với admin của B) → vẫn chỉ xét đơn vị A của host.
        // Admin A và B trùng tên/mật khẩu nên phải kiểm qua claim: phiên phải thuộc A.
        var login = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["tenant"] = codeB, ["userName"] = "quantri", ["password"] = "Matkhau123", ["returnUrl"] = "/",
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var tokens = await SignInWithCodeFlowOnHostAsync(browser);
        var jwt = await ValidateLikeAnotherServiceAsync(tokens);
        Assert.Equal(tenantA.ToString(CultureInfo.InvariantCulture), jwt.GetClaim("tenant_id").Value);
    }

    [Fact]
    public async Task System_admin_cannot_sign_in_on_a_tenant_host()
    {
        var (tenantId, _, _) = await NewTenantWithAdminAsync();
        var browser = BrowserOnTenantHost(tenantId);
        var token = await AntiforgeryTokenAsync(await browser.GetAsync(U("/account/login")));

        var login = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["tenant"] = "", ["userName"] = "sysadmin", ["password"] = "Sysadmin123", ["returnUrl"] = "/",
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    /// <summary>Đã có phiên đăng nhập (cookie) trên host: authorize cấp code ngay, đổi lấy access token.</summary>
    private static async Task<string> SignInWithCodeFlowOnHostAsync(HttpClient browser)
    {
        var (verifier, challenge) = Pkce();
        var authorize = await browser.GetAsync(U(QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "elib-admin", ["response_type"] = "code", ["redirect_uri"] = IdentityApiFactory.AdminRedirect,
            ["scope"] = "openid elib-api", ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["state"] = "s",
        })));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"].ToString();
        var token = await browser.PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = IdentityApiFactory.AdminRedirect,
            ["client_id"] = "elib-admin", ["code_verifier"] = verifier,
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        return JsonDocument.Parse(await token.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetString()!;
    }

    [Fact]
    public async Task User_changes_own_password_and_must_change_flag_is_cleared()
    {
        var (tenantId, code, admin) = await NewTenantWithAdminAsync();
        var me = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, admin.Id));

        var wrong = await me.PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("SaiMatKhau1", "MatKhauMoi456"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("PASSWORD_INCORRECT", await wrong.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var weak = await me.PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("Matkhau123", "ngan"));
        Assert.Contains("PASSWORD_WEAK", await weak.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var same = await me.PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("Matkhau123", "Matkhau123"));
        Assert.Contains("PASSWORD_REUSED", await same.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, (await me.PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("Matkhau123", "MatKhauMoi456"))).StatusCode);
        Assert.False((await me.GetFromJsonAsync<MeDto>(U("/api/me")))!.MustChangePassword);

        // Mật khẩu cũ hết hiệu lực, mật khẩu mới đăng nhập được.
        var tokens = await SignInWithCodeFlowAsync(Browser(), code, "quantri", "MatKhauMoi456");
        Assert.True(tokens.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task Wrong_current_password_counts_towards_lockout()
    {
        var (tenantId, _, admin) = await NewTenantWithAdminAsync();
        var me = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, admin.Id));

        for (var i = 0; i < 5; i++) await me.PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("SaiMatKhau1", "MatKhauMoi456"));

        var correct = await me.PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("Matkhau123", "MatKhauMoi456"));
        Assert.Equal((HttpStatusCode)423, correct.StatusCode);
    }

    [Fact]
    public async Task Service_token_cannot_change_password()
    {
        var response = await factory.CreateClient().WithClaims(TestClaims.Service)
            .PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("x", "MatKhauMoi456"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_is_refused_after_account_is_disabled()
    {
        var (tenantId, code, admin) = await NewTenantWithAdminAsync();
        var browser = Browser();
        var tokens = await SignInWithCodeFlowAsync(browser, code, "quantri", "Matkhau123");

        var disable = await factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, admin.Id))
            .PutAsJsonAsync($"/api/users/{admin.PublicId}", new UpdateUserRequest(admin.FullName, null, null, IsActive: false));
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        Assert.Contains(factory.Published.OfType<PermissionChanged>(), e => e.TenantId == tenantId && e.UserIds.Contains(admin.Id));

        var refreshed = await browser.PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.GetProperty("refresh_token").GetString()!,
            ["client_id"] = "elib-admin",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, refreshed.StatusCode);
        Assert.Contains("invalid_grant", await refreshed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_password_five_times_locks_account()
    {
        var (_, code, _) = await NewTenantWithAdminAsync();
        var browser = Browser();
        var page = await browser.GetAsync(U("/account/login"));
        var token = await AntiforgeryTokenAsync(page);

        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["tenant"] = code, ["userName"] = "quantri", ["password"] = "SaiMatKhau1", ["returnUrl"] = "/",
            }));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, last.StatusCode);
        Assert.Contains("tạm khoá", await last.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Đúng mật khẩu cũng không vào được trong thời gian khoá.
        var correct = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["tenant"] = code, ["userName"] = "quantri", ["password"] = "Matkhau123", ["returnUrl"] = "/",
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_and_wrong_tenant_get_the_same_generic_message()
    {
        var (_, code, _) = await NewTenantWithAdminAsync();
        var browser = Browser();
        var token = await AntiforgeryTokenAsync(await browser.GetAsync(U("/account/login")));

        var unknown = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["tenant"] = code, ["userName"] = "khongco", ["password"] = "Matkhau123", ["returnUrl"] = "/",
        }));
        Assert.Contains("Tên đăng nhập hoặc mật khẩu không đúng", await unknown.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var otherTenant = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["tenant"] = "KHONG-TON-TAI", ["userName"] = "quantri", ["password"] = "Matkhau123", ["returnUrl"] = "/",
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, otherTenant.StatusCode);
    }

    [Fact]
    public async Task Login_without_antiforgery_token_is_rejected()
    {
        var response = await Browser().PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "sysadmin", ["password"] = "Sysadmin123", ["returnUrl"] = "/",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Open_redirect_is_prevented()
    {
        var browser = Browser();
        var token = await AntiforgeryTokenAsync(await browser.GetAsync(U("/account/login")));
        var login = await browser.PostAsync(U("/account/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["userName"] = "sysadmin", ["password"] = "Sysadmin123", ["returnUrl"] = "https://evil.example/",
        }));
        Assert.True(login.StatusCode == HttpStatusCode.Redirect, $"{login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
        Assert.Equal("/", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Service_token_reads_effective_permissions()
    {
        var (tenantId, _, admin) = await NewTenantWithAdminAsync();
        var token = await factory.CreateClient().PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "svc-gateway",
            ["client_secret"] = IdentityApiFactory.GatewaySecret,
            ["scope"] = "elib-api",
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        var accessToken = JsonDocument.Parse(await token.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetString()!;
        var jwt = await ValidateLikeAnotherServiceAsync(accessToken);
        Assert.Equal("service", jwt.GetClaim("sub_type").Value);

        var service = factory.CreateClient();
        service.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        Assert.Equal(["*"], (await service.GetFromJsonAsync<string[]>(U($"/internal/permissions?tenantId={tenantId}&userId={admin.Id}")))!);

        // Nhân viên đơn vị không gọi được API nội bộ.
        var staff = await factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, admin.Id)).GetAsync(U($"/internal/permissions?tenantId={tenantId}&userId={admin.Id}"));
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
        var systemAdmin = await factory.CreateClient().WithClaims(TestClaims.SuperAdmin).GetAsync(U($"/internal/permissions?tenantId={tenantId}&userId={admin.Id}"));
        Assert.Equal(HttpStatusCode.Forbidden, systemAdmin.StatusCode);

        // Service token (lấy được từ /connect/token công khai nếu lộ secret) KHÔNG dùng được API quản trị nền tảng.
        var createAdmin = await service.PostAsJsonAsync($"/api/system/tenants/{tenantId}/admins", new CreateTenantAdminRequest("chiemquyen", "X", null, "Matkhau123"));
        Assert.Equal(HttpStatusCode.Forbidden, createAdmin.StatusCode);
    }

    [Fact]
    public async Task Wrong_client_secret_is_rejected()
    {
        var token = await factory.CreateClient().PostAsync(U("/connect/token"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = "svc-gateway", ["client_secret"] = "sai", ["scope"] = "elib-api",
        }));
        Assert.NotEqual(HttpStatusCode.OK, token.StatusCode);
    }

    [Fact]
    public async Task Tenant_roles_drive_permissions_and_changes_are_published()
    {
        var (tenantId, _, admin) = await NewTenantWithAdminAsync();
        var asAdmin = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, admin.Id));

        var roles = (await asAdmin.GetFromJsonAsync<List<RoleDto>>(U("/api/roles")))!;
        Assert.Contains(roles, r => r.Name == "Quản trị đơn vị" && r.IsBuiltIn);
        Assert.Contains(roles, r => r.Name == "Thủ thư" && r.IsBuiltIn);

        var created = await asAdmin.PostAsJsonAsync("/api/roles", new RoleRequest("Mượn trả", null, ["orgs:add", "USER:view"]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var role = (await created.Content.ReadFromJsonAsync<RoleDto>())!;

        var userResponse = await asAdmin.PostAsJsonAsync("/api/users", new CreateUserRequest("thuthu1", "Thủ thư 1", null, null, "Matkhau123", [role.PublicId]));
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
        var user = (await userResponse.Content.ReadFromJsonAsync<UserDto>())!;

        var asLibrarian = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, user.Id));
        Assert.Equal(HttpStatusCode.OK, (await asLibrarian.GetAsync(U("/api/users"))).StatusCode);       // có USER:view
        Assert.Equal(HttpStatusCode.Forbidden, (await asLibrarian.GetAsync(U("/api/roles"))).StatusCode); // không có ROLE:view

        var update = await asAdmin.PutAsJsonAsync($"/api/roles/{role.PublicId}", new RoleRequest("Mượn trả", null, ["ORGS:add"]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Contains(factory.Published.OfType<PermissionChanged>(), e => e.TenantId == tenantId && e.AllUsersOfTenant);

        var duplicate = await asAdmin.PostAsJsonAsync("/api/users", new CreateUserRequest("THUTHU1", "Trùng", null, null, "Matkhau123", null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Role_permissions_must_come_from_the_catalog_and_admin_role_is_locked()
    {
        var (tenantId, _, admin) = await NewTenantWithAdminAsync();
        var asAdmin = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, admin.Id));

        var catalog = (await asAdmin.GetFromJsonAsync<List<PermissionModule>>(U("/api/permission-catalog")))!;
        Assert.Contains(catalog, m => m.Code == "NOTIFICATION_LOGS" && m.Actions.SequenceEqual(["view"]));
        Assert.Contains(catalog, m => m.Code == "ORGS" && m.Group == "Quản lý bạn đọc / Tham số bạn đọc");

        async Task<string?> Code(HttpResponseMessage r) =>
            System.Text.Json.JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();
        Assert.Equal("PERMISSION_UNKNOWN", await Code(await asAdmin.PostAsJsonAsync("/api/roles", new RoleRequest("A", null, ["CIRCULATION:add"]))));
        Assert.Equal("PERMISSION_UNKNOWN", await Code(await asAdmin.PostAsJsonAsync("/api/roles", new RoleRequest("B", null, ["NOTIFICATION_LOGS:delete"]))));
        Assert.Equal("PERMISSION_ALL_RESERVED", await Code(await asAdmin.PostAsJsonAsync("/api/roles", new RoleRequest("C", null, ["*"]))));

        var roles = (await asAdmin.GetFromJsonAsync<List<RoleDto>>(U("/api/roles")))!;
        var adminRole = roles.Single(r => r.Name == "Quản trị đơn vị");
        var downgrade = await asAdmin.PutAsJsonAsync($"/api/roles/{adminRole.PublicId}", new RoleRequest("Quản trị đơn vị", null, ["ORGS:view"]));
        Assert.Equal("ROLE_BUILT_IN_LOCKED", await Code(downgrade));
        var describe = await asAdmin.PutAsJsonAsync($"/api/roles/{adminRole.PublicId}", new RoleRequest("Quản trị đơn vị", "Mô tả mới", ["*"]));
        Assert.Equal(HttpStatusCode.OK, describe.StatusCode);

        var librarian = roles.Single(r => r.Name == "Thủ thư");
        var grant = await asAdmin.PutAsJsonAsync($"/api/roles/{librarian.PublicId}", new RoleRequest("Thủ thư", null, ["ORGS:view", "orgs:EDIT", "NOTIFICATION_LOGS:view"]));
        Assert.Equal(["NOTIFICATION_LOGS:view", "ORGS:edit", "ORGS:view"], (await grant.Content.ReadFromJsonAsync<RoleDto>())!.Permissions);
    }

    [Fact]
    public async Task System_admin_views_a_tenant_read_only_through_a_one_time_ticket()
    {
        var (tenantId, code, _) = await NewTenantWithAdminAsync();
        var sysadmin = factory.CreateClient().WithClaims(TestClaims.SuperAdmin);

        var noReason = await sysadmin.PostAsJsonAsync("/api/system/impersonation", new StartImpersonationRequest(tenantId, " "));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var asStaff = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await asStaff.PostAsJsonAsync("/api/system/impersonation", new StartImpersonationRequest(tenantId, "hỗ trợ kỹ thuật"))).StatusCode);

        var start = await sysadmin.PostAsJsonAsync("/api/system/impersonation", new StartImpersonationRequest(tenantId, "Hỗ trợ trường cấu hình email"));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var ticketUrl = new Uri((await start.Content.ReadFromJsonAsync<ImpersonationTicketDto>())!.Url);
        Assert.Equal($"{code.ToLowerInvariant()}.truong.test", ticketUrl.Host); // suy từ mẫu redirect URI của elib-admin
        Assert.Equal("/account/impersonate", ticketUrl.AbsolutePath);

        // Mở trên host của đơn vị khác: bị từ chối và vé bị huỷ.
        var (otherTenant, _, _) = await NewTenantWithAdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await BrowserOnTenantHost(otherTenant).GetAsync(U(ticketUrl.PathAndQuery))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await BrowserOnTenantHost(tenantId).GetAsync(U(ticketUrl.PathAndQuery))).StatusCode);

        start = await sysadmin.PostAsJsonAsync("/api/system/impersonation", new StartImpersonationRequest(tenantId, "Hỗ trợ trường cấu hình email"));
        ticketUrl = new Uri((await start.Content.ReadFromJsonAsync<ImpersonationTicketDto>())!.Url);
        var browser = BrowserOnTenantHost(tenantId);
        var redeem = await browser.GetAsync(U(ticketUrl.PathAndQuery));
        Assert.Equal(HttpStatusCode.Redirect, redeem.StatusCode);
        Assert.Equal("/admin/", redeem.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await BrowserOnTenantHost(tenantId).GetAsync(U(ticketUrl.PathAndQuery))).StatusCode); // dùng một lần
        Assert.Contains(factory.Published.OfType<AuditRecorded>(),
            e => e.TenantId == tenantId && e.Action == "IMPERSONATION_STARTED" && e.Summary!.Contains("cấu hình email", StringComparison.Ordinal));

        var accessToken = await SignInWithCodeFlowOnHostAsync(browser);
        var jwt = await ValidateLikeAnotherServiceAsync(accessToken);
        Assert.Equal(tenantId.ToString(CultureInfo.InvariantCulture), jwt.GetClaim("tenant_id").Value);
        Assert.Equal("readonly", jwt.GetClaim("imp").Value);

        var api = factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        api.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        api.DefaultRequestHeaders.Add("X-Gw-Signature", Elib.BuildingBlocks.Tenancy.GatewaySignature.Compute(IdentityApiFactory.GatewayKey, tenantId));
        var me = (await api.GetFromJsonAsync<MeDto>(U("/api/me")))!;
        Assert.True(me.ReadOnly);
        Assert.Equal(["*:view"], me.Permissions);
        Assert.Equal(tenantId, me.TenantId);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync(U("/api/users"))).StatusCode);
        var write = await api.PostAsJsonAsync("/api/users", new CreateUserRequest("hacker", "X", null, null, "Matkhau123", null));
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Contains("IMPERSONATION_READONLY", await write.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await api.PostAsJsonAsync("/api/me/password", new ChangePasswordRequest("Sysadmin123", "Matkhau999"))).StatusCode);
    }

    [Fact]
    public async Task Users_of_one_tenant_are_invisible_to_another()
    {
        var (tenantA, _, adminA) = await NewTenantWithAdminAsync();
        var (tenantB, _, adminB) = await NewTenantWithAdminAsync();

        var listA = (await factory.CreateClient().WithClaims(TestClaims.Staff(tenantA, adminA.Id)).GetFromJsonAsync<PagedResult<UserDto>>(U("/api/users")))!;
        Assert.DoesNotContain(listA.Items, u => u.Id == adminB.Id);

        var crossRead = await factory.CreateClient().WithClaims(TestClaims.Staff(tenantA, adminA.Id)).GetAsync(U($"/api/users/{adminB.PublicId}"));
        Assert.Equal(HttpStatusCode.NotFound, crossRead.StatusCode);
        _ = tenantB;
    }

    [Fact]
    public async Task Tenant_provisioning_seeds_built_in_roles_and_reports_back()
    {
        var tenantId = Interlocked.Increment(ref _nextTenant);
        await factory.ProvisionTenantAsync(tenantId, "SEED" + tenantId.ToString(CultureInfo.InvariantCulture));

        var seeded = Assert.Single(factory.Published.OfType<TenantSeeded>(), s => s.TenantId == tenantId);
        Assert.True(seeded.Succeeded);
        Assert.Equal("identity", seeded.Service);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryField();
}
