using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Patron.Application;

namespace Elib.Patron.Tests.Api;

public sealed class DashboardApiTests(PatronApiFactory factory) : IClassFixture<PatronApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static long _nextTenantId = 61000;
    private static long _nextUserId = 71000;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

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

    [Fact]
    public async Task Dashboard_counts_active_locked_expired_readers_and_types_of_the_tenant_only()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var student = await Read<NamedItemDto>(await staff.PostAsJsonAsync(U("/api/reader-types/Add"), new NameRequest("Học sinh"), Json));
        var teacher = await Read<NamedItemDto>(await staff.PostAsJsonAsync(U("/api/reader-types/Add"), new NameRequest("Giáo viên"), Json));
        async Task<ReaderDto> Add(string card, long type, DateOnly? expire = null) =>
            await Read<ReaderDto>(await staff.PostAsJsonAsync(U("/api/readers/Add"), new ReaderRequest(card, "Nguyễn", "An", ReaderTypeId: type, ExpireDate: expire), Json));
        await Add("DB-01", student.Id);
        await Add("DB-02", student.Id, new DateOnly(2020, 1, 1));
        var lockedReader = await Add("DB-03", teacher.Id);
        await Read<ReaderDto>(await staff.PostAsJsonAsync(U($"/api/readers/Lock/{lockedReader.PublicId}"), new LockReaderRequest("Mất thẻ"), Json));

        var d = await Read<PatronDashboard>(await staff.GetAsync(U("/api/dashboard")));
        Assert.Equal((3, 1, 1, 1), (d.Readers.Total, d.Readers.Locked, d.Readers.Expired, d.Readers.Active));
        Assert.Equal(12, d.Added12.Points.Count);
        Assert.Equal(3, d.Added12.Total);
        Assert.Equal(3, d.Added12.Points[^1].Value);
        Assert.Equal([("Học sinh", 2), ("Giáo viên", 1)], d.ByType.Select(t => (t.Name, t.Count)));

        var other = await Read<PatronDashboard>(await Staff(Interlocked.Increment(ref _nextTenantId)).GetAsync(U("/api/dashboard")));
        Assert.Equal(0, other.Readers.Total);
        Assert.Empty(other.ByType);
    }

    [Fact]
    public async Task Dashboard_requires_the_readers_view_permission()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "CLASSES:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "READERS:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
