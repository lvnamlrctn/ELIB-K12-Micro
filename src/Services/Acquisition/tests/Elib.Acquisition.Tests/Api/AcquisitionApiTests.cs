using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.Acquisition.Application;
using Elib.Acquisition.Domain;
using Elib.Acquisition.Infrastructure;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Acquisition;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;

namespace Elib.Acquisition.Tests.Api;

public sealed class AcquisitionApiTests : IClassFixture<AcquisitionApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 12000;
    private static long _nextUserId = 22000;
    private static long _nextMfn = 900;

    private readonly AcquisitionApiFactory _factory;

    public AcquisitionApiTests(AcquisitionApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) _factory.Licenses.Licensed.Add((tenantId, "ACQUISITION"));
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

    private static BibChanged Bib(long tenantId, string title, long? mfn = null) => new()
    {
        TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = mfn ?? Interlocked.Increment(ref _nextMfn), Title = title, Author = "Nam Cao",
        Isbns = ["9786041000001"], Status = 2,
    };

    private async Task<BibChanged> PublishBib(long tenantId, string title)
    {
        var bib = Bib(tenantId, title);
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib);
        return bib;
    }

    private ReceiptChanged LastState(Guid receiptPublicId) =>
        _factory.PublishedOf<ReceiptChanged>().Where(e => e.ReceiptPublicId == receiptPublicId).MaxBy(e => e.Version)!;

    [Fact]
    public async Task New_tenant_gets_default_sources_and_a_bib_replica_copied_from_catalog()
    {
        var tenantId = NewTenant();
        foreach (var title in new[] { "Tắt đèn", "Bỉ vỏ", "Giông tố" })
        {
            var bib = Bib(tenantId, title);
            _factory.CatalogBibs.Bibs[(tenantId, bib.Mfn)] = bib;
        }
        await _factory.PublishAsync(new TenantProvisioned
        {
            TenantId = tenantId, Code = "A" + tenantId, Name = "Trường " + tenantId, Subdomain = "a" + tenantId, TimeZone = "Asia/Ho_Chi_Minh",
            Modules = [new ModuleLicense("ACQUISITION", "Active", null, null)],
        });
        await AcquisitionApiFactory.WaitForAsync(() => Task.FromResult(_factory.PublishedOf<TenantSeeded>().FirstOrDefault(s => s.TenantId == tenantId)), "TenantSeeded");

        var staff = Staff(tenantId);
        var sources = await Read<List<NamedItemDto>>(await staff.PostAsJsonAsync(U("/api/sources/SearchAll"), new CrudSearch(), Json));
        Assert.Equal(SourceResource.Defaults.Order(StringComparer.Ordinal), sources.Select(s => s.Name).Order(StringComparer.Ordinal));
        var calls = _factory.CatalogBibs.Calls;
        var found = await Read<List<BibOption>>(await staff.PostAsJsonAsync(U("/api/bibs/Lookup"), new CrudSearch { Keyword = "giông" }, Json));
        Assert.Equal("Giông tố", Assert.Single(found).Title); // đã chép sẵn (3 biểu ghi, 2 trang) — không hỏi catalog theo MFN
        Assert.Equal(calls, _factory.CatalogBibs.Calls);

        // Đơn vị mua phân hệ sau: chép khi nhận ModuleLicenseChanged.
        var later = NewTenant();
        var old = Bib(later, "Số đỏ");
        _factory.CatalogBibs.Bibs[(later, old.Mfn)] = old;
        var changed = new ModuleLicenseChanged { TenantId = later, Modules = [new ModuleLicense("ACQUISITION", "Active", null, null)], SourceVersion = 1 };
        await _factory.PublishAsync(changed);
        await AcquisitionApiFactory.WaitForAsync(() => Task.FromResult(_factory.Consumed.OfType<ModuleLicenseChanged>()
            .Count(c => c.EventId == changed.EventId) >= 2 ? changed : null), "ModuleLicenseChanged");
        Assert.Equal("Số đỏ", Assert.Single(await Read<List<BibOption>>(await Staff(later).PostAsJsonAsync(U("/api/bibs/Lookup"), new CrudSearch { Keyword = "số đỏ" }, Json))).Title);
    }

    [Fact]
    public async Task Order_lines_track_received_quantities_from_receipts()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var supplier = await Read<SupplierDto>(await staff.PostAsJsonAsync(U("/api/suppliers/Add"), new { name = "Fahasa", email = "kd@fahasa.vn" }, Json));
        var bib = await PublishBib(tenantId, "Lão Hạc");
        var order = await Read<PurchaseOrderDto>(await staff.PostAsJsonAsync(U("/api/orders/Add"),
            new PurchaseOrderRequest("Đặt sách quý 4", new DateOnly(2026, 10, 1), SupplierId: supplier.Id), Json));

        var catalogued = await Read<OrderLineDto>(await staff.PostAsJsonAsync(U($"/api/orders/SaveLine/{order.PublicId}"),
            new OrderLineRequest(Mfn: bib.Mfn, Quantity: 5, Price: 45000m, Currency: "vnd"), Json));
        Assert.Equal(("Lão Hạc", "VND", 5), (catalogued.Title, catalogued.Currency, catalogued.Remaining));
        var draft = await Read<OrderLineDto>(await staff.PostAsJsonAsync(U($"/api/orders/SaveLine/{order.PublicId}"),
            new OrderLineRequest(Title: "Sách chưa biên mục", Isbn: "978604", Quantity: 2), Json));
        Assert.Null(draft.Mfn);
        Assert.Equal("LINE_EXISTS", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/orders/SaveLine/{order.PublicId}"), new OrderLineRequest(Mfn: bib.Mfn), Json)));

        var receipt = await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 10, 10), "Nhận đợt 1"), Json));
        var receivable = await Read<CrudPage<ReceivableLine>>(await staff.PostAsJsonAsync(U("/api/receipts/Receivable"), new ReceivableSearch { OrderId = order.Id }, Json));
        Assert.Equal(2, receivable.TotalCount);

        Task<HttpResponseMessage> SaveLine(ReceiptLineRequest r) => staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"), r, Json);
        Assert.Equal("ORDER_LINE_EXCEEDED", await ErrorCode(await SaveLine(new ReceiptLineRequest(Quantity: 6, OrderLinePublicId: catalogued.PublicId))));
        Assert.Equal("MFN_REQUIRED", await ErrorCode(await SaveLine(new ReceiptLineRequest(Quantity: 1, OrderLinePublicId: draft.PublicId))));
        var line = await Read<ReceiptLineDto>(await SaveLine(new ReceiptLineRequest(Quantity: 3, OrderLinePublicId: catalogued.PublicId)));
        Assert.Equal((bib.Mfn, 45000m, "VND", order.Id), (line.Mfn, line.Price, line.Currency, line.OrderId)); // giá, tiền tệ theo dòng đặt

        var lines = await Read<List<OrderLineDto>>(await staff.GetAsync(U($"/api/orders/Lines/{order.PublicId}")));
        Assert.Equal((3, 2), (lines.Single(l => l.PublicId == catalogued.PublicId).Received, lines.Single(l => l.PublicId == catalogued.PublicId).Remaining));
        Assert.Equal(3, (await Read<PurchaseOrderDto>(await staff.GetAsync(U($"/api/orders/GetById/{order.PublicId}")))).Received);
        Assert.Equal("QUANTITY_BELOW_RECEIVED", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/orders/SaveLine/{order.PublicId}"),
            new OrderLineRequest(LinePublicId: catalogued.PublicId, Mfn: bib.Mfn, Quantity: 2), Json)));
        Assert.Equal("LINE_RECEIVED", await ErrorCode(await staff.DeleteAsync(U($"/api/orders/Lines/{catalogued.PublicId}"))));
        Assert.Equal("ORDER_RECEIVED", await ErrorCode(await staff.DeleteAsync(U($"/api/orders/Delete/{order.PublicId}"))));
        Assert.Equal("SUPPLIER_IN_USE", await ErrorCode(await staff.DeleteAsync(U($"/api/suppliers/Delete/{supplier.PublicId}"))));
    }

    [Fact]
    public async Task Receipt_registration_is_capped_by_line_quantity_and_locked_when_completed()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Chí Phèo");
        var receipt = await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"),
            new ReceiptRequest(new DateOnly(2026, 9, 20), "Biếu tặng", StoreId: 7), Json));
        Assert.DoesNotContain(_factory.PublishedOf<ReceiptChanged>(), e => e.ReceiptPublicId == receipt.PublicId); // chưa có dòng — chưa phát

        var line = await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"), new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 3), Json));
        var state = LastState(receipt.PublicId);
        Assert.Equal((receipt.Id, new DateOnly(2026, 9, 20), 7L, false), (state.Code, state.ReceivedOn, state.StoreId!.Value, state.Completed));
        Assert.Equal((line.PublicId, 3), (Assert.Single(state.Lines).LinePublicId, state.Lines[0].Quantity));
        // Chọn lại biểu ghi đã có trong phiếu (như ELIB-LRC) → ghi đè dòng đó, không thêm dòng mới.
        var overwritten = await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
            new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 3, Price: 50000m, Currency: "vnd"), Json));
        Assert.Equal((line.PublicId, 3, 50000m, "VND"), (overwritten.PublicId, overwritten.Quantity, overwritten.Price, overwritten.Currency));
        Assert.Single((await Read<ReceiptLinesView>(await staff.GetAsync(U($"/api/receipts/Lines/{receipt.PublicId}")))).Lines);

        Task<HttpResponseMessage> Register(RegisterReceiptItemsRequest r) => staff.PostAsJsonAsync(U($"/api/receipts/RegisterItems/{line.PublicId}"), r, Json);
        var batch = await Read<List<RegisteredItem>>(await Register(new RegisterReceiptItemsRequest(Quantity: 2, Prefix: "CP")));
        Assert.Equal(2, batch.Count);
        Assert.Equal((receipt.PublicId, 3), (_factory.Holdings.LastRequest!.Receipt.ReceiptPublicId, _factory.Holdings.LastRequest.Receipt.Lines[0].Quantity));
        Assert.Equal("RECEIPT_LINE_FULL", await ErrorCode(await Register(new RegisterReceiptItemsRequest(Quantity: 2, Prefix: "CP"))));

        var view = await Read<ReceiptLinesView>(await staff.GetAsync(U($"/api/receipts/Lines/{receipt.PublicId}")));
        Assert.Equal((true, 2), (view.ItemsAvailable, Assert.Single(view.Lines).Registered!.Value));
        // Không giảm số bản dưới số ĐKCB đã đăng ký; không bỏ dòng còn ĐKCB; không xoá phiếu còn ĐKCB.
        Assert.Equal("QUANTITY_BELOW_REGISTERED", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
            new ReceiptLineRequest(LinePublicId: line.PublicId, Quantity: 1), Json)));
        Assert.Equal("QUANTITY_BELOW_REGISTERED", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
            new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 1), Json))); // ghi đè cũng không giảm dưới số ĐKCB
        Assert.Equal("LINE_HAS_ITEMS", await ErrorCode(await staff.DeleteAsync(U($"/api/receipts/Lines/{line.PublicId}"))));
        Assert.Equal("RECEIPT_HAS_ITEMS", await ErrorCode(await staff.DeleteAsync(U($"/api/receipts/Delete/{receipt.PublicId}"))));
        await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"), new ReceiptLineRequest(LinePublicId: line.PublicId, Quantity: 2), Json));

        // Hoàn tất → khoá phiếu; holdings thấy trạng thái đã hoàn tất.
        var done = await Read<ReceiptDto>(await staff.PostAsync(U($"/api/receipts/Complete/{receipt.PublicId}"), null));
        Assert.NotNull(done.CompletedAt);
        Assert.True(LastState(receipt.PublicId).Completed);
        Assert.Equal("RECEIPT_COMPLETED", await ErrorCode(await Register(new RegisterReceiptItemsRequest(Barcode: "CP-X"))));
        Assert.Equal("RECEIPT_COMPLETED", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
            new ReceiptLineRequest(LinePublicId: line.PublicId, Quantity: 5), Json)));
        Assert.Equal("RECEIPT_COMPLETED", await ErrorCode(await staff.DeleteAsync(U($"/api/receipts/Items/{receipt.PublicId}/{batch[0].PublicId}"))));
        Assert.Equal("RECEIPT_COMPLETED", await ErrorCode(await staff.PutAsJsonAsync(U($"/api/receipts/Update/{receipt.PublicId}"),
            new ReceiptRequest(new DateOnly(2026, 9, 21)), Json)));

        // Mở lại cần quyền xoá; mở lại rồi thì xoá được ĐKCB nhầm.
        var editor = Staff(tenantId, "AB_RECEIPTS:view", "AB_RECEIPTS:edit");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsync(U($"/api/receipts/Reopen/{receipt.PublicId}"), null)).StatusCode);
        Assert.Null((await Read<ReceiptDto>(await staff.PostAsync(U($"/api/receipts/Reopen/{receipt.PublicId}"), null))).CompletedAt);
        Assert.False(LastState(receipt.PublicId).Completed);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/receipts/Items/{receipt.PublicId}/{batch[0].PublicId}"))).StatusCode);
        Assert.Equal(1, (await Read<ReceiptLinesView>(await staff.GetAsync(U($"/api/receipts/Lines/{receipt.PublicId}")))).Lines[0].Registered);
        Assert.True(_factory.PublishedOf<ReceiptChanged>().Where(e => e.ReceiptPublicId == receipt.PublicId).Select(e => e.Version).Distinct().Count() >= 4);
    }

    [Fact]
    public async Task Holdings_outage_still_shows_lines_but_refuses_registration_and_quantity_cuts()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Đời thừa");
        var receipt = await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 9, 1)), Json));
        var line = await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"), new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 4), Json));
        _factory.Holdings.Down = true;
        try
        {
            var view = await Read<ReceiptLinesView>(await staff.GetAsync(U($"/api/receipts/Lines/{receipt.PublicId}")));
            Assert.Equal((false, (int?)null), (view.ItemsAvailable, Assert.Single(view.Lines).Registered));
            var refused = await staff.PostAsJsonAsync(U($"/api/receipts/RegisterItems/{line.PublicId}"), new RegisterReceiptItemsRequest(Quantity: 1, Prefix: "DT"), Json);
            Assert.Equal((HttpStatusCode.ServiceUnavailable, "HOLDINGS_UNAVAILABLE"), (refused.StatusCode, await ErrorCode(refused)));
            Assert.Equal("HOLDINGS_UNAVAILABLE", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
                new ReceiptLineRequest(LinePublicId: line.PublicId, Quantity: 2), Json)));
            // Tăng số bản không cần hỏi holdings.
            Assert.Equal(6, (await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
                new ReceiptLineRequest(LinePublicId: line.PublicId, Quantity: 6), Json))).Quantity);
        }
        finally
        {
            _factory.Holdings.Down = false;
        }
    }

    [Fact]
    public async Task Receipt_list_filters_by_fund_and_creation_date()
    {
        var staff = Staff(NewTenant());
        var fund = await Read<FundDto>(await staff.PostAsJsonAsync(U("/api/funds/Add"), new FundRequest("Quỹ sách giáo khoa"), Json));
        var withFund = await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 9, 1), "Có quỹ", FundId: fund.Id), Json));
        await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 9, 1), "Không quỹ"), Json);

        var byFund = await Read<CrudPage<ReceiptDto>>(await staff.PostAsJsonAsync(U("/api/receipts/Search"), new ReceiptSearch { FundId = fund.Id }, Json));
        Assert.Equal(withFund.PublicId, Assert.Single(byFund.Items).PublicId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var created = await Read<CrudPage<ReceiptDto>>(await staff.PostAsJsonAsync(U("/api/receipts/Search"),
            new ReceiptSearch { CreatedFrom = today.AddDays(-1), CreatedTo = today.AddDays(1) }, Json));
        Assert.Equal(2, created.TotalCount);
        var past = await Read<CrudPage<ReceiptDto>>(await staff.PostAsJsonAsync(U("/api/receipts/Search"), new ReceiptSearch { CreatedTo = today.AddDays(-2) }, Json));
        Assert.Equal(0, past.TotalCount);
    }

    [Fact]
    public async Task Receipt_line_accepts_a_bib_just_created_in_catalog_before_its_event_arrives()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = Bib(tenantId, "Vừa biên mục trong phiếu nhận");
        _factory.CatalogBibs.Bibs[(tenantId, bib.Mfn)] = bib; // catalog đã có, bản sao ở acquisition chưa có (event chưa tới)
        var receipt = await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 10, 1)), Json));

        var line = await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"), new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 2), Json));
        Assert.Equal((bib.Mfn, "Vừa biên mục trong phiếu nhận", 2), (line.Mfn, line.Title, line.Quantity));
    }

    [Fact]
    public async Task Receipt_list_can_be_limited_to_my_receipts_and_lines_follow_the_edited_bib()
    {
        var tenantId = NewTenant();
        var me = Staff(tenantId);
        var other = Staff(tenantId);
        var mine = await Read<ReceiptDto>(await me.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 9, 1), "Của tôi"), Json));
        await other.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 9, 1), "Của người khác"), Json);
        var page = await Read<CrudPage<ReceiptDto>>(await me.PostAsJsonAsync(U("/api/receipts/Search"), new ReceiptSearch { Mine = true }, Json));
        Assert.Equal(mine.PublicId, Assert.Single(page.Items).PublicId);
        Assert.Equal(2, (await Read<CrudPage<ReceiptDto>>(await me.PostAsJsonAsync(U("/api/receipts/Search"), new ReceiptSearch(), Json))).TotalCount);

        var bib = await PublishBib(tenantId, "Tên cũ");
        await me.PostAsJsonAsync(U($"/api/receipts/SaveLine/{mine.PublicId}"), new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 1), Json);
        var edited = new BibChanged { TenantId = tenantId, BibPublicId = bib.BibPublicId, Mfn = bib.Mfn, Title = "Tên mới", Author = "Nam Cao", Isbns = [], Status = 2, Version = 5 };
        await _factory.PublishAsync(edited);
        await _factory.WaitConsumedAsync(edited);
        var view = await Read<ReceiptLinesView>(await me.GetAsync(U($"/api/receipts/Lines/{mine.PublicId}")));
        Assert.Equal("Tên mới", Assert.Single(view.Lines).Title);
    }

    [Fact]
    public async Task Bib_lookup_by_mfn_asks_catalog_when_the_replica_misses_and_other_tenants_are_isolated()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId, "AB_RECEIPTS:view");
        var bib = Bib(tenantId, "Vang bóng một thời");
        _factory.CatalogBibs.Bibs[(tenantId, bib.Mfn)] = bib;
        var found = await Read<List<BibOption>>(await staff.PostAsJsonAsync(U("/api/bibs/Lookup"),
            new CrudSearch { Keyword = bib.Mfn.ToString(System.Globalization.CultureInfo.InvariantCulture) }, Json));
        Assert.Equal((bib.BibPublicId, "Vang bóng một thời"), (Assert.Single(found).BibPublicId, found[0].Title));

        var other = Staff(NewTenant());
        Assert.Empty(await Read<List<BibOption>>(await other.PostAsJsonAsync(U("/api/bibs/Lookup"), new CrudSearch { Keyword = "vang bóng" }, Json)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(NewTenant(licensed: false)).PostAsJsonAsync(U("/api/receipts/Search"), new ReceiptSearch(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync(U("/api/orders/Search"), new PurchaseOrderSearch(), Json)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_check_search_filters_like_lrc_ignores_accents_and_flags_bibs_already_received_or_ordered()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        async Task<BibChanged> Publish(string title, string author, string publisher, string year, long typeId, string typeName)
        {
            var bib = Bib(tenantId, title) with { Author = author, Publisher = publisher, PublishYear = year, BibTypeId = typeId, BibTypeName = typeName };
            await _factory.PublishAsync(bib);
            await _factory.WaitConsumedAsync(bib);
            return bib;
        }
        var demen = await Publish("Dế mèn phiêu lưu ký", "Tô Hoài", "NXB Kim Đồng", "2019", 1, "Sách");
        var vochong = await Publish("Vợ chồng A Phủ", "Tô Hoài", "NXB Văn học", "2020", 1, "Sách");
        var tapchi = await Publish("Tạp chí Thiếu nhi", "Nhiều tác giả", "NXB Kim Đồng", "2020", 2, "Tạp chí");

        var receipt = await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 10, 1), "Đợt 1"), Json));
        await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"), new ReceiptLineRequest(Mfn: demen.Mfn), Json));
        var order = await Read<PurchaseOrderDto>(await staff.PostAsJsonAsync(U("/api/orders/Add"), new PurchaseOrderRequest("Đặt", new DateOnly(2026, 10, 1)), Json));
        await Read<OrderLineDto>(await staff.PostAsJsonAsync(U($"/api/orders/SaveLine/{order.PublicId}"), new OrderLineRequest(Mfn: vochong.Mfn, Quantity: 2), Json));

        async Task<CrudPage<BibSearchRow>> Search(BibSearchRequest r) =>
            await Read<CrudPage<BibSearchRow>>(await staff.PostAsJsonAsync(U("/api/bibs/Search"), r, Json));

        // Không dấu, không phân biệt hoa thường; sắp MFN giảm dần.
        var byAuthor = await Search(new BibSearchRequest(Author: "to hoai"));
        Assert.Equal([vochong.Mfn, demen.Mfn], byAuthor.Items.Select(b => b.Mfn));
        var flags = byAuthor.Items.ToDictionary(b => b.Mfn, b => (b.HasReceipt, b.HasOrder));
        Assert.Equal((true, false), flags[demen.Mfn]);
        Assert.Equal((false, true), flags[vochong.Mfn]);
        Assert.Equal(demen.Mfn, Assert.Single((await Search(new BibSearchRequest(Title: "DE MEN"))).Items).Mfn);
        Assert.Equal([tapchi.Mfn, demen.Mfn], (await Search(new BibSearchRequest(Publisher: "kim dong"))).Items.Select(b => b.Mfn));
        Assert.Equal(tapchi.Mfn, Assert.Single((await Search(new BibSearchRequest(BibTypeId: 2))).Items).Mfn);
        Assert.Equal([tapchi.Mfn, vochong.Mfn], (await Search(new BibSearchRequest(PublishYear: "2020"))).Items.Select(b => b.Mfn));
        Assert.Equal(vochong.Mfn, Assert.Single((await Search(new BibSearchRequest(MfnFrom: vochong.Mfn, MfnTo: vochong.Mfn))).Items).Mfn);
        Assert.Equal(("Sách", "NXB Văn học", "2020"), (byAuthor.Items[0].BibTypeName, byAuthor.Items[0].Publisher, byAuthor.Items[0].PublishYear));

        // Phân trang 10 dòng mặc định, trang 2.
        var paged = await Search(new BibSearchRequest(Author: "to hoai", PageIndex: 2, PageSize: 1));
        Assert.Equal((2, demen.Mfn), (paged.TotalCount, Assert.Single(paged.Items).Mfn));

        var types = await Read<List<BibTypeOption>>(await staff.GetAsync(U("/api/bibs/Types")));
        Assert.Equal([new BibTypeOption(1, "Sách"), new BibTypeOption(2, "Tạp chí")], types);

        // Đơn vị khác không thấy; cán bộ không có quyền đơn đặt/phiếu nhận bị chặn.
        Assert.Equal(0, (await Read<CrudPage<BibSearchRow>>(await Staff(NewTenant()).PostAsJsonAsync(U("/api/bibs/Search"), new BibSearchRequest(Author: "to hoai"), Json))).TotalCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "SUPPLIERS:view").PostAsJsonAsync(U("/api/bibs/Search"), new BibSearchRequest(), Json)).StatusCode);
    }

    [Fact]
    public async Task Startup_sync_fills_type_and_search_keys_of_replica_rows_written_before_the_upgrade()
    {
        var tenantId = NewTenant();
        var inCatalog = Bib(tenantId, "Số đỏ") with { BibTypeId = 5, BibTypeName = "Sách", Version = 3 };
        _factory.CatalogBibs.Bibs[(tenantId, inCatalog.Mfn)] = inCatalog;
        var gone = Bib(tenantId, "Giông tố");
        using (var scope = _factory.Services.CreateScope())
        {
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
            var db = scope.ServiceProvider.GetRequiredService<ICrudDbContext>();
            // Dòng cũ: cùng version với catalog, chưa có loại / khoá tìm (Synced = false).
            db.Set<BibSnapshot>().Add(new BibSnapshot { BibPublicId = inCatalog.BibPublicId, Mfn = inCatalog.Mfn, Title = "Số đỏ", Version = 3 });
            db.Set<BibSnapshot>().Add(new BibSnapshot { BibPublicId = gone.BibPublicId, Mfn = gone.Mfn, Title = "Giông tố", Author = "Vũ Trọng Phụng", Version = 1 });
            await db.SaveChangesAsync();
        }

        var sync = _factory.Services.GetServices<IHostedService>().OfType<BibSnapshotSync>().Single();
        Assert.Equal(0, await sync.RunOnceAsync(CancellationToken.None));

        var staff = Staff(tenantId);
        var typed = Assert.Single((await Read<CrudPage<BibSearchRow>>(await staff.PostAsJsonAsync(U("/api/bibs/Search"), new BibSearchRequest(BibTypeId: 5), Json))).Items);
        Assert.Equal(inCatalog.Mfn, typed.Mfn);
        // Biểu ghi catalog không trả về: khoá tìm tự tính từ dữ liệu đang có.
        Assert.Equal(gone.Mfn, Assert.Single((await Read<CrudPage<BibSearchRow>>(await staff.PostAsJsonAsync(U("/api/bibs/Search"),
            new BibSearchRequest(Author: "vu trong phung"), Json))).Items).Mfn);
    }
}
