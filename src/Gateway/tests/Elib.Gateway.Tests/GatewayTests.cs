using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;

namespace Elib.Gateway.Tests;

public sealed class GatewayTests : IAsyncLifetime
{
    private EchoBackend _backend = null!;
    private GatewayFactory _gateway = null!;

    public async Task InitializeAsync()
    {
        _backend = await EchoBackend.StartAsync();
        _gateway = new GatewayFactory(_backend.Address);
        _gateway.Directory.Tenants["truong-a"] = new TenantInfo(11, "TH-A", "truong-a", "Active", ["SEARCH", "CIRCULATION", "CATALOG", "HOLDINGS"]);
        _gateway.Directory.Tenants["truong-b"] = new TenantInfo(12, "TH-B", "truong-b", "Active", ["SEARCH"]);
        _gateway.Directory.Tenants["truong-c"] = new TenantInfo(13, "TH-C", "truong-c", "Suspended", ["SEARCH"]);
        _gateway.Directory.Tenants["truong-d"] = new TenantInfo(14, "TH-D", "truong-d", "Provisioning", []);
    }

    public async Task DisposeAsync()
    {
        await _gateway.DisposeAsync();
        await _backend.DisposeAsync();
    }

    private sealed record Echo(string Path, Dictionary<string, string> Headers);

