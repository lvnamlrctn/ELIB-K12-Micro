using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Holdings.Application;

namespace Elib.Holdings.Tests.Api;

/// <summary>Kiểm kê, mất, thanh lý, xuất kho, điều chuyển, giao nhận, đăng ký lại mã, thống kê — chạy trên service thật (SQLite, bus in-memory).</summary>
public sealed class StoreOperationsApiTests : IClassFixture<HoldingsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 9000;
    private static long _nextUserId = 19000;
    private static long _nextMfn = 5000;

    private readonly HoldingsApiFactory _factory;

    public StoreOperationsApiTests(HoldingsApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        _factory.Licenses.Licensed.Add((tenantId, "HOLDINGS"));
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

    private static async Task Ok(HttpResponseMessage response) =>
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private async Task<BibChanged> PublishBib(long tenantId, string title, string? ddc = "895.9223", long? typeId = 1, string? typeName = "Sách")
    {
        var bib = new BibChanged
        {
            TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Author = "Nam Cao",
            PublishYear = "2019", Isbns = [], Ddc = ddc, BibTypeId = typeId, BibTypeName = typeName, Status = 2,
        };
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib);
        return bib;
    }

    private static async Task<StoreDto> AddStore(HttpClient staff, string code) =>
        await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest(code, "Kho " + code), Json));

    /// <summary>Đăng ký <paramref name="quantity"/> bản (tiền tố riêng), tuỳ chọn xếp giá luôn.</summary>
    private static async Task<List<ItemDto>> Items(HttpClient staff, long mfn, string prefix, int quantity, long? storeId = null, bool shelve = true)
    {
        var items = await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Register"),
            new RegisterItemsRequest(mfn, quantity, prefix, 4, null, storeId), Json));
        if (shelve) await Read<ShelveResult>(await staff.PostAsJsonAsync(U("/api/items/Shelve"), new ShelveRequest(Ids: [.. items.Select(i => i.PublicId)]), Json));
        return items;
    }

    private static async Task<ItemDto> Item(HttpClient staff, Guid publicId) => await Read<ItemDto>(await staff.GetAsync(U($"/api/items/GetById/{publicId}")));

    private async Task Loan(long tenantId, ItemDto item, bool returned = false, string? closed = null, long version = 0, Guid? loanId = null, bool reopened = false)
    {
        var now = DateTimeOffset.UtcNow;
        var e = new LoanChanged
        {
            TenantId = tenantId, LoanPublicId = loanId ?? Guid.CreateVersion7(), ReaderPublicId = Guid.CreateVersion7(), CardNo = "HS99",
            ItemPublicId = item.PublicId, Barcode = item.Barcode, Mfn = item.Mfn, LoanedAt = now, DueAt = now.AddDays(14),
            ReturnedAt = returned ? now : null, ClosedItemStatus = closed, Version = version, Reopened = reopened,
        };
        await _factory.PublishAsync(e);
        await _factory.WaitConsumedAsync(e);
    }

    private ItemChanged LastEvent(Guid itemId) => _factory.PublishedOf<ItemChanged>().Where(e => e.ItemPublicId == itemId).MaxBy(e => e.Version)!;

    private static MemoryStream Workbook(params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++) sheet.Cell(r + 1, c + 1).Value = rows[r][c];
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static MultipartFormDataContent FileForm(MemoryStream stream)
    {
        var form = new MultipartFormDataContent();
        var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(CrudExcel.ContentType);
        form.Add(content, "file", "data.xlsx");
        return form;
    }

    private static readonly string[] ClerkGrants = ["LOST_BOOKS:view", "LOST_BOOKS:add", "LOST_BOOKS:edit", "LIQUIDATES:view", "LIQUIDATES:add", "LIQUIDATES:edit", "CATALOG_BIBS:view", "AB_RECEIPTS:view"];

    [Fact]
    public async Task Liquidation_needs_a_reason_and_waits_for_approval_by_default_and_keeps_the_decision_and_previous_status()
    {
        var tenantId = NewTenant();
        var admin = Staff(tenantId);
        var clerk = Staff(tenantId, ClerkGrants);
        var bib = await PublishBib(tenantId, "Tắt đèn");
        var items = await Items(admin, bib.Mfn, "TD", 2, shelve: false); // chưa xếp giá (I)
        await Items(admin, bib.Mfn, "TE", 1);

        Assert.Equal("REASON_REQUIRED", await ErrorCode(await clerk.PostAsJsonAsync(U("/api/liquidations/Liquidate"), new LiquidateRequest(["TD0001"]), Json)));
        var held = await clerk.PostAsJsonAsync(U("/api/liquidations/Liquidate"),
            new LiquidateRequest(["TD0001", "TD0002"], Reason: "Hư hỏng nặng", DecisionNo: "QĐ-12/2026", DecisionDate: new DateOnly(2026, 10, 1)), Json);
        Assert.Equal(HttpStatusCode.Accepted, held.StatusCode);
        var pending = await Read<PendingApproval>(held);
        Assert.Equal(StoreApprovals.Liquidate, pending.Kind);
        Assert.Equal("I", (await Item(admin, items[0].PublicId)).Status); // chưa đổi gì

        Assert.Equal("APPROVAL_FORBIDDEN", await ErrorCode(await clerk.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest(), Json)));
        await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{pending.RequestId}/Approve"), new ApprovalDecisionRequest("Đã có quyết định"), Json));
        var list = await Read<CrudPage<LiquidationRow>>(await admin.PostAsJsonAsync(U("/api/liquidations/Search"), new StoreRecordSearch(), Json));
        Assert.Equal(2, list.TotalCount);
        Assert.All(list.Items, r => Assert.Equal(("QĐ-12/2026", new DateOnly(2026, 10, 1), "S"), (r.DecisionNo, r.DecisionDate, r.ItemStatus)));

        // Huỷ thanh lý: lý do bắt buộc, chờ duyệt; bản về ĐÚNG trạng thái trước (chưa xếp giá), bản ghi gốc giữ lại nhưng không còn trong danh sách.
        Assert.Equal("REASON_REQUIRED", await ErrorCode(await admin.PostAsJsonAsync(U("/api/liquidations/Cancel"), new ItemReasonRequest("TD0001", null), Json)));
        var cancelHeld = await clerk.PostAsJsonAsync(U("/api/liquidations/Cancel"), new ItemReasonRequest("TD0001", "Thanh lý nhầm"), Json);
        Assert.Equal(HttpStatusCode.Accepted, cancelHeld.StatusCode);
        await Read<ChangeRequestDto>(await admin.PostAsJsonAsync(U($"/api/approvals/{(await Read<PendingApproval>(cancelHeld)).RequestId}/Approve"), new ApprovalDecisionRequest(), Json));
        Assert.Equal("I", (await Item(admin, items[0].PublicId)).Status);
        Assert.Equal(["TD0002"], (await Read<CrudPage<LiquidationRow>>(await admin.PostAsJsonAsync(U("/api/liquidations/Search"), new StoreRecordSearch(), Json))).Items.Select(r => r.Barcode));
    }

    [Fact]
    public async Task Bulk_lost_marking_over_the_threshold_waits_for_approval_and_restoring_a_lost_loan_item_proposes_a_fine_adjustment()
    {
        var tenantId = NewTenant();
        var admin = Staff(tenantId);
        var clerk = Staff(tenantId, ClerkGrants);
        var bib = await PublishBib(tenantId, "Dế mèn phiêu lưu ký");
        var items = await Items(admin, bib.Mfn, "DM", 12);

        // 3 bản: áp dụng ngay (dưới ngưỡng 10).
        var small = await clerk.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest(["DM0001", "DM0002", "DM0003"], Reason: "Kiểm kê"), Json);
        Assert.Equal(HttpStatusCode.OK, small.StatusCode);
        // 9 bản còn lại + 1 = 10 bản → chờ duyệt, các bản vẫn sẵn sàng.
        var big = await clerk.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest([.. items.Skip(2).Select(i => i.Barcode)], Reason: "Lũ lụt"), Json);
        Assert.Equal(HttpStatusCode.Accepted, big.StatusCode);
        Assert.Equal("R", (await Item(admin, items[11].PublicId)).Status);

        // Khôi phục bản mất khi đang mượn: phát ItemLossReversed để circulation đề xuất điều chỉnh phiếu phạt.
        var loanId = Guid.CreateVersion7();
        await Loan(tenantId, items[11], version: 0, loanId: loanId);
        await Loan(tenantId, items[11], returned: true, closed: "L", version: 1, loanId: loanId);
        Assert.Equal("L", (await Item(admin, items[11].PublicId)).Status);
        var restore = await admin.PostAsJsonAsync(U("/api/lost-books/Restore"), new ItemReasonRequest("DM0012", "Tìm thấy ở kho phụ"), Json);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode); // người có quyền duyệt hoàn tác thẳng
        var evt = Assert.Single(_factory.PublishedOf<ItemLossReversed>(), e => e.ItemPublicId == items[11].PublicId);
        Assert.Equal(("HS99", "Tìm thấy ở kho phụ"), (evt.CardNo, evt.Reason));
        Assert.Equal("R", (await Item(admin, items[11].PublicId)).Status);
    }

    [Fact]
    public async Task Reopening_a_loan_closed_as_lost_takes_the_item_off_the_lost_list_and_back_on_loan()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Số đỏ");
        var items = await Items(staff, bib.Mfn, "SO", 1);
        var loanId = Guid.CreateVersion7();
        await Loan(tenantId, items[0], version: 0, loanId: loanId);
        await Loan(tenantId, items[0], returned: true, closed: "L", version: 1, loanId: loanId);
        Assert.Equal("L", (await Item(staff, items[0].PublicId)).Status);
        Assert.Single((await Read<CrudPage<LostRow>>(await staff.PostAsJsonAsync(U("/api/lost-books/Search"), new StoreRecordSearch(), Json))).Items, r => r.Barcode == "SO0001");

        await Loan(tenantId, items[0], version: 2, loanId: loanId, reopened: true); // circulation mở lại lượt đóng nhầm
        var after = await Item(staff, items[0].PublicId);
        Assert.Equal(("R", true), (after.Status, after.OnLoan));
        Assert.DoesNotContain((await Read<CrudPage<LostRow>>(await staff.PostAsJsonAsync(U("/api/lost-books/Search"), new StoreRecordSearch(), Json))).Items, r => r.Barcode == "SO0001");
        Assert.Equal("R", LastEvent(items[0].PublicId).Status);
    }

    [Fact]
    public async Task Lost_books_batch_skips_loans_and_restore_returns_item_to_shelf()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Chí Phèo");
        var items = await Items(staff, bib.Mfn, "CP", 3);
        await Loan(tenantId, items[2]);

        var result = await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U("/api/lost-books/MarkLost"),
            new MarkLostRequest(["cp0001", "CP0002", "CP0003", "KHONGCO"], new DateOnly(2026, 9, 1), "Kiểm kê không thấy"), Json));
        Assert.Equal(2, result.Done);
        Assert.Equal(["KHONGCO"], result.NotFound);
        Assert.Equal("CP0003", Assert.Single(result.Skipped).Barcode); // đang mượn → xử lý qua phiếu phạt
        Assert.Equal("L", LastEvent(items[0].PublicId).Status);

        // Bản đang mượn bị đóng lượt vì mất (phiếu phạt) → cũng có bản ghi báo mất, kèm thẻ.
        await Loan(tenantId, items[2], returned: true, closed: "L", version: 1, loanId: null);
        var page = await Read<CrudPage<LostRow>>(await staff.PostAsJsonAsync(U("/api/lost-books/Search"), new StoreRecordSearch { Keyword = "cp" }, Json));
        Assert.Equal(3, page.TotalCount);
        Assert.Contains(page.Items, r => r.Barcode == "CP0003" && r.CardNo == "HS99");
        Assert.All(page.Items, r => Assert.Equal(("L", "Chí Phèo"), (r.ItemStatus, r.Title)));
        var dated = await Read<CrudPage<LostRow>>(await staff.PostAsJsonAsync(U("/api/lost-books/Search"),
            new StoreRecordSearch { From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 1) }, Json));
        Assert.Equal(2, dated.TotalCount);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync(U("/api/lost-books/Restore"), new ItemReasonRequest("cp0001", "Hoàn tác theo biên bản"), Json)).StatusCode);
        Assert.Equal("R", (await Item(staff, items[0].PublicId)).Status);
        var again = await staff.PostAsJsonAsync(U("/api/lost-books/Restore"), new ItemReasonRequest("CP0001", "Hoàn tác theo biên bản"), Json);
        Assert.Equal("ITEM_NOT_LOST", await ErrorCode(again));
        Assert.Equal(2, (await Read<CrudPage<LostRow>>(await staff.PostAsJsonAsync(U("/api/lost-books/Search"), new StoreRecordSearch(), Json))).TotalCount);

        var export = await staff.PostAsJsonAsync(U("/api/lost-books/Export"), new StoreRecordSearch(), Json);
        Assert.Equal(CrudExcel.ContentType, export.Content.Headers.ContentType?.MediaType);

        var viewer = Staff(tenantId, "LOST_BOOKS:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest(["CP0002"], Reason: "Kiểm kê không thấy"), Json)).StatusCode);
    }

    [Fact]
    public async Task Liquidation_blocks_out_of_store_and_cancel_restores_lost_state()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Vợ nhặt");
        var items = await Items(staff, bib.Mfn, "VN", 3);
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest(["VN0002"], Reason: "Kiểm kê không thấy"), Json));
        await Read<StoreOutRow>(await staff.PostAsJsonAsync(U("/api/store-outs/ScanOut"), new ScanOutRequest("VN0003"), Json));

        var result = await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U("/api/liquidations/Liquidate"),
            new LiquidateRequest(["VN0001", "VN0002", "VN0003"], Reason: "Rách nát"), Json));
        Assert.Equal(2, result.Done); // bản đã mất thanh lý được
        Assert.Equal("VN0003", Assert.Single(result.Skipped).Barcode); // đang xuất kho
        var list = await Read<CrudPage<LiquidationRow>>(await staff.PostAsJsonAsync(U("/api/liquidations/Search"), new StoreRecordSearch { Keyword = "vợ" }, Json));
        Assert.Equal(2, list.TotalCount);
        Assert.All(list.Items, r => Assert.Equal(("Rách nát", "S"), (r.Reason, r.ItemStatus)));

        await Ok(await staff.PostAsJsonAsync(U("/api/liquidations/Cancel"), new ItemReasonRequest("VN0001", "Hoàn tác theo biên bản"), Json));
        await Ok(await staff.PostAsJsonAsync(U("/api/liquidations/Cancel"), new ItemReasonRequest("VN0002", "Hoàn tác theo biên bản"), Json));
        Assert.Equal("R", (await Item(staff, items[0].PublicId)).Status);
        Assert.Equal("L", (await Item(staff, items[1].PublicId)).Status); // còn bản ghi báo mất
        Assert.Equal("ITEM_NOT_LIQUIDATED", await ErrorCode(await staff.PostAsJsonAsync(U("/api/liquidations/Cancel"), new ItemReasonRequest("VN0001", "Hoàn tác theo biên bản"), Json)));
    }

    [Fact]
    public async Task Store_out_scan_out_and_in_with_catalogs_and_delete_of_open_round()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Số đỏ");
        var items = await Items(staff, bib.Mfn, "SO", 2);
        var reason = await Read<NamedItemDto>(await staff.PostAsJsonAsync(U("/api/export-reasons/Add"), new NameRequest("Triển lãm sách"), Json));
        var unit = await Read<NamedItemDto>(await staff.PostAsJsonAsync(U("/api/book-out-units/Add"), new NameRequest("Trường THCS A"), Json));

        var clerk = Staff(tenantId, "BOOK_OUT_STORE:view", "BOOK_OUT_STORE:edit");
        var options = JsonDocument.Parse(await clerk.GetStringAsync(U("/api/store-outs/Options"))).RootElement;
        Assert.Contains(options.GetProperty("reasons").EnumerateArray(), r => r.GetProperty("name").GetString() == "Triển lãm sách");

        var outRow = await Read<StoreOutRow>(await clerk.PostAsJsonAsync(U("/api/store-outs/ScanOut"),
            new ScanOutRequest("so0001", reason.Id, unit.Id, DelivererName: "Cô Lan", ReceiverName: "Thầy Minh"), Json));
        Assert.Equal(("SO0001", "Triển lãm sách", "Trường THCS A", "Số đỏ"), (outRow.Barcode, outRow.ReasonName, outRow.UnitName, outRow.Title));
        Assert.Null(outRow.InAt);
        Assert.Equal("X", LastEvent(items[0].PublicId).Status);
        Assert.Equal("ITEM_OUT_OF_STORE", await ErrorCode(await clerk.PostAsJsonAsync(U("/api/store-outs/ScanOut"), new ScanOutRequest("SO0001"), Json)));
        Assert.Equal("STORE_OUT_NOT_OPEN", await ErrorCode(await clerk.PostAsJsonAsync(U("/api/store-outs/ScanIn"), new BarcodeRequest("SO0002"), Json)));

        var inRow = await Read<StoreOutRow>(await clerk.PostAsJsonAsync(U("/api/store-outs/ScanIn"), new BarcodeRequest("SO0001"), Json));
        Assert.Equal(outRow.Id, inRow.Id);
        Assert.NotNull(inRow.InAt);
        Assert.Equal("R", (await Item(staff, items[0].PublicId)).Status);

        // Lượt mở bị xoá (nhập nhầm) → bản sách về kho.
        var wrong = await Read<StoreOutRow>(await clerk.PostAsJsonAsync(U("/api/store-outs/ScanOut"), new ScanOutRequest("SO0002"), Json));
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.DeleteAsync(U($"/api/store-outs/Delete/{wrong.PublicId}"))).StatusCode);
        Assert.Equal("REASON_REQUIRED", await ErrorCode(await staff.DeleteAsync(U($"/api/store-outs/Delete/{wrong.PublicId}"))));
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, U($"/api/store-outs/Delete/{wrong.PublicId}"));
        deleteRequest.Headers.Add("X-Change-Reason", Uri.EscapeDataString("Nhập nhầm"));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.SendAsync(deleteRequest)).StatusCode);
        Assert.Equal("R", (await Item(staff, items[1].PublicId)).Status);

        var history = await Read<CrudPage<StoreOutRow>>(await clerk.PostAsJsonAsync(U("/api/store-outs/Search"), new StoreOutSearch { Keyword = "lan" }, Json));
        Assert.Equal(1, history.TotalCount);
        Assert.Equal(0, (await Read<CrudPage<StoreOutRow>>(await clerk.PostAsJsonAsync(U("/api/store-outs/Search"), new StoreOutSearch { Open = true }, Json))).TotalCount);
        Assert.Equal("EXPORT_REASON_IN_USE", await ErrorCode(await staff.DeleteAsync(U($"/api/export-reasons/Delete/{reason.PublicId}"))));
    }

    [Fact]
    public async Task Inventory_scans_compares_and_reports_missing_items_of_the_store()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Tắt đèn");
        var kho = await AddStore(staff, "KA");
        var other = await AddStore(staff, "KB");
        var items = await Items(staff, bib.Mfn, "TD", 5, kho.Id);
        var elsewhere = (await Items(staff, bib.Mfn, "TX", 1, other.Id))[0];
        await Loan(tenantId, items[3]); // đang mượn — không tính thiếu
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest(["TD0005"], Reason: "Kiểm kê không thấy"), Json));

        var inventory = await Read<InventoryDto>(await staff.PostAsJsonAsync(U("/api/inventories/Add"), new InventoryRequest("Kiểm kê HK1", null, kho.Id), Json));
        Task<HttpResponseMessage> Scan(string code) => staff.PostAsJsonAsync(U("/api/inventories/Scan"), new ScanRequest(inventory.PublicId, code), Json);

        var first = await Read<ScanResult>(await Scan("td0001"));
        Assert.Equal(("TD0001", true, false, false, "Tắt đèn"), (first.Scan.Barcode, first.Scan.Registered, first.Scan.WrongStore, first.AlreadyScanned, first.Scan.Title));
        Assert.True((await Read<ScanResult>(await Scan("TD0001"))).AlreadyScanned);
        var wrongStore = await Read<ScanResult>(await Scan(elsewhere.Barcode));
        Assert.True(wrongStore.Scan.WrongStore);
        var unknown = await Read<ScanResult>(await Scan("LA-123"));
        Assert.False(unknown.Scan.Registered);
        Assert.True((await Read<ScanResult>(await Scan("TD0005"))).Scan.Abnormal); // đã báo mất nhưng vẫn trên giá

        // Excel: mã thứ hai + mã đã quét; mã kho sai → lỗi cả file.
        var bad = await staff.PostAsync(U($"/api/inventories/ImportScans/{inventory.PublicId}"), FileForm(Workbook(["Số ĐKCB", "Mã kho"], ["TD0002", "ZZ"])));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var imported = await Read<InventoryImportResult>(await staff.PostAsync(U($"/api/inventories/ImportScans/{inventory.PublicId}"),
            FileForm(Workbook(["Số ĐKCB"], ["TD0002"], ["td0001"]))));
        Assert.Equal((1, 1), (imported.Scanned, imported.AlreadyScanned));

        var summary = await Read<InventorySummary>(await staff.GetAsync(U($"/api/inventories/Summary/{inventory.PublicId}")));
        Assert.Equal(new InventorySummary(5, 1, 1, 0, 1, 1), summary); // thiếu: TD0003 (TD0004 đang mượn, TD0005 đã quét)
        var missing = await Read<CrudPage<MissingRow>>(await staff.GetAsync(U($"/api/inventories/Missing/{inventory.PublicId}")));
        Assert.Equal("TD0003", Assert.Single(missing.Items).Barcode);
        var wrong = await Read<CrudPage<InventoryScanRow>>(await staff.PostAsJsonAsync(U("/api/inventories/Scans"),
            new ScanSearch { InventoryId = inventory.PublicId, Filter = "wrong-store" }, Json));
        Assert.Equal(elsewhere.Barcode, Assert.Single(wrong.Items).Barcode);

        var report = await staff.GetAsync(U($"/api/inventories/Report/{inventory.PublicId}"));
        using (var book = new XLWorkbook(await report.Content.ReadAsStreamAsync()))
            Assert.Equal(["Tổng quan", "Chi tiết đã quét", "Chưa quét (nghi mất)"], book.Worksheets.Select(w => w.Name));

        await Read<InventoryDto>(await staff.PostAsync(U($"/api/inventories/Close/{inventory.PublicId}"), null));
        Assert.Equal("INVENTORY_CLOSED", await ErrorCode(await Scan("TD0003")));
        var update = await staff.PutAsJsonAsync(U($"/api/inventories/Update/{inventory.PublicId}"), new InventoryRequest("Đổi kho", null, other.Id), Json);
        Assert.Equal("INVENTORY_HAS_SCANS", await ErrorCode(update));
    }

    [Fact]
    public async Task Store_move_takes_items_of_the_source_store_and_completion_moves_them()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Lão Hạc");
        var from = await AddStore(staff, "NG");
        var to = await AddStore(staff, "NH");
        var items = await Items(staff, bib.Mfn, "LH", 3, from.Id);
        var outside = (await Items(staff, bib.Mfn, "LX", 1, to.Id))[0];

        Assert.Equal("MOVE_SAME_STORE", await ErrorCode(await staff.PostAsJsonAsync(U("/api/store-moves/Add"), new StoreMoveRequest(from.Id, from.Id), Json)));
        var move = await Read<StoreMoveDto>(await staff.PostAsJsonAsync(U("/api/store-moves/Add"), new StoreMoveRequest(from.Id, to.Id, "Cô Hoa"), Json));
        var candidates = await Read<CrudPage<ItemDto>>(await staff.PostAsJsonAsync(U("/api/store-moves/Candidates"),
            new TransferCandidateSearch { TransferId = move.PublicId }, Json));
        Assert.Equal(3, candidates.TotalCount);

        var added = await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/store-moves/AddLines/{move.PublicId}"),
            new AddLinesRequest([items[0].PublicId], ["LH0002", "LH0003", outside.Barcode]), Json));
        Assert.Equal(3, added.Done);
        Assert.Equal(outside.Barcode, Assert.Single(added.Skipped).Barcode);
        Assert.Equal(0, (await Read<CrudPage<ItemDto>>(await staff.PostAsJsonAsync(U("/api/store-moves/Candidates"),
            new TransferCandidateSearch { TransferId = move.PublicId }, Json))).TotalCount);
        var changeSource = await staff.PutAsJsonAsync(U($"/api/store-moves/Update/{move.PublicId}"), new StoreMoveRequest(to.Id, from.Id), Json);
        Assert.Equal("MOVE_HAS_LINES", await ErrorCode(changeSource));

        await Loan(tenantId, items[2]);
        var done = await Read<CompleteResult>(await staff.PostAsync(U($"/api/store-moves/Complete/{move.PublicId}"), null));
        Assert.Equal(2, done.Done);
        Assert.Equal("LH0003", Assert.Single(done.Skipped).Barcode);
        Assert.Equal(to.Id, (await Item(staff, items[0].PublicId)).StoreId);
        Assert.Equal(("Kho NH", 2L), (LastEvent(items[0].PublicId).StoreName, LastEvent(items[0].PublicId).Version));
        var lines = await Read<List<TransferLine>>(await staff.GetAsync(U($"/api/store-moves/Lines/{move.PublicId}")));
        Assert.NotNull(lines.Single(l => l.Barcode == "LH0003").SkipReason);

        Assert.Equal("MOVE_COMPLETED", await ErrorCode(await staff.DeleteAsync(U($"/api/store-moves/Delete/{move.PublicId}"))));
        var dto = await Read<StoreMoveDto>(await staff.GetAsync(U($"/api/store-moves/GetById/{move.PublicId}")));
        Assert.Equal((2, 3), (dto.MovedCount, dto.LineCount));
    }

    [Fact]
    public async Task Delivery_lines_are_unique_and_signing_shelves_unshelved_items_into_the_store()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Đất rừng phương Nam");
        var store = await AddStore(staff, "KM");
        var items = await Items(staff, bib.Mfn, "DR", 3, shelve: false);
        await Read<ShelveResult>(await staff.PostAsJsonAsync(U("/api/items/Shelve"), new ShelveRequest(Barcodes: ["DR0003"]), Json));

        var delivery = await Read<DeliveryDto>(await staff.PostAsJsonAsync(U("/api/deliveries/Add"),
            new DeliveryRequest("Tổ bổ sung", ReceiverName: "Thủ kho", StoreId: store.Id), Json));
        var candidates = await Read<CrudPage<ItemDto>>(await staff.PostAsJsonAsync(U("/api/deliveries/Candidates"),
            new TransferCandidateSearch { TransferId = delivery.PublicId, Keyword = "DR" }, Json));
        Assert.Equal(2, candidates.TotalCount); // DR0003 đã xếp giá
        var added = await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/deliveries/AddLines/{delivery.PublicId}"),
            new AddLinesRequest(Barcodes: ["DR0001", "DR0002", "DR0003"]), Json));
        Assert.Equal((2, "DR0003"), (added.Done, Assert.Single(added.Skipped).Barcode));

        var second = await Read<DeliveryDto>(await staff.PostAsJsonAsync(U("/api/deliveries/Add"), new DeliveryRequest("Khác"), Json));
        var dupe = await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/deliveries/AddLines/{second.PublicId}"),
            new AddLinesRequest(Barcodes: ["DR0001"]), Json));
        Assert.Equal(0, dupe.Done);

        Assert.Equal("DELIVERY_NOT_RECEIVED", await ErrorCode(await staff.PostAsync(U($"/api/deliveries/Sign/{delivery.PublicId}"), null)));
        await Read<DeliveryDto>(await staff.PutAsJsonAsync(U($"/api/deliveries/Update/{delivery.PublicId}"),
            new DeliveryRequest("Tổ bổ sung", ReceiverName: "Thủ kho", StoreId: store.Id, Status: 2), Json));
        var signed = await Read<CompleteResult>(await staff.PostAsync(U($"/api/deliveries/Sign/{delivery.PublicId}"), null));
        Assert.Equal(2, signed.Done);
        var item = await Item(staff, items[0].PublicId);
        Assert.Equal(("R", store.Id), (item.Status, item.StoreId));
        Assert.Equal("DELIVERY_SIGNED", await ErrorCode(await staff.PutAsJsonAsync(U($"/api/deliveries/Update/{delivery.PublicId}"),
            new DeliveryRequest("Sửa"), Json)));

        // Xoá phiếu chưa ký thì bản sách chọn lại được cho phiếu khác.
        var third = await Read<DeliveryDto>(await staff.PostAsJsonAsync(U("/api/deliveries/Add"), new DeliveryRequest("Ba"), Json));
        var more = await Items(staff, bib.Mfn, "DS", 1, shelve: false);
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/deliveries/AddLines/{third.PublicId}"), new AddLinesRequest([more[0].PublicId]), Json));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/deliveries/Delete/{third.PublicId}"))).StatusCode);
        Assert.Equal(1, (await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/deliveries/AddLines/{second.PublicId}"),
            new AddLinesRequest([more[0].PublicId]), Json))).Done);
    }

    [Fact]
    public async Task Delivery_list_filters_by_code_range_people_dates_status_and_signed_state()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        async Task<DeliveryDto> AddDelivery(string deliverer, string receiver, DateOnly on, int status = 1) =>
            await Read<DeliveryDto>(await staff.PostAsJsonAsync(U("/api/deliveries/Add"),
                new DeliveryRequest(deliverer, ReceiverName: receiver, DeliveredOn: on, Status: status), Json));
        async Task<CrudPage<DeliveryDto>> Search(DeliverySearch search) =>
            await Read<CrudPage<DeliveryDto>>(await staff.PostAsJsonAsync(U("/api/deliveries/Search"), search, Json));

        var a = await AddDelivery("Tổ Bổ sung", "Kho chính", new DateOnly(2026, 9, 1));
        var b = await AddDelivery("Biên mục", "Kho phụ", new DateOnly(2026, 9, 15), status: 2);
        var c = await AddDelivery("Tổ bổ sung", "Kho phụ", new DateOnly(2026, 10, 2), status: 2);
        await Read<CompleteResult>(await staff.PostAsync(U($"/api/deliveries/Sign/{c.PublicId}"), null));

        Assert.Equal([b.Id, c.Id], (await Search(new DeliverySearch { CodeFrom = b.Id, CodeTo = c.Id })).Items.Select(x => x.Id).Order());
        Assert.Equal([a.Id, c.Id], (await Search(new DeliverySearch { DelivererName = "bổ SUNG" })).Items.Select(x => x.Id).Order());
        Assert.Equal([b.Id, c.Id], (await Search(new DeliverySearch { ReceiverName = "phụ" })).Items.Select(x => x.Id).Order());
        Assert.Equal([b.Id], (await Search(new DeliverySearch { DeliveredFrom = new DateOnly(2026, 9, 10), DeliveredTo = new DateOnly(2026, 9, 30) })).Items.Select(x => x.Id));
        // Hàng đợi "Nhận phân bổ": đã nhận đủ và chưa ký.
        Assert.Equal([b.Id], (await Search(new DeliverySearch { DeliveryStatus = 2, Confirmed = false })).Items.Select(x => x.Id));
    }

    [Fact]
    public async Task Re_register_renames_barcode_blocks_loans_and_imports_whole_file_or_nothing()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Truyện Kiều");
        var store = await AddStore(staff, "KR");
        var items = await Items(staff, bib.Mfn, "TK", 4);

        var renamed = await Read<ItemDto>(await staff.PostAsJsonAsync(U("/api/items/ReRegister"), new ReRegisterRequest("tk0001", "KIEU-001", store.Id), Json));
        Assert.Equal(("KIEU-001", store.Id), (renamed.Barcode, renamed.StoreId));
        Assert.Equal("KIEU-001", LastEvent(items[0].PublicId).Barcode);
        Assert.Equal("BARCODE_EXISTS", await ErrorCode(await staff.PostAsJsonAsync(U("/api/items/ReRegister"), new ReRegisterRequest("TK0002", "kieu-001"), Json)));
        // Đổi hoa/thường của chính nó được.
        Assert.Equal("kieu-001", (await Read<ItemDto>(await staff.PostAsJsonAsync(U("/api/items/ReRegister"), new ReRegisterRequest("KIEU-001", "kieu-001"), Json))).Barcode);
        await Loan(tenantId, items[3]);
        Assert.Equal("ITEM_ON_LOAN", await ErrorCode(await staff.PostAsJsonAsync(U("/api/items/ReRegister"), new ReRegisterRequest("TK0004", "TK9004"), Json)));

        var preview = await Read<ReRegisterImportResult>(await staff.PostAsync(U("/api/items/ReRegister/Import?dryRun=true"),
            FileForm(Workbook(["ĐKCB cũ", "ĐKCB mới", "Mã kho"], ["TK0002", "TK9002", "KR"], ["TK0003", "TK9003", ""]))));
        Assert.Equal((2, 0, true), (preview.Total, preview.Applied, preview.DryRun));
        Assert.Equal("TK0002", (await Item(staff, items[1].PublicId)).Barcode);

        var rejected = await staff.PostAsync(U("/api/items/ReRegister/Import"),
            FileForm(Workbook(["ĐKCB cũ", "ĐKCB mới"], ["TK0002", "TK9002"], ["TK0003", "TK9002"])));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("TK0002", (await Item(staff, items[1].PublicId)).Barcode);

        var applied = await Read<ReRegisterImportResult>(await staff.PostAsync(U("/api/items/ReRegister/Import"),
            FileForm(Workbook(["ĐKCB cũ", "ĐKCB mới", "Mã kho"], ["TK0002", "TK9002", "KR"], ["TK0003", "TK9003", ""]))));
        Assert.Equal(2, applied.Applied);
        var item = await Item(staff, items[1].PublicId);
        Assert.Equal(("TK9002", store.Id), (item.Barcode, item.StoreId));
    }

    [Fact]
    public async Task Statistics_group_by_status_store_ddc_and_type_and_book_report_excludes_lost()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var store = await AddStore(staff, "TK");
        var novel = await PublishBib(tenantId, "Tiểu thuyết", "895.922", 1, "Sách");
        var maths = await PublishBib(tenantId, "Toán 6", "510", 2, "Sách giáo khoa");
        var unknown = await PublishBib(tenantId, "Chưa phân loại", null, null, null);
        await Items(staff, novel.Mfn, "ST", 3, store.Id);
        await Items(staff, maths.Mfn, "SG", 2, store.Id);
        await Items(staff, unknown.Mfn, "SU", 1, shelve: false);
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U("/api/lost-books/MarkLost"), new MarkLostRequest(["ST0003"], Reason: "Kiểm kê không thấy"), Json));

        async Task<DocumentStats> Stats(string criterion) =>
            await Read<DocumentStats>(await staff.PostAsJsonAsync(U("/api/statistics/Document"), new DocumentStatsRequest(criterion), Json));

        var byStatus = await Stats("status");
        Assert.Equal((3, 6), (byStatus.Titles, byStatus.Copies));
        Assert.Equal([("I", 1), ("L", 1), ("R", 4)], byStatus.Rows.Select(r => (r.Key, r.Copies)));
        var byStore = await Stats("store");
        Assert.Contains(byStore.Rows, r => r.Label == "TK — Kho TK" && r.Copies == 5 && r.Titles == 2);
        var byDdc = await Stats("ddc");
        Assert.Equal([("5", 1, 2), ("8", 1, 3), ("", 1, 1)], byDdc.Rows.Select(r => (r.Key, r.Titles, r.Copies)));
        Assert.StartsWith("800 - Văn học", byDdc.Rows[1].Label, StringComparison.Ordinal);
        var byType = await Stats("bibtype");
        Assert.Equal(["Sách", "Sách giáo khoa", "(Chưa xác định loại)"], byType.Rows.Select(r => r.Label));
        Assert.Equal("CRITERION_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/statistics/Document"), new DocumentStatsRequest("abc"), Json)));
        var statsFile = await staff.PostAsJsonAsync(U("/api/statistics/DocumentExport"), new DocumentStatsRequest("ddc"), Json);
        Assert.Equal(CrudExcel.ContentType, statsFile.Content.Headers.ContentType?.MediaType);

        var report = await Read<StoreBookReport>(await staff.PostAsJsonAsync(U("/api/store-book-report/Search"), new StoreBookReportRequest(store.Id), Json));
        Assert.Equal((4, 2), (report.TotalCount, report.TitleCount)); // ST0003 đã mất
        Assert.Equal(4, Assert.Single(report.StoreStats).Copies);
        var file = await staff.PostAsJsonAsync(U("/api/store-book-report/Export"), new StoreBookReportRequest(), Json);
        using var book = new XLWorkbook(await file.Content.ReadAsStreamAsync());
        Assert.Equal("BÁO CÁO SÁCH TRONG KHO", book.Worksheet(1).Cell(3, 1).GetString());

        var other = Staff(NewTenant());
        Assert.Equal(0, (await Read<DocumentStats>(await other.PostAsJsonAsync(U("/api/statistics/Document"), new DocumentStatsRequest("status"), Json))).Copies);
    }
}
