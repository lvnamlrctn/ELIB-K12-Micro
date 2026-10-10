using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;
using Elib.Circulation.Application;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Patron;

namespace Elib.Circulation.Tests.Api;

/// <summary>Dữ liệu và client dùng chung cho các test API của circulation (mỗi test một đơn vị riêng).</summary>
public abstract class CirculationTestBase(CirculationApiFactory factory) : IClassFixture<CirculationApiFactory>, IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 9000;
    private static long _nextUserId = 19000;
    private static long _nextMfn = 900;

    protected CirculationApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static Uri U(string path) => new(path, UriKind.Relative);

    protected static long NextMfn() => Interlocked.Increment(ref _nextMfn);

    protected long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) Factory.Licenses.Licensed.Add((tenantId, "CIRCULATION"));
        return tenantId;
    }

    protected HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        Factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return Factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    protected static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected static async Task<(int Status, string? Code, string? Detail)> Error(HttpResponseMessage response)
    {
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return ((int)response.StatusCode, root.GetProperty("code").GetString(), root.GetProperty("detail").GetString());
    }

    protected static ReaderChanged Reader(long tenantId, string card, long? typeId = null, int status = 2, DateOnly? expire = null, long version = 0,
        string? email = null) => new()
    {
        TenantId = tenantId, ReaderPublicId = Guid.CreateVersion7(), CardNo = card, FullName = "Bạn đọc " + card, ReaderTypeId = typeId,
        ReaderTypeName = "Học sinh", ClassName = "6A1", Status = status, ExpireDate = expire, Version = version, Email = email,
    };

    protected static ItemChanged Item(long tenantId, string barcode, long mfn, string status = "R", long? storeId = 1, long version = 0,
        Guid? bibPublicId = null) => new()
    {
        TenantId = tenantId, ItemPublicId = Guid.CreateVersion7(), Barcode = barcode, BibPublicId = bibPublicId ?? Guid.CreateVersion7(), Mfn = mfn,
        StoreId = storeId, StoreName = "Kho " + storeId, Status = status, Version = version,
    };

    protected static BibChanged Bib(long tenantId, long mfn, string title, Guid? bibPublicId = null) => new()
    {
        TenantId = tenantId, BibPublicId = bibPublicId ?? Guid.CreateVersion7(), Mfn = mfn, Title = title, Author = "Tô Hoài", Status = 2,
    };

    /// <summary>Đưa vào bản sao qua event (như patron/holdings/catalog phát) và chờ consumer xử lý xong.</summary>
    protected async Task Publish(params IntegrationEvent[] events)
    {
        foreach (var e in events) await Factory.PublishAsAsync(e);
        foreach (var e in events) await Factory.WaitConsumedAsync(e);
    }

    protected static async Task<CircPlaceDto> AddPlace(HttpClient staff, string code, params long[] stores) =>
        await Read<CircPlaceDto>(await staff.PostAsJsonAsync(U("/api/circ-places/Add"), new CircPlaceRequest(code, "Quầy " + code, stores), Json));

    protected static Task<HttpResponseMessage> Checkout(HttpClient staff, string card, long place, params string[] barcodes) =>
        staff.PostAsJsonAsync(U("/api/loans/Checkout"), new CheckoutRequest(card, barcodes, place), Json);
}