    private static async Task<string?> Code(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private static Uri U(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task Licensed_anonymous_route_is_forwarded_with_signed_tenant_header_and_rewritten_path()
    {
        var response = await _gateway.ClientFor("truong-a.thuvientn.vn").GetAsync(U("/api/opac/search/books?q=toan"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = (await response.Content.ReadFromJsonAsync<Echo>())!;
        Assert.Equal("/api/books", echo.Path);
        Assert.Equal("11", echo.Headers["X-Tenant-Id"]);
        Assert.True(GatewaySignature.Verify(GatewayFactory.SigningKey, 11, echo.Headers["X-Gw-Signature"]));
        Assert.True(Guid.TryParse(echo.Headers["X-Correlation-Id"], out _));
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Client_supplied_tenant_headers_are_replaced()
    {
        var client = _gateway.ClientFor("truong-a.thuvientn.vn");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "12");
        client.DefaultRequestHeaders.Add("X-Gw-Signature", "forged");

        var echo = (await client.GetFromJsonAsync<Echo>(U("/api/opac/search/books")))!;

        Assert.Equal("11", echo.Headers["X-Tenant-Id"]);
        Assert.NotEqual("forged", echo.Headers["X-Gw-Signature"]);
    }

    [Fact]
    public async Task Tenant_service_timeout_is_503_problem_not_unhandled_error()
    {
        _gateway.Directory.Unavailable = true;
        var response = await _gateway.ClientFor("truong-a.thuvientn.vn").GetAsync(U("/api/opac/search/books"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("TENANT_DIRECTORY_UNAVAILABLE", await Code(response));
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Unknown_host_is_404()
    {
        var response = await _gateway.ClientFor("khong-co.thuvientn.vn").GetAsync(U("/api/opac/search/books"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("TENANT_NOT_FOUND", await Code(response));
    }

    [Fact]
    public async Task Unlicensed_module_is_403()
    {
        var response = await _gateway.ClientFor("truong-b.thuvientn.vn", TestClaims.Staff(12)).GetAsync(U("/api/admin/circulation/loans"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("MODULE_NOT_LICENSED", await Code(response));
    }

    [Theory]
    [InlineData("truong-c", HttpStatusCode.Forbidden, "TENANT_SUSPENDED")]
    [InlineData("truong-d", HttpStatusCode.ServiceUnavailable, "TENANT_NOT_READY")]
    public async Task Inactive_tenant_is_blocked(string host, HttpStatusCode status, string code)
    {
        var response = await _gateway.ClientFor($"{host}.thuvientn.vn").GetAsync(U("/api/opac/search/books"));
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, await Code(response));
    }

    [Fact]
    public async Task Admin_route_requires_login()
    {
        var response = await _gateway.ClientFor("truong-a.thuvientn.vn").GetAsync(U("/api/admin/circulation/loans"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_of_another_tenant_is_rejected()
    {
        var response = await _gateway.ClientFor("truong-a.thuvientn.vn", TestClaims.Staff(12)).GetAsync(U("/api/admin/circulation/loans"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("TENANT_MISMATCH", await Code(response));
    }

    [Fact]
    public async Task Token_of_same_tenant_is_forwarded_with_authorization()
    {
        var response = await _gateway.ClientFor("truong-a.thuvientn.vn", TestClaims.Staff(11)).GetAsync(U("/api/admin/circulation/loans"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = (await response.Content.ReadFromJsonAsync<Echo>())!;
        Assert.Equal("/api/loans", echo.Path);
        Assert.Equal("11", echo.Headers["X-Tenant-Id"]);
        Assert.True(echo.Headers.ContainsKey(HeaderAuthHandler.HeaderName)); // service tự xác thực lại (ở thật là Authorization: Bearer)
    }

    [Fact]
    public async Task System_routes_only_on_system_host_and_without_tenant_header()
    {
        var onSystemHost = await _gateway.ClientFor("quantri.thuvientn.vn", TestClaims.SuperAdmin).GetAsync(U("/api/system/tenant/tenants"));
        Assert.Equal(HttpStatusCode.OK, onSystemHost.StatusCode);
        var echo = (await onSystemHost.Content.ReadFromJsonAsync<Echo>())!;
        Assert.Equal("/api/tenants", echo.Path);
        Assert.False(echo.Headers.ContainsKey("X-Tenant-Id"));

        // Token nhân viên đơn vị không dùng được trên host hệ thống.
        var tenantToken = await _gateway.ClientFor("quantri.thuvientn.vn", TestClaims.Staff(11)).GetAsync(U("/api/system/tenant/tenants"));
        Assert.Equal("TENANT_MISMATCH", await Code(tenantToken));

        // Route cần đơn vị không dùng được trên host hệ thống.
        var tenantRoute = await _gateway.ClientFor("quantri.thuvientn.vn").GetAsync(U("/api/opac/search/books"));
        Assert.Equal(HttpStatusCode.NotFound, tenantRoute.StatusCode);
    }

    [Fact]
    public async Task Identity_login_works_on_tenant_and_system_hosts()
    {
        var onTenant = (await _gateway.ClientFor("truong-a.thuvientn.vn").GetFromJsonAsync<Echo>(U("/account/login")))!;
        Assert.Equal("11", onTenant.Headers["X-Tenant-Id"]);

        var onSystem = await _gateway.ClientFor("quantri.thuvientn.vn").GetAsync(U("/connect/authorize?client_id=x"));
        Assert.Equal(HttpStatusCode.OK, onSystem.StatusCode);

        // Host lạ vẫn tới được trang đăng nhập (đơn vị tuỳ chọn), không bị 404.
        var unknown = await _gateway.ClientFor("khong-co.thuvientn.vn").GetAsync(U("/account/login"));
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
    }

    [Fact]
    public async Task Admin_app_and_me_endpoint_are_served_on_system_and_tenant_hosts()
    {
        // App tĩnh: ẩn danh, đường dẫn giữ nguyên.
        var onSystem = (await _gateway.ClientFor("quantri.thuvientn.vn").GetFromJsonAsync<Echo>(U("/admin/don-vi")))!;
        Assert.Equal("/admin/don-vi", onSystem.Path);
        var onTenant = (await _gateway.ClientFor("truong-b.thuvientn.vn").GetFromJsonAsync<Echo>(U("/admin/")))!;
        Assert.Equal("11", (await _gateway.ClientFor("truong-a.thuvientn.vn").GetFromJsonAsync<Echo>(U("/admin/")))!.Headers["X-Tenant-Id"]);
        Assert.Equal("/admin/", onTenant.Path);

        // /api/me: quản trị hệ thống trên host hệ thống, nhân viên trên host đơn vị của mình.
        Assert.Equal(HttpStatusCode.OK, (await _gateway.ClientFor("quantri.thuvientn.vn", TestClaims.SuperAdmin).GetAsync(U("/api/me"))).StatusCode);
        var staffMe = (await _gateway.ClientFor("truong-a.thuvientn.vn", TestClaims.Staff(11)).GetFromJsonAsync<Echo>(U("/api/me/password")))!;
        Assert.Equal("/api/me/password", staffMe.Path);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _gateway.ClientFor("truong-a.thuvientn.vn").GetAsync(U("/api/me"))).StatusCode);
        Assert.Equal("TENANT_MISMATCH", await Code(await _gateway.ClientFor("truong-a.thuvientn.vn", TestClaims.Staff(12)).GetAsync(U("/api/me"))));

        var root = await _gateway.ClientFor("quantri.thuvientn.vn").GetAsync(U("/"));
        Assert.Equal("/admin/", root.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Storage_route_reaches_minio_with_its_own_host_and_without_credentials()
    {
        var client = _gateway.ClientFor("truong-a.thuvientn.vn");
        using var request = new HttpRequestMessage(HttpMethod.Get, U("/s3/media-public/11/tenant-logo/2026/10/a.png?X-Amz-Signature=ff"));
        request.Headers.Add("Cookie", "session=secret");
        request.Headers.TryAddWithoutValidation("Authorization", "AWS4-HMAC-SHA256 Credential=forged");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var echo = (await response.Content.ReadFromJsonAsync<Echo>())!;
        Assert.Equal("/media-public/11/tenant-logo/2026/10/a.png", echo.Path);
        // Chữ ký URL do MinIO kiểm theo Host = địa chỉ MinIO: gateway không được giữ host gốc hay thêm X-Forwarded-Host.
        Assert.Equal(new Uri(_backend.Address).Authority, echo.Headers["Host"]);
        Assert.DoesNotContain(echo.Headers.Keys, k => k.StartsWith("X-Forwarded", StringComparison.OrdinalIgnoreCase));
        Assert.False(echo.Headers.ContainsKey("Authorization"));
        Assert.False(echo.Headers.ContainsKey("Cookie"));
        Assert.False(echo.Headers.ContainsKey("X-Tenant-Id"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);

        // Chỉ GET/HEAD/PUT (tải/upload qua URL ký) — không công bố các thao tác quản trị bucket.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync(U("/s3/media-public/11/x.png"))).StatusCode);
    }

    [Fact]
    public async Task Opac_is_served_at_the_root_of_tenant_hosts_only()
    {
        var tenantHost = _gateway.ClientFor("truong-b.thuvientn.vn");
        var root = (await tenantHost.GetFromJsonAsync<Echo>(U("/")))!;
        Assert.Equal("/", root.Path);
        Assert.Equal("12", root.Headers["X-Tenant-Id"]);
        Assert.Equal("/tim-kiem", (await tenantHost.GetFromJsonAsync<Echo>(U("/tim-kiem?q=toan")))!.Path); // route SPA

        // Route cụ thể vẫn ưu tiên hơn route bắt mọi đường dẫn của OPAC.
        Assert.Equal("/api/features", (await tenantHost.GetFromJsonAsync<Echo>(U("/api/opac/tenant/features")))!.Path);
        Assert.Equal(HttpStatusCode.Unauthorized, (await tenantHost.GetAsync(U("/api/admin/identity/users"))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await tenantHost.PostAsync(U("/khong-co"), null)).StatusCode);

        // Host hệ thống: gốc → Admin; đường dẫn lạ không có OPAC. Đơn vị tạm ngưng: OPAC bị chặn.
        var system = await _gateway.ClientFor("quantri.thuvientn.vn").GetAsync(U("/"));
        Assert.Equal("/admin/", system.Headers.Location!.OriginalString);
        Assert.Equal("TENANT_NOT_FOUND", await Code(await _gateway.ClientFor("quantri.thuvientn.vn").GetAsync(U("/tim-kiem"))));
        Assert.Equal("TENANT_SUSPENDED", await Code(await _gateway.ClientFor("truong-c.thuvientn.vn").GetAsync(U("/"))));
    }

    [Fact]
    public async Task Media_and_manifest_routes_are_mapped()
    {
        var admin = (await _gateway.ClientFor("truong-a.thuvientn.vn", TestClaims.Staff(11)).GetFromJsonAsync<Echo>(U("/api/admin/media/files/abc")))!;
        Assert.Equal("/api/files/abc", admin.Path);
        var system = (await _gateway.ClientFor("quantri.thuvientn.vn", TestClaims.SuperAdmin).GetFromJsonAsync<Echo>(U("/api/system/media/files/uploads")))!;
        Assert.Equal("/api/system/files/uploads", system.Path);
        var manifest = (await _gateway.ClientFor("truong-a.thuvientn.vn").GetFromJsonAsync<Echo>(U("/api/opac/tenant/manifest.json")))!;
        Assert.Equal("/api/manifest.json", manifest.Path);
        Assert.Equal("11", manifest.Headers["X-Tenant-Id"]);
    }

    private HttpClient ViaProxy(string remoteIp, string? forwardedFor = null, string? forwardedProto = null)
    {
        var client = _gateway.ClientFor("truong-a.thuvientn.vn");
        client.DefaultRequestHeaders.Add(GatewayFactory.RemoteIpHeader, remoteIp);
        if (forwardedFor is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        if (forwardedProto is not null) client.DefaultRequestHeaders.Add("X-Forwarded-Proto", forwardedProto);
        return client;
    }

    [Fact]
    public async Task Forwarded_headers_from_trusted_proxy_are_honoured()
    {
        var echo = (await ViaProxy("10.0.0.5", "203.0.113.7", "https").GetFromJsonAsync<Echo>(U("/api/opac/search/books")))!;

        Assert.Equal("https", echo.Headers["X-Forwarded-Proto"]); // identity sinh URL https
        Assert.Equal("203.0.113.7", echo.Headers["X-Forwarded-For"]);
    }

    [Fact]
    public async Task Forwarded_headers_from_untrusted_client_are_ignored()
    {
        var echo = (await ViaProxy("198.51.100.9", "203.0.113.7", "https").GetFromJsonAsync<Echo>(U("/api/opac/search/books")))!;

        Assert.Equal("http", echo.Headers["X-Forwarded-Proto"]);
        Assert.Equal("198.51.100.9", echo.Headers["X-Forwarded-For"]);
    }

    [Fact]
    public async Task Rate_limit_behind_proxy_is_per_real_client_ip()
    {
        HttpResponseMessage last = null!;
        for (var i = 0; i < 25; i++) last = await ViaProxy("10.0.0.5", "203.0.113.7").GetAsync(U("/api/opac/search/books"));
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);

        // Cùng đi qua nginx 10.0.0.5 nhưng là người khác — không bị chặn chung.
        Assert.Equal(HttpStatusCode.OK, (await ViaProxy("10.0.0.5", "203.0.113.8").GetAsync(U("/api/opac/search/books"))).StatusCode);
    }

    [Fact]
    public async Task Requests_are_rate_limited_per_host_and_client()
    {
        var client = _gateway.ClientFor("truong-b.thuvientn.vn");
        HttpResponseMessage last = null!;
        for (var i = 0; i < 25; i++) last = await client.GetAsync(U("/api/opac/search/books"));

        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.Equal("TOO_MANY_REQUESTS", await Code(last));

        // Đơn vị khác không bị ảnh hưởng.
        Assert.Equal(HttpStatusCode.OK, (await _gateway.ClientFor("truong-a.thuvientn.vn").GetAsync(U("/api/opac/search/books"))).StatusCode);
    }

    [Fact]
    public async Task Opac_routes_have_a_stricter_per_ip_limit_with_retry_after()
    {
        HttpResponseMessage last = null!;
        for (var i = 0; i < 6; i++) last = await ViaProxy("10.0.0.5", "203.0.113.20").GetAsync(U("/api/opac/catalog/bibs/x/marc"));
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.True(int.Parse(last.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture) > 0);

        // Mọi route OPAC (appsettings: search-opac cũng đặt policy "opac") dùng chung hạn mức của IP; IP khác vẫn tra được.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await ViaProxy("10.0.0.5", "203.0.113.20").GetAsync(U("/api/opac/search/books"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ViaProxy("10.0.0.5", "203.0.113.21").GetAsync(U("/api/opac/catalog/bibs/x/marc"))).StatusCode);
    }
}
