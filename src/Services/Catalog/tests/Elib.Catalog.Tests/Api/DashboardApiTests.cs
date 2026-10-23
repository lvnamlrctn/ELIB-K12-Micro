using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;

namespace Elib.Catalog.Tests.Api;

public sealed class DashboardApiTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 62000;
    private static long _nextUserId = 72000;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "CATALOG"));
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

    private static MarcField Data(string tag, string ind1, string ind2, params (string Code, string Value)[] subfields) =>
        new(tag, ind1, ind2, Subfields: [.. subfields.Select(s => new MarcSubfield(s.Code, s.Value))]);

    private static Task<HttpResponseMessage> AddBib(HttpClient staff, string title, bool author) => staff.PostAsJsonAsync(U("/api/bibs/Add"), new BibRequest(null,
    [
        .. author ? [Data("100", "0", " ", ("a", "Nguyễn Du"))] : Array.Empty<MarcField>(),
        Data("245", "1", "0", ("a", title + " /")),
        Data("260", " ", " ", ("a", "H. :"), ("b", "Văn học,"), ("c", "2019")),
    ]), Json);

    [Fact]
    public async Task Dashboard_counts_bibs_new_by_week_and_missing_data_of_the_tenant_only()
    {
        var a = NewTenant();
        var other = NewTenant();
        var staff = Staff(a);
        Assert.True((await AddBib(staff, "Truyện Kiều", true)).IsSuccessStatusCode);
        Assert.True((await AddBib(staff, "Sách không tác giả", false)).IsSuccessStatusCode);
        Assert.True((await AddBib(Staff(other), "Của đơn vị khác", true)).IsSuccessStatusCode);

        var d = await Read<CatalogDashboard>(await staff.GetAsync(U("/api/dashboard")));
        Assert.Equal((2, 2), (d.Bibs, d.Visible));
        Assert.Equal(12, d.NewByWeek.Count);
        Assert.Equal(DayOfWeek.Monday, d.NewByWeek[^1].Date.DayOfWeek);
        Assert.Equal((2, 2), (d.NewTotal, d.NewByWeek[^1].Value));
        Assert.Equal((1, 2, 2), (d.MissingAuthor, d.MissingDdc, d.MissingCover));
    }

    [Fact]
    public async Task Dashboard_requires_the_bib_view_permission()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "BIB_TYPES:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "CATALOG_BIBS:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
