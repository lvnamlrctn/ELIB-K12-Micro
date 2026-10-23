using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Holdings.Application;

namespace Elib.Holdings.Tests.Api;

public sealed class DashboardApiTests(HoldingsApiFactory factory) : IClassFixture<HoldingsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 61000;
    private static long _nextUserId = 71000;
    private static long _nextMfn = 90000;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "HOLDINGS"));
        return tenantId;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<long> PublishBib(long tenantId, string title, string? type)
    {
        var bib = new BibChanged
        {
            TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Author = "Tô Hoài",
            Status = 2, BibTypeId = type is null ? null : 1, BibTypeName = type,
        };
        await factory.PublishAsync(bib);
        await factory.WaitConsumedAsync(bib);
        return bib.Mfn;
    }

    private static Task<HttpResponseMessage> Register(HttpClient staff, long mfn, int quantity, string prefix, long? storeId = null) =>
        staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(mfn, quantity, prefix, 6, null, storeId), Json);

    [Fact]
    public async Task Dashboard_counts_titles_copies_distribution_additions_and_store_fill_of_the_tenant_only()
    {
        var a = NewTenant();
        var other = NewTenant();
        var staff = Staff(a);
        var store = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("DB1", "Kho chính", null, null, 10), Json));
        var (textbook, loose) = (await PublishBib(a, "Toán 6", "Sách giáo khoa"), await PublishBib(a, "Truyện", null));
        Assert.True((await Register(staff, textbook, 3, "DA", store.Id)).IsSuccessStatusCode);
        Assert.True((await Register(staff, loose, 1, "DB", store.Id)).IsSuccessStatusCode);
        var otherStaff = Staff(other);
        Assert.True((await Register(otherStaff, await PublishBib(other, "Khác", "Tạp chí"), 5, "DC")).IsSuccessStatusCode);

        var d = await Read<HoldingsDashboard>(await staff.GetAsync(U("/api/dashboard")));
        Assert.Equal((2, 4, 4, 0, 0), (d.Titles, d.Copies, d.Unshelved, d.OnLoan, d.Lost));
        Assert.Equal(2, d.DistributionTotal);
        Assert.Equal([("Sách giáo khoa", 1, 50), ("", 1, 50)], d.Distribution.OrderByDescending(x => x.Name).Select(x => (x.Name, x.Count, x.Percent)));
        Assert.Equal(12, d.PrintAdded.Points.Count);
        Assert.Equal((4, 4), (d.PrintAdded.Total, d.PrintAdded.Points[^1].Value));
        var fill = Assert.Single(d.Stores);
        Assert.Equal(("Kho chính", 4, 10, 40), (fill.Name, fill.Count, fill.Capacity, fill.Percent));
    }

    [Fact]
    public async Task Dashboard_requires_the_store_book_report_permission()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "STORES:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "STORE_BOOK_REPORT:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
