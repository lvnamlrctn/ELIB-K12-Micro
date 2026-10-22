using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Space;
using Elib.Holdings.Application;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Holdings.Tests.Api;

/// <summary>Bản sao giá/ngăn/tầng từ space (ShelfChanged/FloorChanged), vị trí trên bản sách và lịch sử di chuyển.</summary>
public sealed class ShelfLocationApiTests : IClassFixture<HoldingsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 8800;
    private static long _nextUserId = 18800;
    private static long _nextMfn = 8800;

    private readonly HoldingsApiFactory _factory;

    public ShelfLocationApiTests(HoldingsApiFactory factory) => _factory = factory;

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
        return _factory.CreateClient().WithClaims($"{TestClaims.Staff(tenantId, userId)};name=Thu Kho;preferred_username=thukho");
    }

    private HttpClient StaffWith(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<BibChanged> PublishBib(long tenantId, string title, string ddc = "895.9223")
    {
        var bib = new BibChanged
        {
            TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Author = "Tô Hoài",
            PublishYear = "2020", Isbns = ["9786042000011"], Ddc = ddc, Status = 2,
        };
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib);
        return bib;
    }

    private static ShelfChanged Shelf(long tenantId, Guid shelfId, Guid floorId, string name, DateTimeOffset at, bool deleted = false, params (Guid Id, int Index, string From, string To)[] rows) => new()
    {
        TenantId = tenantId, OccurredAt = at, ShelfPublicId = shelfId, Code = "K1", Name = name, FloorPublicId = floorId, PositionX = 10, PositionY = 20, Width = 12, Height = 8,
        Deleted = deleted, Rows = rows.Select(r => new ShelfRowState { RowPublicId = r.Id, RowIndex = r.Index, DdcStart = r.From, DdcEnd = r.To }).ToList(),
    };

    private async Task<T> InTenant<T>(long tenantId, Func<ICrudDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        return await query(scope.ServiceProvider.GetRequiredService<ICrudDbContext>());
    }

    [Fact]
    public async Task Shelf_and_floor_events_build_the_replicas_replace_rows_and_ignore_older_events()
    {
        var tenantId = NewTenant();
        var (shelfId, floorId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var t0 = DateTimeOffset.UtcNow;

        var floor = new FloorChanged { TenantId = tenantId, OccurredAt = t0, FloorPublicId = floorId, Name = "Tầng 2", FloorNumber = 2, BuildingName = "Toà A", CanvasWidth = 1200, CanvasHeight = 800 };
        var first = Shelf(tenantId, shelfId, floorId, "Kệ 300", t0, rows: [(Guid.CreateVersion7(), 1, "300", "329"), (Guid.CreateVersion7(), 2, "330", "339")]);
        await _factory.PublishAsync(floor);
        await _factory.PublishAsync(first);
        await _factory.WaitConsumedAsync(floor);
        await _factory.WaitConsumedAsync(first);

        var rows = await InTenant(tenantId, async db => await db.Set<ShelfRowReplica>().AsNoTracking().OrderBy(r => r.RowIndex).Select(r => r.DdcStart + "-" + r.DdcEnd).ToListAsync());
        Assert.Equal(["300-329", "330-339"], rows);
        var floorReplica = await InTenant(tenantId, db => db.Set<FloorReplica>().AsNoTracking().SingleAsync());
        Assert.Equal(("Tầng 2", "Toà A", 1200d), (floorReplica.Name, floorReplica.BuildingName, floorReplica.CanvasWidth));

        // Event mới hơn thay toàn bộ ngăn; event cũ hơn (tới lệch thứ tự) bị bỏ qua.
        var newer = Shelf(tenantId, shelfId, floorId, "Kệ 300 mới", t0.AddMinutes(1), rows: [(Guid.CreateVersion7(), 1, "300", "399")]);
        var older = Shelf(tenantId, shelfId, floorId, "Kệ cũ", t0.AddSeconds(-30), rows: [(Guid.CreateVersion7(), 9, "000", "099")]);
        await _factory.PublishAsync(newer);
        await _factory.WaitConsumedAsync(newer);
        await _factory.PublishAsync(older);
        await _factory.WaitConsumedAsync(older);
        var shelf = await InTenant(tenantId, db => db.Set<ShelfReplica>().AsNoTracking().SingleAsync());
        Assert.Equal(("Kệ 300 mới", false), (shelf.Name, shelf.Deleted));
        Assert.Equal(["300-399"], await InTenant(tenantId, async db => await db.Set<ShelfRowReplica>().AsNoTracking().Select(r => r.DdcStart + "-" + r.DdcEnd).ToListAsync()));

        // Giá bị xoá ở space: đánh dấu xoá và bỏ ngăn (bản sách vẫn giữ mã giá).
        var gone = Shelf(tenantId, shelfId, floorId, "Kệ 300 mới", t0.AddMinutes(2), deleted: true);
        await _factory.PublishAsync(gone);
        await _factory.WaitConsumedAsync(gone);
        Assert.True((await InTenant(tenantId, db => db.Set<ShelfReplica>().AsNoTracking().SingleAsync())).Deleted);
        Assert.Empty(await InTenant(tenantId, async db => await db.Set<ShelfRowReplica>().AsNoTracking().ToListAsync()));
    }

    [Theory]
    [InlineData("330", "330", "339", true)]
    [InlineData("339", "330", "339", true)]
    [InlineData("339.5", "330", "339", true)] // thuộc khoảng theo tiền tố
    [InlineData("329.9", "330", "339", false)]
    [InlineData("340", "330", "339", false)]
    [InlineData(" 335.2 ", "330", "339", true)]
    [InlineData(null, "330", "339", false)]
    [InlineData("", "330", "339", false)]
    public void Ddc_ranges_match_by_ordinal_comparison_or_end_prefix(string? ddc, string start, string end, bool expected) =>
        Assert.Equal(expected, ShelfRowReplica.Contains(ddc, start, end));

    [Fact]
    public async Task Shelving_a_store_move_and_a_re_registration_leave_a_movement_trail_with_the_actor()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Số đỏ");
        var a = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("KA", "Kho A"), Json));
        var b = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("KB", "Kho B"), Json));
        var items = await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(bib.Mfn, 1, "MV", 6, null, a.Id), Json));
        var item = items[0];

        await Read<ShelveResult>(await staff.PostAsJsonAsync(U("/api/items/Shelve"), new ShelveRequest(Barcodes: [item.Barcode]), Json));
        var move = await Read<StoreMoveDto>(await staff.PostAsJsonAsync(U("/api/store-moves/Add"), new StoreMoveRequest(a.Id, b.Id), Json));
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/store-moves/AddLines/{move.PublicId}"), new AddLinesRequest(Barcodes: [item.Barcode]), Json));
        await Read<CompleteResult>(await staff.PostAsync(U($"/api/store-moves/Complete/{move.PublicId}"), null));
        await Read<ItemDto>(await staff.PostAsJsonAsync(U("/api/items/ReRegister"), new ReRegisterRequest(item.Barcode, "MV999999"), Json));

        var trail = await Read<List<ItemMovementDto>>(await staff.GetAsync(U($"/api/items/Movements/{item.PublicId}")));
        Assert.Equal(["REREGISTER", "MOVE", "SHELVE"], trail.Select(m => m.Kind)); // mới nhất trước
        var shelve = trail[2];
        Assert.Equal(("I", "R", "Thu Kho (thukho)"), (shelve.FromStatus, shelve.ToStatus, shelve.ActorName));
        var moved = trail[1];
        Assert.Equal(("Kho A", "Kho B"), (moved.FromStore, moved.ToStore));
        Assert.Contains("Phiếu điều chuyển", moved.Reason);
        Assert.Contains("→ MV999999", trail[0].Reason);
    }

    [Fact]
    public async Task A_shelf_whose_floor_event_never_arrived_still_shows_and_editing_the_store_clears_its_old_shelf_position()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var a = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("FA", "Kho A"), Json));
        var b = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("FB", "Kho B"), Json));
        var (shelfId, floorId, rowId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        // Chỉ có ShelfChanged — chưa có FloorChanged (tầng có từ trước khi có sự kiện này, giá thêm sau).
        var shelf = Shelf(tenantId, shelfId, floorId, "Kệ mồ côi", DateTimeOffset.UtcNow, rows: [(rowId, 1, "000", "999")]) with { StorePublicId = a.PublicId };
        await _factory.PublishAsAsync(shelf);
        await _factory.WaitConsumedAsync(shelf);
        var tree = await Read<List<FloorNode>>(await staff.GetAsync(U($"/api/map-shelving/Shelves?storeId={a.Id}")));
        var node = Assert.Single(tree);
        Assert.Equal((floorId, "Kệ mồ côi"), (node.FloorPublicId, Assert.Single(node.Shelves).Name));

        var bib = await PublishBib(tenantId, "Sách thử");
        var item = (await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(bib.Mfn, 2, "FX", 6, null, a.Id), Json)))[0];
        var other = (await Read<CrudPage<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Lookup"), new ItemSearch { Mfn = bib.Mfn }, Json))).Items.Single(i => i.PublicId != item.PublicId);
        await Read<PlaceResult>(await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([item.PublicId, other.PublicId], shelfId, rowId), Json));

        // Bản mất vẫn giữ mã giá nhưng không tính vào số bản trên giá.
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest([other.Barcode], Reason: "Kiểm kê không thấy"), Json));
        var counted = await Read<List<FloorNode>>(await staff.GetAsync(U($"/api/map-shelving/Shelves?storeId={a.Id}")));
        Assert.Equal((1, 1), (counted[0].Shelves[0].Items, counted[0].Shelves[0].Rows[0].Items));

        // Sửa kho ở màn bản sách: vị trí giá cũ bị xoá và ghi vào lịch sử di chuyển.
        var edited = await Read<ItemDto>(await staff.PutAsJsonAsync(U($"/api/items/Update/{item.PublicId}"), new ItemRequest(StoreId: b.Id, Note: "chuyển kho"), Json));
        Assert.Equal((b.Id, (Guid?)null), (edited.StoreId, edited.ShelfPublicId));
        var trail = await Read<List<ItemMovementDto>>(await staff.GetAsync(U($"/api/items/Movements/{item.PublicId}")));
        Assert.Equal("MOVE", trail[0].Kind);
        Assert.Equal(("Kho A", "Kho B", "Kệ mồ côi"), (trail[0].FromStore, trail[0].ToStore, trail[0].FromShelf));
    }

    [Fact]
    public async Task Visual_shelving_suggests_rows_by_ddc_places_reshelves_and_guards_the_rules()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var a = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("SA", "Kho A"), Json));
        var b = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("SB", "Kho B"), Json));
        var (shelfA, shelfB, floorId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var (row1, row2, rowB) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var t0 = DateTimeOffset.UtcNow;
        var floor = new FloorChanged { TenantId = tenantId, OccurredAt = t0, FloorPublicId = floorId, Name = "Tầng 1", FloorNumber = 1, BuildingName = "Toà A" };
        var sa = Shelf(tenantId, shelfA, floorId, "Kệ A", t0, rows: [(row1, 1, "300", "329"), (row2, 2, "330", "339")]) with { StorePublicId = a.PublicId };
        var sb = Shelf(tenantId, shelfB, floorId, "Kệ B", t0, rows: [(rowB, 1, "300", "399")]) with { StorePublicId = b.PublicId };
        foreach (var e in new Elib.Contracts.Events.IntegrationEvent[] { floor, sa, sb })
        {
            await _factory.PublishAsAsync(e);
            await _factory.WaitConsumedAsync(e);
        }

        var social = await PublishBib(tenantId, "Kinh tế chính trị", "335.4");
        var general = await PublishBib(tenantId, "Tổng quát", "301");
        var novel = await PublishBib(tenantId, "Tiểu thuyết", "895.9223");
        async Task<ItemDto> One(BibChanged bib, string prefix, long storeId) =>
            (await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(bib.Mfn, 1, prefix, 6, null, storeId), Json)))[0];
        var (i1, i2, i3, other) = (await One(social, "KT", a.Id), await One(general, "TQ", a.Id), await One(novel, "TT", a.Id), await One(social, "KB", b.Id));

        // Tìm: kho A, chưa xếp giá → gợi ý đúng ngăn theo DDC (895 không có ngăn phù hợp).
        var found = await Read<CrudPage<MapShelvingRow>>(await staff.PostAsJsonAsync(U("/api/map-shelving/Search"), new MapShelvingSearch { StoreId = a.Id }, Json));
        Assert.Equal(3, found.TotalCount);
        var byBarcode = found.Items.ToDictionary(r => r.Barcode);
        Assert.Equal((2, "Kệ A"), (byBarcode[i1.Barcode].SuggestedRowIndex, byBarcode[i1.Barcode].SuggestedShelfName));
        Assert.Equal(1, byBarcode[i2.Barcode].SuggestedRowIndex);
        Assert.Null(byBarcode[i3.Barcode].SuggestedShelfPublicId);

        var tree = await Read<List<FloorNode>>(await staff.GetAsync(U($"/api/map-shelving/Shelves?storeId={a.Id}")));
        var floorNode = Assert.Single(tree);
        Assert.Equal(("Tầng 1", "Toà A"), (floorNode.Name, floorNode.BuildingName));
        Assert.Equal(["Kệ A"], floorNode.Shelves.Select(s => s.Name)); // chỉ giá của kho A

        // Xếp: I → R, ghi vị trí và lịch sử PLACE.
        var placed = await Read<PlaceResult>(await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([i1.PublicId, i2.PublicId], shelfA, row2), Json));
        Assert.Equal(2, placed.Placed);
        var moved = await Read<ItemDto>(await staff.GetAsync(U($"/api/items/GetById/{i1.PublicId}")));
        Assert.Equal(("R", shelfA, "Kệ A", 2, "330 – 339"), (moved.Status, moved.ShelfPublicId, moved.ShelfName, moved.ShelfRowIndex, moved.ShelfRowRange));

        // Xếp lại: cùng vị trí bị bỏ qua; đổi ngăn → RESHELVE.
        var same = await Read<PlaceResult>(await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([i1.PublicId], shelfA, row2), Json));
        Assert.Equal((0, "Đã ở đúng vị trí này."), (same.Placed, Assert.Single(same.Skipped).Reason));
        var reshelved = await Read<PlaceResult>(await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([i1.PublicId], shelfA, row1, "Dọn lại kệ"), Json));
        Assert.Equal(1, reshelved.Placed);
        var trail = await Read<List<ItemMovementDto>>(await staff.GetAsync(U($"/api/items/Movements/{i1.PublicId}")));
        Assert.Equal(["RESHELVE", "PLACE"], trail.Select(m => m.Kind));
        Assert.Equal(("Ngăn 2 (330 – 339)", "Ngăn 1 (300 – 329)", "Dọn lại kệ"), (trail[0].FromRow, trail[0].ToRow, trail[0].Reason));
        Assert.Equal(("I", "R"), (trail[1].FromStatus, trail[1].ToStatus));

        // Quy tắc: giá của kho khác, ngăn của giá khác, thiếu quyền, bản đã mất.
        var wrongStore = await Read<PlaceResult>(await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([other.PublicId], shelfA), Json));
        Assert.Equal(0, wrongStore.Placed);
        Assert.Contains("kho khác", Assert.Single(wrongStore.Skipped).Reason);
        var wrongRow = await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([i3.PublicId], shelfA, rowB), Json);
        Assert.Equal("ROW_NOT_IN_SHELF", JsonDocument.Parse(await wrongRow.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        var viewer = StaffWith(tenantId, "MAP_SHELVING:view");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([i3.PublicId], shelfA), Json)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await viewer.PostAsJsonAsync(U("/api/map-shelving/Search"), new MapShelvingSearch(), Json)).StatusCode);
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest([i3.Barcode], Reason: "Kiểm kê không thấy"), Json));
        var lost = await Read<PlaceResult>(await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([i3.PublicId], shelfA), Json));
        Assert.Equal((0, 1), (lost.Placed, lost.Skipped.Count));

        // Danh sách xếp lại (đã xếp giá) thấy vị trí hiện tại; số bản trên sơ đồ giá cập nhật.
        var shelved = await Read<CrudPage<MapShelvingRow>>(await staff.PostAsJsonAsync(U("/api/map-shelving/Search"), new MapShelvingSearch { Status = "R", StoreId = a.Id }, Json));
        Assert.Equal(2, shelved.TotalCount);
        var mine = shelved.Items.Single(r => r.Barcode == i1.Barcode);
        Assert.Equal(("Kệ A", 1), (mine.ShelfName, mine.RowIndex));
        var tree2 = await Read<List<FloorNode>>(await staff.GetAsync(U($"/api/map-shelving/Shelves?storeId={a.Id}")));
        Assert.Equal((2, 1, 1), (tree2[0].Shelves[0].Items, tree2[0].Shelves[0].Rows[0].Items, tree2[0].Shelves[0].Rows[1].Items)); // ngăn 1: i1; ngăn 2: i2

        // Chuyển kho thì vị trí giá cũ không còn đúng → xoá.
        var move = await Read<StoreMoveDto>(await staff.PostAsJsonAsync(U("/api/store-moves/Add"), new StoreMoveRequest(a.Id, b.Id), Json));
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/store-moves/AddLines/{move.PublicId}"), new AddLinesRequest(Barcodes: [i1.Barcode]), Json));
        await Read<CompleteResult>(await staff.PostAsync(U($"/api/store-moves/Complete/{move.PublicId}"), null));
        var afterMove = await Read<ItemDto>(await staff.GetAsync(U($"/api/items/GetById/{i1.PublicId}")));
        Assert.Equal((b.Id, (Guid?)null), (afterMove.StoreId, afterMove.ShelfPublicId));

        // Giá bị xoá ở space → xếp vào giá đó bị từ chối, sơ đồ không còn giá, bản cũ hiển thị "giá đã xoá".
        var gone = Shelf(tenantId, shelfA, floorId, "Kệ A", t0.AddMinutes(5), deleted: true) with { StorePublicId = a.PublicId };
        await _factory.PublishAsync(gone);
        await _factory.WaitConsumedAsync(gone);
        var noShelf = await staff.PostAsJsonAsync(U("/api/map-shelving/Place"), new PlaceRequest([i2.PublicId], shelfA), Json);
        Assert.Equal("SHELF_NOT_FOUND", JsonDocument.Parse(await noShelf.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        Assert.Empty(await Read<List<FloorNode>>(await staff.GetAsync(U($"/api/map-shelving/Shelves?storeId={a.Id}"))));
        var orphan = (await Read<CrudPage<MapShelvingRow>>(await staff.PostAsJsonAsync(U("/api/map-shelving/Search"), new MapShelvingSearch { Status = "R", StoreId = a.Id }, Json))).Items.Single();
        Assert.Equal(("Kệ A", true), (orphan.ShelfName, orphan.ShelfDeleted));
    }
}
