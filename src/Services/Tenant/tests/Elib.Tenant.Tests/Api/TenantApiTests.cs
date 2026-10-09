using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Application;
using Elib.Tenant.Domain;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Tenant.Tests.Api;

public sealed class TenantApiTests : IClassFixture<TenantApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly TenantApiFactory _factory;

    public TenantApiTests(TenantApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient Admin() => _factory.CreateClient().WithClaims(TestClaims.SuperAdmin);

    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static CreateTenantRequest NewRequest(string code, params string[] modules) =>
        new(code, "Trường " + code, code.ToLowerInvariant(), null, null, modules.Select(m => new LicenseInput(m)).ToList());

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private async Task<TenantDto> CreateAsync(params string[] modules)
    {
        var response = await Admin().PostAsJsonAsync("/api/tenants", NewRequest(NewCode(), modules), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<TenantDto>(response);
    }

    private async Task<TenantDto> WaitForStatusAsync(Guid publicId, TenantState status)
    {
        TenantDto dto = null!;
        for (var i = 0; i < 50; i++)
        {
            dto = await Read<TenantDto>(await Admin().GetAsync(new Uri($"/api/tenants/{publicId}", UriKind.Relative)));
            if (dto.Status == status) return dto;
            await Task.Delay(100);
        }
        Assert.Fail($"Đơn vị không chuyển sang {status}; hiện tại {dto.Status}.");
        return dto;
    }

    private async Task<TenantDto> CreateActiveAsync(params string[] modules)
    {
        var tenant = await CreateAsync(modules);
        await _factory.Services.GetRequiredService<IBus>().Publish(new TenantSeeded { TenantId = tenant.Id, Service = "identity" });
        return await WaitForStatusAsync(tenant.PublicId, TenantState.Active);
    }

    [Fact]
    public async Task Create_starts_provisioning_and_publishes_event()
    {
        var tenant = await CreateAsync("CATALOG", "HOLDINGS", "CIRCULATION");

        Assert.Equal(TenantState.Provisioning, tenant.Status);
        var step = Assert.Single(tenant.ProvisioningSteps);
        Assert.Equal("identity", step.Service); // chỉ service đang triển khai được chờ
        Assert.Equal(3, tenant.Licenses.Count);

        var evt = Assert.Single(_factory.PublishedOf<TenantProvisioned>(), e => e.TenantId == tenant.Id);
        Assert.Equal(tenant.Subdomain, evt.Subdomain);
        Assert.Equal(["CATALOG", "CIRCULATION", "HOLDINGS"], evt.Modules.Select(m => m.ModuleCode));
    }

    [Fact]
    public async Task Seeded_by_all_expected_services_activates_and_publishes_update()
    {
        var tenant = await CreateActiveAsync("SEARCH");

        Assert.Equal(StepStatus.Succeeded, Assert.Single(tenant.ProvisioningSteps).Status);
        Assert.Contains(_factory.PublishedOf<TenantUpdated>(), e => e.TenantId == tenant.Id && e.Status == TenantStatus.Active);
    }

    [Fact]
    public async Task Only_system_context_can_manage_tenants()
    {
        var staff = await _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId: 1)).GetAsync(new Uri("/api/tenants", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);
        Assert.Equal("SYSTEM_ONLY", await ErrorCode(staff));

        var anonymous = await _factory.CreateClient().GetAsync(new Uri("/api/tenants", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Service token không phải quản trị viên: lộ secret một service không được thành quyền quản trị nền tảng.
        var service = await _factory.CreateClient().WithClaims(TestClaims.Service).PostAsJsonAsync("/api/tenants", NewRequest(NewCode()), Json);
        Assert.Equal(HttpStatusCode.Forbidden, service.StatusCode);
        Assert.Equal("SYSTEM_ONLY", await ErrorCode(service));
    }

    [Fact]
    public async Task Internal_endpoints_only_accept_service_tokens()
    {
        var path = new Uri("/internal/tenants/by-host/x.thuvientn.vn", UriKind.Relative);

        var admin = await Admin().GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal("SERVICE_ONLY", await ErrorCode(admin));

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().WithClaims(TestClaims.Service).GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_code_or_subdomain_is_conflict()
    {
        var code = NewCode();
        Assert.Equal(HttpStatusCode.Created, (await Admin().PostAsJsonAsync("/api/tenants", NewRequest(code), Json)).StatusCode);

        var duplicate = await Admin().PostAsJsonAsync("/api/tenants", NewRequest(code), Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("TENANT_CODE_EXISTS", await ErrorCode(duplicate));

        var sameHost = NewRequest(NewCode()) with { Subdomain = code.ToLowerInvariant() };
        var conflict = await Admin().PostAsJsonAsync("/api/tenants", sameHost, Json);
        Assert.Equal("TENANT_SUBDOMAIN_EXISTS", await ErrorCode(conflict));
    }

    [Fact]
    public async Task Missing_module_dependency_is_rejected()
    {
        var response = await Admin().PostAsJsonAsync("/api/tenants", NewRequest(NewCode(), "AI"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MODULE_DEPENDENCY_MISSING", await ErrorCode(response));
    }

    [Fact]
    public async Task Features_for_tenant_staff_and_for_anonymous_opac_via_gateway()
    {
        var tenant = await CreateActiveAsync("SEARCH", "AI");

        var staff = await Read<FeaturesDto>(await _factory.CreateClient().WithClaims(TestClaims.Staff(tenant.Id)).GetAsync(new Uri("/api/features", UriKind.Relative)));
        Assert.Equal(["AI", "SEARCH"], staff.Modules);

        var opac = _factory.CreateClient();
        opac.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        opac.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(TenantApiFactory.GatewayKey, tenant.Id));
        var anonymous = await Read<FeaturesDto>(await opac.GetAsync(new Uri("/api/features", UriKind.Relative)));
        Assert.Equal(tenant.Code, anonymous.Code);
        Assert.Equal(["AI", "SEARCH"], anonymous.Modules);
    }

    [Fact]
    public async Task Branding_accepts_only_own_media_logo_and_feeds_features_and_manifest()
    {
        var tenant = await CreateActiveAsync("SEARCH");
        var other = await CreateActiveAsync("SEARCH");
        var logo = $"/s3/media-public/{tenant.Id}/tenant-logo/2026/10/{Guid.NewGuid():N}.png";
        Uri Branding(Guid id) => new($"/api/tenants/{id}/branding", UriKind.Relative);

        foreach (var bad in new[]
                 {
                     "https://tracker.example/logo.png",
                     $"/s3/media-public/{other.Id}/tenant-logo/2026/10/{Guid.NewGuid():N}.png", // logo của đơn vị khác
                     $"/s3/media-public/{tenant.Id}/tenant-logo/../../{other.Id}/x.png",
                     $"/s3/media-public/{tenant.Id}/attachment/2026/10/{Guid.NewGuid():N}.png",
                 })
        {
            var rejected = await Admin().PutAsJsonAsync(Branding(tenant.PublicId), new SetBrandingRequest("TH Demo", bad), Json);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal("TENANT_LOGO_INVALID", await ErrorCode(rejected));
        }

        var saved = await Read<TenantDto>(await Admin().PutAsJsonAsync(Branding(tenant.PublicId), new SetBrandingRequest(" TH Demo ", logo), Json));
        Assert.Equal(logo, saved.LogoUrl);
        Assert.Equal("TH Demo", saved.LogoText);
        Assert.Contains(_factory.PublishedOf<SystemAuditRecorded>(), a => a.Action == "TENANT_BRANDING" && a.TenantId == tenant.Id);

        var staff = _factory.CreateClient().WithClaims(TestClaims.Staff(tenant.Id));
        Assert.Equal(logo, (await Read<FeaturesDto>(await staff.GetAsync(new Uri("/api/features", UriKind.Relative)))).LogoUrl);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync(Branding(tenant.PublicId), new SetBrandingRequest(null, null), Json)).StatusCode);

        var opac = _factory.CreateClient();
        opac.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        opac.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(TenantApiFactory.GatewayKey, tenant.Id));
        var response = await opac.GetAsync(new Uri("/api/manifest.json", UriKind.Relative));
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(tenant.Name, manifest.GetProperty("name").GetString());
        Assert.Equal("TH Demo", manifest.GetProperty("short_name").GetString());
        Assert.Equal(logo, manifest.GetProperty("icons")[0].GetProperty("src").GetString());

        // Bỏ logo → manifest không có icon.
        await Read<TenantDto>(await Admin().PutAsJsonAsync(Branding(tenant.PublicId), new SetBrandingRequest(null, null), Json));
        manifest = JsonDocument.Parse(await opac.GetStringAsync(new Uri("/api/manifest.json", UriKind.Relative))).RootElement;
        Assert.Equal(0, manifest.GetProperty("icons").GetArrayLength());
    }

    [Fact]
    public async Task Forged_gateway_header_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "1");
        client.DefaultRequestHeaders.Add("X-Gw-Signature", "forged");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/features", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Suspended_tenant_has_no_features()
    {
        var tenant = await CreateActiveAsync("SEARCH");
        Assert.Equal(HttpStatusCode.OK, (await Admin().PostAsJsonAsync($"/api/tenants/{tenant.PublicId}/suspend", new { reason = "hết hạn hợp đồng" }, Json)).StatusCode);

        var features = await Read<FeaturesDto>(await _factory.CreateClient().WithClaims(TestClaims.Staff(tenant.Id)).GetAsync(new Uri("/api/features", UriKind.Relative)));
        Assert.Equal(TenantState.Suspended, features.Status);
        Assert.Empty(features.Modules);
        Assert.Contains(_factory.PublishedOf<TenantSuspended>(), e => e.TenantId == tenant.Id && e.Reason == "hết hạn hợp đồng");
        Assert.Contains(_factory.PublishedOf<SystemAuditRecorded>(), e => e.TenantId == tenant.Id && e.Action == "TENANT_SUSPEND"
            && e.Service == "tenant" && e.Summary!.EndsWith("Lý do: hết hạn hợp đồng", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Changing_licenses_publishes_full_set_with_newer_version_and_updates_host_lookup()
    {
        var tenant = await CreateActiveAsync("SEARCH");

        var response = await Admin().PutAsJsonAsync($"/api/tenants/{tenant.PublicId}/licenses",
            new SetLicensesRequest([new LicenseInput("SEARCH"), new LicenseInput("AI"), new LicenseInput("PORTAL", LicenseStatus.Trial)]), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var evt = Assert.Single(_factory.PublishedOf<ModuleLicenseChanged>(), e => e.TenantId == tenant.Id);
        Assert.True(evt.SourceVersion > tenant.Version);
        Assert.Equal(["AI", "PORTAL", "SEARCH"], evt.Modules.Select(m => m.ModuleCode));

        // Ngừng PORTAL rồi bán lại: không vướng unique index nhờ bản ghi cũ đã xoá mềm.
        await Admin().PutAsJsonAsync($"/api/tenants/{tenant.PublicId}/licenses", new SetLicensesRequest([new LicenseInput("SEARCH")]), Json);
        await Admin().PutAsJsonAsync($"/api/tenants/{tenant.PublicId}/licenses", new SetLicensesRequest([new LicenseInput("SEARCH"), new LicenseInput("PORTAL")]), Json);

        var host = await Read<TenantHostDto>(await _factory.CreateClient().WithClaims(TestClaims.Service)
            .GetAsync(new Uri($"/internal/tenants/by-host/{tenant.Subdomain}.thuvientn.vn", UriKind.Relative)));
        Assert.Equal(tenant.Id, host.TenantId);
        Assert.Equal(["PORTAL", "SEARCH"], host.Modules);
    }

    [Fact]
    public async Task Replica_snapshots_give_new_services_every_tenant_with_licenses()
    {
        var tenant = await CreateActiveAsync("CATALOG", "HOLDINGS");
        var path = new Uri("/internal/tenants/replicas", UriKind.Relative);

        var snapshots = await Read<List<TenantReplicaSnapshot>>(await _factory.CreateClient().WithClaims(TestClaims.Service).GetAsync(path));
        var mine = Assert.Single(snapshots, s => s.Tenant.TenantId == tenant.Id);
        Assert.Equal((tenant.Code, TenantStatus.Active), (mine.Tenant.Code, mine.Tenant.Status));
        Assert.Equal(["CATALOG", "HOLDINGS"], mine.Licenses.Modules.Select(m => m.ModuleCode));
        Assert.Equal(mine.Tenant.SourceVersion, mine.Licenses.SourceVersion);

        // Chỉ service gọi trong cluster — cán bộ/quản trị nền tảng không đọc được.
        Assert.Equal(HttpStatusCode.Forbidden, (await Admin().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Retry_after_failure_restarts_provisioning()
    {
        var tenant = await CreateAsync("SEARCH");
        await _factory.Services.GetRequiredService<IBus>().Publish(new TenantSeeded { TenantId = tenant.Id, Service = "identity", Succeeded = false, Error = "boom" });
        var failed = await WaitForStatusAsync(tenant.PublicId, TenantState.ProvisioningFailed);
        Assert.Contains("boom", failed.ProvisioningError, StringComparison.Ordinal);

        var retried = await Read<TenantDto>(await Admin().PostAsync(new Uri($"/api/tenants/{tenant.PublicId}/provisioning/retry", UriKind.Relative), null));

        Assert.Equal(TenantState.Provisioning, retried.Status);
        Assert.Equal(2, _factory.PublishedOf<TenantProvisioned>().Count(e => e.TenantId == tenant.Id));
    }

    [Fact]
    public async Task Resync_republishes_current_state_for_new_services()
    {
        var tenant = await CreateActiveAsync("SEARCH");
        var current = await Read<TenantDto>(await Admin().GetAsync(new Uri($"/api/tenants/{tenant.PublicId}", UriKind.Relative)));

        var resynced = await Read<TenantDto>(await Admin().PostAsync(new Uri($"/api/tenants/{tenant.PublicId}/resync", UriKind.Relative), null));
        Assert.Equal(current.Version, resynced.Version); // không đổi dữ liệu

        var updated = _factory.PublishedOf<TenantUpdated>().Last(e => e.TenantId == tenant.Id);
        Assert.Equal((TenantStatus.Active, current.Version), (updated.Status, updated.SourceVersion));
        var licenses = _factory.PublishedOf<ModuleLicenseChanged>().Last(e => e.TenantId == tenant.Id);
        Assert.Equal(["SEARCH"], licenses.Modules.Select(m => m.ModuleCode));

        var staff = _factory.CreateClient().WithClaims(TestClaims.Staff(tenant.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync(new Uri($"/api/tenants/{tenant.PublicId}/resync", UriKind.Relative), null)).StatusCode);
    }

    [Fact]
    public async Task Module_catalog_is_seeded()
    {
        var modules = await Read<List<ModuleDto>>(await Admin().GetAsync(new Uri("/api/modules", UriKind.Relative)));

        Assert.Equal(ModuleCatalog.All.Count, modules.Count);
        Assert.Equal(["CATALOG", "HOLDINGS"], modules.Single(m => m.Code == "CIRCULATION").DependsOn);
    }
}
