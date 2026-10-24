using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.Acquisition.Application;
using Elib.BuildingBlocks.Testing;

namespace Elib.Acquisition.Tests.Api;

/// <summary>Danh sách nhà cung cấp nội bộ cho serials (service token, không đòi license ACQUISITION).</summary>
public sealed class InternalSuppliersApiTests : IClassFixture<AcquisitionApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 12700;
    private static long _nextUserId = 22700;

    private readonly AcquisitionApiFactory _factory;

    public InternalSuppliersApiTests(AcquisitionApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task Suppliers_are_listed_by_name_per_tenant_for_service_callers_only()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var other = Interlocked.Increment(ref _nextTenantId);
        _factory.Licenses.Licensed.Add((tenantId, "ACQUISITION"));
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = ["*"];
        var staff = _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
        foreach (var name in new[] { "Vinabook", "Fahasa", "Đã xoá" })
        {
            var added = await staff.PostAsJsonAsync(U("/api/suppliers/Add"), new { name }, Json);
            Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync());
            if (name == "Đã xoá")
            {
                var id = (await added.Content.ReadFromJsonAsync<SupplierDto>(Json))!.PublicId;
                Assert.True((await staff.DeleteAsync(U($"/api/suppliers/Delete/{id}"))).IsSuccessStatusCode);
            }
        }

        var service = _factory.CreateClient().WithClaims(TestClaims.Service);
        var list = (await service.GetFromJsonAsync<List<SupplierOption>>(U($"/internal/tenants/{tenantId}/suppliers"), Json))!;
        Assert.Equal(["Fahasa", "Vinabook"], list.Select(s => s.Name));
        Assert.All(list, s => Assert.NotEqual(Guid.Empty, s.PublicId));

        // Đơn vị chưa dùng bổ sung (không license, không dữ liệu): rỗng chứ không lỗi.
        Assert.Empty((await service.GetFromJsonAsync<List<SupplierOption>>(U($"/internal/tenants/{other}/suppliers"), Json))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenantId}/suppliers"))).StatusCode);
    }
}
