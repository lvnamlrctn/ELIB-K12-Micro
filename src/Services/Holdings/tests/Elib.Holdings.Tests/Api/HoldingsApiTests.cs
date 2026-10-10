using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Platform;
using Elib.Holdings.Application;

namespace Elib.Holdings.Tests.Api;

public sealed class HoldingsApiTests : IClassFixture<HoldingsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 8000;
    private static long _nextUserId = 18000;
    private static long _nextMfn = 500;

    private readonly HoldingsApiFactory _factory;

    public HoldingsApiTests(HoldingsApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) _factory.Licenses.Licensed.Add((tenantId, "HOLDINGS"));
        return tenantId;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private static BibChanged Bib(long tenantId, long mfn, string title, long version = 0, bool deleted = false, Guid? id = null) => new()
    {
        TenantId = tenantId, BibPublicId = id ?? Guid.CreateVersion7(), Mfn = mfn, Title = title, Author = "Tô Hoài", PublishYear = "2020",
        Isbns = ["9786042000011"], Ddc = "895.9223", Status = 2, Deleted = deleted, Version = version,
    };

    /// <summary>Biểu ghi đã sang holdings qua event BibChanged.</summary>
    private async Task<BibChanged> PublishBib(long tenantId, string title)
    {
        var bib = Bib(tenantId, Interlocked.Increment(ref _nextMfn), title);
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib);
        return bib;
    }

    private static Task<HttpResponseMessage> Register(HttpClient staff, long mfn, int quantity, string? prefix = "VV", long? start = null, long? storeId = null) =>
        staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(mfn, quantity, prefix, 6, start, storeId), Json);

    private static async Task<StoreDto> AddStore(HttpClient staff, string code) =>
        await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest(code, "Kho " + code), Json));

    [Fact]
    public async Task New_tenant_gets_default_store_types_and_a_common_store()
    {
        var tenantId = NewTenant();
        await _factory.PublishAsync(new TenantProvisioned
        {
            TenantId = tenantId, Code = "H" + tenantId, Name = "Trường " + tenantId, Subdomain = "h" + tenantId, TimeZone = "Asia/Ho_Chi_Minh", Modules = [],
        });
        await HoldingsApiFactory.WaitForAsync(() => Task.FromResult(_factory.PublishedOf<TenantSeeded>().FirstOrDefault(s => s.TenantId == tenantId)), "TenantSeeded");
        Assert.True(_factory.PublishedOf<TenantSeeded>().Single(s => s.TenantId == tenantId).Succeeded);

        var staff = Staff(tenantId);
        var types = await Read<List<NamedItemDto>>(await staff.PostAsJsonAsync(U("/api/store-types/SearchAll"), new CrudSearch(), Json));
        Assert.Equal(["Kho mở", "Kho đóng"], types.Select(t => t.Name).Order(StringComparer.Ordinal));
        var store = Assert.Single(await Read<List<StoreDto>>(await staff.PostAsJsonAsync(U("/api/stores/SearchAll"), new CrudSearch(), Json)));
        Assert.Equal(("KC", "Kho chung", 0), (store.Code, store.Name, store.ItemCount));
        Assert.Equal(types.Single(t => t.Name == "Kho mở").Id, store.StoreTypeId);
    }

    [Fact]
    public async Task Register_batch_numbers_on_from_highest_number_and_rejects_duplicates()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var store = await AddStore(staff, "KM");
        var bib = await PublishBib(tenantId, "Dế mèn phiêu lưu ký");

        var items = await Read<List<ItemDto>>(await Register(staff, bib.Mfn, 3, "vv", storeId: store.Id));
        Assert.Equal(["VV000001", "VV000002", "VV000003"], items.Select(i => i.Barcode));
        Assert.All(items, i => Assert.Equal(("I", "Dế mèn phiêu lưu ký", "KM", bib.BibPublicId), (i.Status, i.Title, i.StoreCode, i.BibPublicId)));

        // Mỗi bản phát ItemChanged cho circulation/search.
        var events = _factory.PublishedOf<ItemChanged>().Where(e => e.TenantId == tenantId).ToList();
        Assert.Equal(3, events.Count);
        Assert.All(events, e => Assert.Equal((bib.Mfn, "I", "Kho KM", false), (e.Mfn, e.Status, e.StoreName, e.Deleted)));

        var next = await Read<NextBarcode>(await staff.GetAsync(U("/api/items/NextBarcode?prefix=VV&digits=6")));
        Assert.Equal("VV000004", next.Barcode);

        // Trùng mã đã có → cả lô bị từ chối.
        var dupe = await Register(staff, bib.Mfn, 3, "VV", start: 3);
        Assert.Equal(HttpStatusCode.Conflict, dupe.StatusCode);
        Assert.Equal("BARCODE_EXISTS", await ErrorCode(dupe));

        // Nhập tay một mã (không phân biệt hoa/thường) — lô sau đánh tiếp sau số lớn nhất.
        var single = await Read<ItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new ItemRequest(bib.Mfn, "vv000010"), Json));
        Assert.Equal("vv000010", single.Barcode);
        var again = await staff.PostAsJsonAsync(U("/api/items/Add"), new ItemRequest(bib.Mfn, "VV000010"), Json);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("VV000011", (await Read<List<ItemDto>>(await Register(staff, bib.Mfn, 1, "VV")))[0].Barcode);

        var prefixed = await Register(staff, bib.Mfn, 1, "VV1");
        Assert.Equal("PREFIX_INVALID", await ErrorCode(prefixed));

        var byBib = await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/SearchAll"), new ItemSearch { Mfn = bib.Mfn }, Json));
        Assert.Equal(5, byBib.Count);
        Assert.Equal(3, (await Read<StoreDto>(await staff.GetAsync(U($"/api/stores/{store.Id}")))).ItemCount);
    }

    [Fact]
    public async Task Bib_missing_from_replica_is_fetched_from_catalog_and_events_keep_snapshot_current()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        await PublishBib(tenantId, "Khởi động");

        // Biểu ghi có trước khi holdings triển khai: chưa có bản sao → hỏi catalog.
        var old = Bib(tenantId, Interlocked.Increment(ref _nextMfn), "Tắt đèn", version: 4);
        _factory.CatalogBibs.Bibs[(tenantId, old.Mfn)] = old;
        var items = await Read<List<ItemDto>>(await Register(staff, old.Mfn, 2, "TD"));
        Assert.Equal("Tắt đèn", items[0].Title);
        var calls = _factory.CatalogBibs.Calls;
        await Read<List<ItemDto>>(await Register(staff, old.Mfn, 1, "TD"));
        Assert.Equal(calls, _factory.CatalogBibs.Calls); // lần sau dùng bản sao

        var missing = await Register(staff, 999_999, 1, "TD");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Event cũ hơn bị bỏ qua, event mới cập nhật nhan đề.
        await _factory.PublishAsync(Bib(tenantId, old.Mfn, "Bản cũ", version: 2, id: old.BibPublicId));
        await _factory.PublishAsync(Bib(tenantId, old.Mfn, "Tắt đèn (tái bản)", version: 5, id: old.BibPublicId));
        await HoldingsApiFactory.WaitForAsync(async () =>
        {
            var list = await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/SearchAll"), new ItemSearch { Mfn = old.Mfn }, Json));
            return list.All(i => i.Title == "Tắt đèn (tái bản)") ? list : null;
        }, "nhan đề mới");

        // Biểu ghi đã xoá ở catalog → không đăng ký thêm được.
        await _factory.PublishAsync(Bib(tenantId, old.Mfn, "Tắt đèn (tái bản)", version: 6, deleted: true, id: old.BibPublicId));
        await HoldingsApiFactory.WaitForAsync(async () =>
            (await Register(staff, old.Mfn, 1, "TD")).StatusCode == HttpStatusCode.NotFound ? "ok" : null, "biểu ghi đã xoá");
    }

    [Fact]
    public async Task Shelving_moves_unshelved_items_to_available_with_its_own_permission()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Số đỏ");
        var shelf = await AddStore(staff, "KD");
        await Read<List<ItemDto>>(await Register(staff, bib.Mfn, 3, "SD"));

        var shelver = Staff(tenantId, "MAP_SHELVING:view", "MAP_SHELVING:edit");
        var unshelved = await Read<CrudPage<ItemDto>>(await shelver.PostAsJsonAsync(U("/api/items/Lookup"), new ItemSearch { ItemStatus = "i", Keyword = "sd" }, Json));
        Assert.Equal(3, unshelved.TotalCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await Register(shelver, bib.Mfn, 1, "SD")).StatusCode);

        var result = await Read<ShelveResult>(await shelver.PostAsJsonAsync(U("/api/items/Shelve"),
            new ShelveRequest(Barcodes: ["sd000001", "SD000002", "KHONGCO"], StoreId: shelf.Id), Json));
        Assert.Equal(2, result.Shelved);
        Assert.Equal(["KHONGCO"], result.NotFound);

        var again = await Read<ShelveResult>(await shelver.PostAsJsonAsync(U("/api/items/Shelve"), new ShelveRequest(Barcodes: ["SD000001", "SD000003"]), Json));
        Assert.Equal(1, again.Shelved);
        Assert.Equal(["SD000001"], again.Skipped);

        var all = await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/SearchAll"), new ItemSearch { Mfn = bib.Mfn }, Json));
        Assert.All(all, i => Assert.Equal("R", i.Status));
        Assert.Equal([shelf.Id, shelf.Id, null], all.Select(i => i.StoreId));
        var last = _factory.PublishedOf<ItemChanged>().Where(e => e.Barcode == "SD000001").MaxBy(e => e.Version)!;
        Assert.Equal(("R", 1L, "Kho KD"), (last.Status, last.Version, last.StoreName));
    }

    [Fact]
    public async Task Loan_events_show_on_loan_and_a_lost_closure_marks_the_item_lost()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Nhật ký trong tù");
        var items = await Read<List<ItemDto>>(await Register(staff, bib.Mfn, 2, "NK"));
        await Read<ShelveResult>(await staff.PostAsJsonAsync(U("/api/items/Shelve"), new ShelveRequest(Ids: [.. items.Select(i => i.PublicId)]), Json));
        var now = DateTimeOffset.UtcNow;
        LoanChanged Loan(ItemDto item, Guid id, long version, DateTimeOffset loanedAt, DateTimeOffset? returned = null, string? closed = null) => new()
        {
            TenantId = tenantId, LoanPublicId = id, ReaderPublicId = Guid.CreateVersion7(), CardNo = "HS01", ItemPublicId = item.PublicId,
            Barcode = item.Barcode, Mfn = bib.Mfn, LoanedAt = loanedAt, DueAt = loanedAt.AddDays(14), ReturnedAt = returned,
            ClosedItemStatus = closed, Version = version,
        };

        // Bản 1: mượn → đang mượn; event trả tới trước event gia hạn cũ hơn → giữ trạng thái đã trả.
        var first = Guid.CreateVersion7();
        foreach (var e in new[] { Loan(items[0], first, 0, now), Loan(items[0], first, 2, now, returned: now.AddDays(3)), Loan(items[0], first, 1, now) })
        {
            await _factory.PublishAsync(e);
            await _factory.WaitConsumedAsync(e);
        }
        // Bản 2: mượn rồi đóng vì mất tài liệu → trạng thái "Mất", phát ItemChanged.
        var second = Guid.CreateVersion7();
        var open = Loan(items[1], second, 0, now);
        await _factory.PublishAsync(open);
        await _factory.WaitConsumedAsync(open);
        var onLoan = await Read<CrudPage<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), new ItemSearch { Mfn = bib.Mfn, ItemStatus = "b" }, Json));
        Assert.Equal(("NK000002", "HS01"), (Assert.Single(onLoan.Items).Barcode, onLoan.Items[0].LoanCardNo));

        var lost = Loan(items[1], second, 1, now, returned: now.AddDays(1), closed: "L");
        await _factory.PublishAsync(lost);
        await _factory.WaitConsumedAsync(lost);

        var all = await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/SearchAll"), new ItemSearch { Mfn = bib.Mfn }, Json));
        Assert.Equal([("R", false), ("L", false)], all.Select(i => (i.Status, i.OnLoan)));
        var changed = _factory.PublishedOf<ItemChanged>().Where(e => e.ItemPublicId == items[1].PublicId).MaxBy(e => e.Version)!;
        Assert.Equal("L", changed.Status);
    }

    [Fact]
    public async Task Store_rules_delete_and_tenant_isolation()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Lão Hạc");
        var store = await AddStore(staff, "kt");
        Assert.Equal("KT", store.Code);
        var dupe = await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("KT", "Khác"), Json);
        Assert.Equal(HttpStatusCode.Conflict, dupe.StatusCode);

        var item = (await Read<List<ItemDto>>(await Register(staff, bib.Mfn, 1, "LH", storeId: store.Id)))[0];
        var blocked = await staff.DeleteAsync(U($"/api/stores/Delete/{store.PublicId}"));
        Assert.Equal("STORE_NOT_EMPTY", await ErrorCode(blocked));

        // Đơn vị khác không thấy bản sách.
        var other = Staff(NewTenant());
        Assert.Equal(0, (await Read<CrudPage<ItemDto>>(await other.PostAsJsonAsync(U("/api/items/Search"), new ItemSearch { Keyword = "LH" }, Json))).TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(U($"/api/items/Delete/{item.PublicId}"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/items/Delete/{item.PublicId}"))).StatusCode);
        var deleted = _factory.PublishedOf<ItemChanged>().Single(e => e.ItemPublicId == item.PublicId && e.Deleted);
        Assert.Equal(1, deleted.Version);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/stores/Delete/{store.PublicId}"))).StatusCode);
        // Nội bộ (circulation): trạng thái theo mã, chỉ service token.
        var service = _factory.CreateClient().WithClaims(TestClaims.Service);
        Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync(U($"/internal/tenants/{tenantId}/items/by-barcode/lh000001"))).StatusCode); // đã xoá
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenantId}/items/by-barcode/LH000001"))).StatusCode);

        // Mã của bản đã xoá dùng lại được.
        var reused = await Read<ItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new ItemRequest(bib.Mfn, "LH000001"), Json));
        Assert.Equal("LH000001", reused.Barcode);
        var state = await Read<ItemChanged>(await service.GetAsync(U($"/internal/tenants/{tenantId}/items/by-barcode/lh000001")));
        Assert.Equal((reused.PublicId, bib.Mfn, "I", false), (state.ItemPublicId, state.Mfn, state.Status, state.Deleted));
    }

    [Fact]
    public async Task Unlicensed_tenant_is_blocked_and_its_bib_events_are_ignored()
    {
        var tenantId = NewTenant(licensed: false);
        var staff = Staff(tenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync(U("/api/stores/Search"), new CrudSearch(), Json)).StatusCode);

        var bib = Bib(tenantId, Interlocked.Increment(ref _nextMfn), "Không có kho");
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib); // consumer nhận rồi bỏ qua (chưa có license)
        _factory.Licenses.Licensed.Add((tenantId, "HOLDINGS"));
        var calls = _factory.CatalogBibs.Calls;
        Assert.Equal(HttpStatusCode.NotFound, (await Register(staff, bib.Mfn, 1, "KK")).StatusCode);
        Assert.Equal(calls + 1, _factory.CatalogBibs.Calls); // bản sao không có (event bị bỏ qua) → đã hỏi catalog
    }
}
