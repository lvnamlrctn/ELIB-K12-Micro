using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Holdings;
using Elib.Holdings.Application;

namespace Elib.Holdings.Tests.Api;

/// <summary>ĐKCB của tập đóng báo/tạp chí (serials gọi /internal): đăng ký idempotent, gỡ theo quy tắc xoá khỏi phiếu nhận.</summary>
public sealed class SerialItemsApiTests : IClassFixture<HoldingsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 9700;
    private static long _nextUserId = 19700;
    private static long _nextMfn = 7700;

    private readonly HoldingsApiFactory _factory;

    public SerialItemsApiTests(HoldingsApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        _factory.Licenses.Licensed.Add((tenantId, "HOLDINGS"));
        return tenantId;
    }

    private HttpClient Staff(long tenantId)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = ["*"];
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private HttpClient Service() => _factory.CreateClient().WithClaims(TestClaims.Service);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private async Task<BibChanged> PublishBib(long tenantId, string title)
    {
        var bib = new BibChanged
        {
            TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Isbns = [], Status = 2,
        };
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib);
        return bib;
    }

    private Task<HttpResponseMessage> Register(long tenantId, Guid binding, long mfn, string accession, long? storeId = null) =>
        Service().PostAsJsonAsync(U($"/internal/tenants/{tenantId}/serial-items/Register"), new SerialItemRegisterRequest(binding, mfn, accession, storeId), Json);

    private Task<HttpResponseMessage> Remove(long tenantId, Guid binding) => Service().DeleteAsync(U($"/internal/tenants/{tenantId}/serial-items/{binding}"));

    [Fact]
    public async Task Register_creates_one_unshelved_item_and_is_idempotent_per_binding()
    {
        var tenantId = NewTenant();
        var bib = await PublishBib(tenantId, "Tạp chí Văn nghệ");
        var binding = Guid.CreateVersion7();

        var first = await Read<SerialItemDto>(await Register(tenantId, binding, bib.Mfn, "TC-2026-01"));
        Assert.Equal(("TC-2026-01", bib.Mfn, "I"), (first.Barcode, first.Mfn, first.Status));
        Assert.Single(_factory.PublishedOf<ItemChanged>(), e => e.ItemPublicId == first.PublicId);

        var again = await Read<SerialItemDto>(await Register(tenantId, binding, bib.Mfn, "TC-2026-01"));
        Assert.Equal(first.PublicId, again.PublicId); // gọi lại không tạo thêm
        Assert.Single(_factory.PublishedOf<ItemChanged>(), e => e.ItemPublicId == first.PublicId);
    }

    [Fact]
    public async Task Register_rejects_duplicate_accession_number_and_unknown_bib_and_non_service_callers()
    {
        var tenantId = NewTenant();
        var bib = await PublishBib(tenantId, "Báo Nhân dân");
        await Read<SerialItemDto>(await Register(tenantId, Guid.CreateVersion7(), bib.Mfn, "BND-01"));

        var duplicate = await Register(tenantId, Guid.CreateVersion7(), bib.Mfn, "BND-01");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Register(tenantId, Guid.CreateVersion7(), 999_999_999, "BND-02")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Register(tenantId, Guid.CreateVersion7(), bib.Mfn, " ")).StatusCode);

        var staff = Staff(tenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync(U($"/internal/tenants/{tenantId}/serial-items/Register"),
            new SerialItemRegisterRequest(Guid.CreateVersion7(), bib.Mfn, "BND-03"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync(U($"/internal/tenants/{tenantId}/serial-items/{Guid.CreateVersion7()}"))).StatusCode);
    }

    [Fact]
    public async Task Remove_is_idempotent_blocked_once_shelved_and_tenants_do_not_see_each_other()
    {
        var tenantId = NewTenant();
        var other = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Tạp chí Cộng sản");
        var otherBib = await PublishBib(other, "Tạp chí Cộng sản");
        var kept = Guid.CreateVersion7();
        var removed = Guid.CreateVersion7();
        var keptItem = await Read<SerialItemDto>(await Register(tenantId, kept, bib.Mfn, "CS-01"));
        var removedItem = await Read<SerialItemDto>(await Register(tenantId, removed, bib.Mfn, "CS-02"));
        var theirs = await Read<SerialItemDto>(await Register(other, removed, otherBib.Mfn, "CS-02")); // cùng mã tập đóng, đơn vị khác
        Assert.NotEqual(removedItem.PublicId, theirs.PublicId);

        // Xếp giá rồi thì không gỡ được.
        await Read<ShelveResult>(await staff.PostAsJsonAsync(U("/api/items/Shelve"), new ShelveRequest(Ids: [keptItem.PublicId]), Json));
        Assert.Equal("ITEM_IN_USE", await ErrorCode(await Remove(tenantId, kept)));

        Assert.Equal(HttpStatusCode.NoContent, (await Remove(tenantId, removed)).StatusCode);
        Assert.Contains(_factory.PublishedOf<ItemChanged>(), e => e.ItemPublicId == removedItem.PublicId && e.Deleted);
        Assert.DoesNotContain(_factory.PublishedOf<ItemChanged>(), e => e.ItemPublicId == theirs.PublicId && e.Deleted); // đơn vị khác còn nguyên
        Assert.Equal(HttpStatusCode.NoContent, (await Remove(tenantId, removed)).StatusCode); // gỡ lần hai: không có gì để gỡ
        Assert.Equal(HttpStatusCode.NoContent, (await Remove(tenantId, Guid.CreateVersion7())).StatusCode);

        // Gỡ xong có thể đóng lại tập với cùng số ĐKCB.
        Assert.Equal(removedItem.Barcode, (await Read<SerialItemDto>(await Register(tenantId, Guid.CreateVersion7(), bib.Mfn, "CS-02"))).Barcode);
    }
}
