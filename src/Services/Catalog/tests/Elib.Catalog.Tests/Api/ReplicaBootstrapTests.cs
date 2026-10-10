using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Catalog.Application;
using Elib.Catalog.Infrastructure;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elib.Catalog.Tests.Api;

/// <summary>Catalog triển khai sau khi đã có đơn vị: bản sao trống → tự dựng từ service tenant và seed dữ liệu mặc định.</summary>
public sealed class ReplicaBootstrapTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static TenantReplicaSnapshot Snapshot(long tenantId, TenantStatus status, params string[] modules) => new(
        new TenantUpdated
        {
            TenantId = tenantId, Code = "T" + tenantId, Name = "Trường " + tenantId, Subdomain = "t" + tenantId,
            TimeZone = "Asia/Ho_Chi_Minh", Status = status, SourceVersion = 3,
        },
        new ModuleLicenseChanged
        {
            TenantId = tenantId, SourceVersion = 3,
            Modules = [.. modules.Select(m => new ModuleLicense(m, "Active", null, null))],
        });

    [Fact]
    public async Task Empty_replica_is_rebuilt_from_tenant_service_and_active_tenants_are_seeded_once()
    {
        using var factory = new CatalogApiFactory();
        await factory.InitializeAsync();

        var tenantApi = new StubTenantApi(JsonSerializer.Serialize(
            new[] { Snapshot(9001, TenantStatus.Active, "CATALOG", "HOLDINGS"), Snapshot(9002, TenantStatus.Suspended, "CATALOG") }, Json));
        var bootstrapper = new TenantReplicaBootstrapper<CatalogDbContext>(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), tenantApi, TimeProvider.System,
            NullLogger<TenantReplicaBootstrapper<CatalogDbContext>>.Instance);

        Assert.Equal(2, await bootstrapper.RunOnceAsync(CancellationToken.None));
        Assert.Equal("/internal/tenants/replicas", tenantApi.LastPath);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var replicas = await db.Set<TenantReplicaRecord>().Include(t => t.Modules).OrderBy(t => t.TenantId).ToListAsync();
            Assert.Equal([9001L, 9002L], replicas.Select(r => r.TenantId));
            Assert.Equal(("T9001", "Active", 3L, 3L), (replicas[0].Code, replicas[0].Status, replicas[0].SourceVersion, replicas[0].LicenseVersion));
            Assert.Equal(["CATALOG", "HOLDINGS"], replicas[0].Modules.Select(m => m.ModuleCode).Order());

            // Giấy phép đọc từ bản sao vừa dựng (nguồn license thật của service, không phải bản giả của test).
            var licenses = new ReplicaModuleLicenseSource<CatalogDbContext>(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>(), TimeProvider.System);
            Assert.True(await licenses.IsLicensedAsync(9001, "CATALOG", CancellationToken.None));
            Assert.False(await licenses.IsLicensedAsync(9002, "CATALOG", CancellationToken.None)); // đơn vị tạm ngưng → không module nào
        }

        // Đơn vị đang hoạt động được seed loại biểu ghi + biểu mẫu mặc định; đơn vị tạm ngưng thì không.
        async Task<int> TypeCount(long tenantId)
        {
            factory.Licenses.Licensed.Add((tenantId, "CATALOG"));
            factory.Permissions.Grants[tenantId * 10] = ["*"];
            var staff = factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, tenantId * 10));
            var response = await staff.PostAsJsonAsync(new Uri("/api/bib-types/SearchAll", UriKind.Relative), new CrudSearch(), Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<List<BibTypeDto>>(Json))!.Count;
        }
        Assert.Equal(BibTypeResource.Defaults.Count, await TypeCount(9001));
        Assert.Equal(0, await TypeCount(9002));

        // Bản sao đã có → không gọi lại service tenant.
        tenantApi.LastPath = null;
        Assert.Equal(0, await bootstrapper.RunOnceAsync(CancellationToken.None));
        Assert.Null(tenantApi.LastPath);
    }

    private sealed class StubTenantApi(string body) : HttpMessageHandler, IHttpClientFactory
    {
        public string? LastPath { get; set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("http://tenant/") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
