using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Elib.Acquisition.Application;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;

namespace Elib.Acquisition.Tests.Api;

/// <summary>Tỷ giá / thanh toán, báo cáo bổ sung và tra cứu đơn nhận theo dòng.</summary>
public sealed class AcquisitionReportTests : IClassFixture<AcquisitionApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 13000;
    private static long _nextUserId = 23000;
    private static long _nextMfn = 5000;

    private readonly AcquisitionApiFactory _factory;

    public AcquisitionReportTests(AcquisitionApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        _factory.Licenses.Licensed.Add((tenantId, "ACQUISITION"));
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

    private async Task<BibChanged> PublishBib(long tenantId, string title, string? author = "Nam Cao", DateTimeOffset? catalogedAt = null)
    {
        var bib = new BibChanged
        {
            TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Author = author, Publisher = "NXB Văn học",
            PublishYear = "2020", Status = 2, CatalogedAt = catalogedAt,
        };
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib);
        return bib;
    }

    private static async Task<ReceiptDto> AddReceipt(HttpClient staff, ReceiptRequest request) =>
        await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), request, Json));

    private static async Task<ReceiptLineDto> AddLine(HttpClient staff, ReceiptDto receipt, ReceiptLineRequest request) =>
        await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"), request, Json));

    private static async Task Register(HttpClient staff, ReceiptLineDto line, int quantity, string prefix) =>
        await Read<List<RegisteredItem>>(await staff.PostAsJsonAsync(U($"/api/receipts/RegisterItems/{line.PublicId}"), new RegisterReceiptItemsRequest(Quantity: quantity, Prefix: prefix), Json));

    private static Task<HttpResponseMessage> Post(HttpClient staff, string report, AcquisitionReportRequest request) =>
        staff.PostAsJsonAsync(U($"/api/reports/{report}"), request, Json);

    [Fact]
    public async Task Rates_and_payment_terms_are_validated_and_totalled()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Nhật ký trong tù");
        var order = await Read<PurchaseOrderDto>(await staff.PostAsJsonAsync(U("/api/orders/Add"),
            new PurchaseOrderRequest("Đặt ngoại văn", new DateOnly(2026, 10, 1), PaymentMethod: 1), Json));
        Assert.Equal(1, order.PaymentMethod);
        Assert.Equal("PAYMENT_METHOD_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/orders/Add"), new PurchaseOrderRequest("X", PaymentMethod: 5), Json)));

        Task<HttpResponseMessage> SaveOrderLine(OrderLineRequest r) => staff.PostAsJsonAsync(U($"/api/orders/SaveLine/{order.PublicId}"), r, Json);
        var orderLine = await Read<OrderLineDto>(await SaveOrderLine(new OrderLineRequest(Mfn: bib.Mfn, Quantity: 10, Price: 12.5m, Currency: "usd", Rate: 25000m)));
        Assert.Equal((25000m, "USD"), (orderLine.Rate, orderLine.Currency));
        Assert.Equal("RATE_INVALID", await ErrorCode(await SaveOrderLine(new OrderLineRequest(LinePublicId: orderLine.PublicId, Mfn: bib.Mfn, Quantity: 10, Rate: 0m))));
        Assert.Equal(10 * 12.5m * 25000m, (await Read<PurchaseOrderDto>(await staff.GetAsync(U($"/api/orders/GetById/{order.PublicId}")))).Total);
        var noRate = await Read<OrderLineDto>(await SaveOrderLine(new OrderLineRequest(Title: "Sách tiền Việt", Quantity: 2, Price: 100m)));
        Assert.Equal(1m, noRate.Rate); // không nhập tỷ giá = 1

        var receipt = await AddReceipt(staff, new ReceiptRequest(new DateOnly(2026, 10, 10), "Nhận đợt 1", PaymentStatus: 2, PaymentMethod: 1));
        Assert.Equal((2, 1), (receipt.PaymentStatus, receipt.PaymentMethod));
        Assert.Equal("PAYMENT_STATUS_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 10, 10), PaymentStatus: 1), Json)));

        var line = await AddLine(staff, receipt, new ReceiptLineRequest(Quantity: 4, OrderLinePublicId: orderLine.PublicId));
        Assert.Equal((25000m, 12.5m, "USD"), (line.Rate, line.Price, line.Currency)); // tỷ giá, giá, tiền tệ theo dòng đặt
        Assert.Equal(4 * 12.5m * 25000m, (await Read<ReceiptDto>(await staff.GetAsync(U($"/api/receipts/GetById/{receipt.PublicId}")))).Total);
        var changed = await AddLine(staff, receipt, new ReceiptLineRequest(LinePublicId: line.PublicId, Quantity: 4, Price: 12.5m, Currency: "USD", Rate: 26000m));
        Assert.Equal(26000m, changed.Rate);
        Assert.Equal(4 * 12.5m * 26000m, (await Read<ReceiptDto>(await staff.GetAsync(U($"/api/receipts/GetById/{receipt.PublicId}")))).Total);
        Assert.Equal("RATE_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
            new ReceiptLineRequest(LinePublicId: line.PublicId, Quantity: 4, Rate: -1m), Json)));
    }

    [Fact]
    public async Task Payment_can_be_settled_after_the_receipt_is_completed_without_unlocking_it()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Truyện ngắn Nam Cao");
        var receipt = await AddReceipt(staff, new ReceiptRequest(new DateOnly(2026, 10, 12), "Nhận chờ thanh toán"));
        await AddLine(staff, receipt, new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 2, Price: 70000m));
        await Read<ReceiptDto>(await staff.PostAsync(U($"/api/receipts/Complete/{receipt.PublicId}"), null));
        var versions = _factory.PublishedOf<Elib.Contracts.Events.Acquisition.ReceiptChanged>().Count(e => e.ReceiptPublicId == receipt.PublicId);

        // Đã hoàn tất: sửa đầu phiếu bị khoá, nhưng chốt thanh toán được (quyền sửa là đủ).
        Assert.Equal("RECEIPT_COMPLETED", await ErrorCode(await staff.PutAsJsonAsync(U($"/api/receipts/Update/{receipt.PublicId}"), new ReceiptRequest(new DateOnly(2026, 10, 13)), Json)));
        var editor = Staff(tenantId, "AB_RECEIPTS:view", "AB_RECEIPTS:edit");
        var paid = await Read<ReceiptDto>(await editor.PostAsJsonAsync(U($"/api/receipts/Payment/{receipt.PublicId}"), new ReceiptPaymentRequest(2, 1), Json));
        Assert.Equal((2, 1, true), (paid.PaymentStatus, paid.PaymentMethod, paid.CompletedAt is not null));
        var reread = await Read<ReceiptDto>(await staff.GetAsync(U($"/api/receipts/GetById/{receipt.PublicId}")));
        Assert.Equal((2, 1, 140000m), (reread.PaymentStatus, reread.PaymentMethod, reread.Total));
        Assert.Equal(versions, _factory.PublishedOf<Elib.Contracts.Events.Acquisition.ReceiptChanged>().Count(e => e.ReceiptPublicId == receipt.PublicId)); // holdings không cần biết

        Assert.Equal("PAYMENT_STATUS_INVALID", await ErrorCode(await editor.PostAsJsonAsync(U($"/api/receipts/Payment/{receipt.PublicId}"), new ReceiptPaymentRequest(1, 0), Json)));
        Assert.Equal("PAYMENT_METHOD_INVALID", await ErrorCode(await editor.PostAsJsonAsync(U($"/api/receipts/Payment/{receipt.PublicId}"), new ReceiptPaymentRequest(2, 9), Json)));
        var viewer = Staff(tenantId, "AB_RECEIPTS:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(U($"/api/receipts/Payment/{receipt.PublicId}"), new ReceiptPaymentRequest(0, 0), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await editor.PostAsJsonAsync(U($"/api/receipts/Payment/{Guid.NewGuid()}"), new ReceiptPaymentRequest(2, 1), Json)).StatusCode);
    }

    [Fact]
    public async Task Reports_filter_receipts_here_and_ask_holdings_only_for_the_matching_ones()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var kieu = await PublishBib(tenantId, "Truyện Kiều", "Nguyễn Du");
        var doTo = await PublishBib(tenantId, "Số đỏ", "Vũ Trọng Phụng");
        var fahasa = await Read<SupplierDto>(await staff.PostAsJsonAsync(U("/api/suppliers/Add"), new { name = "Fahasa" }, Json));
        var phuongNam = await Read<SupplierDto>(await staff.PostAsJsonAsync(U("/api/suppliers/Add"), new { name = "Phương Nam" }, Json));
        var sept = await AddReceipt(staff, new ReceiptRequest(new DateOnly(2026, 9, 5), "Nhận tháng 9", SupplierId: fahasa.Id, StoreId: 1));
        var oct = await AddReceipt(staff, new ReceiptRequest(new DateOnly(2026, 10, 5), "Nhận tháng 10", SupplierId: phuongNam.Id, StoreId: 2));
        var septLine = await AddLine(staff, sept, new ReceiptLineRequest(Mfn: kieu.Mfn, Quantity: 2, Price: 30000m, Currency: "VND"));
        var octLine = await AddLine(staff, oct, new ReceiptLineRequest(Mfn: doTo.Mfn, Quantity: 3, Price: 2m, Currency: "USD", Rate: 25000m));
        await Register(staff, septLine, 2, "KI");
        await Register(staff, octLine, 3, "DO");

        // Danh sách tài liệu bổ sung: tổng theo toàn bộ kết quả lọc, thành tiền có tỷ giá.
        var all = await Read<AcquisitionListPage>(await Post(staff, "AcquisitionList", new AcquisitionReportRequest { PageSize = 1 }));
        Assert.Equal((2, 5, 2 * 30000m + 3 * 2m * 25000m, 1), (all.Total, all.Quantity, all.Amount, all.Items.Count));
        Assert.Equal("Số đỏ", all.Items[0].Title); // mới nhất trước
        var inOct = await Read<AcquisitionListPage>(await Post(staff, "AcquisitionList", new AcquisitionReportRequest { From = new DateOnly(2026, 10, 1), To = new DateOnly(2026, 10, 31) }));
        Assert.Equal(("Số đỏ", 150000m), (Assert.Single(inOct.Items).Title, inOct.Items[0].Total));
        Assert.Equal("Truyện Kiều", Assert.Single((await Read<AcquisitionListPage>(await Post(staff, "AcquisitionList", new AcquisitionReportRequest { SupplierId = fahasa.Id }))).Items).Title);
        Assert.Equal("Truyện Kiều", Assert.Single((await Read<AcquisitionListPage>(await Post(staff, "AcquisitionList", new AcquisitionReportRequest { Title = "KIỀU" }))).Items).Title);
        Assert.Equal("Số đỏ", Assert.Single((await Read<AcquisitionListPage>(await Post(staff, "AcquisitionList", new AcquisitionReportRequest { Author = "trọng phụng" }))).Items).Title);
        Assert.Equal((HttpStatusCode.BadRequest, "DATE_RANGE_INVALID"), await Failure(await Post(staff, "AcquisitionList",
            new AcquisitionReportRequest { From = new DateOnly(2026, 10, 2), To = new DateOnly(2026, 10, 1) })));

        // Tra cứu đơn nhận theo dòng: mô tả ISBD, nguồn, tên kho (holdings), và file Excel cùng bộ lọc.
        var lookup = await Read<CrudPage<ReceiptLookupRow>>(await Post(staff, "LookupLines", new AcquisitionReportRequest()));
        Assert.Equal(2, lookup.TotalCount);
        var row = lookup.Items.Single(r => r.ReceiptId == sept.Id);
        Assert.Equal(("Truyện Kiều / Nguyễn Du .- NXB Văn học, 2020", "Kho chính", kieu.Mfn), (row.Isbd, row.StoreName, row.Mfn));
        Assert.Equal(sept.PublicId, row.ReceiptPublicId); // cột "Mã đơn" mở chi tiết đơn
        Assert.Equal(0, (await Read<CrudPage<ReceiptLookupRow>>(await Post(staff, "LookupLines", new AcquisitionReportRequest { CreatedBy = 987654321 }))).TotalCount);
        Assert.Equal("Kho phụ", lookup.Items.Single(r => r.ReceiptId == oct.Id).StoreName);
        Assert.Equal(sept.Id, Assert.Single((await Read<CrudPage<ReceiptLookupRow>>(await Post(staff, "LookupLines",
            new AcquisitionReportRequest { ReceiptCodeFrom = sept.Id, ReceiptCodeTo = sept.Id }))).Items).ReceiptId);
        Assert.Equal(oct.Id, Assert.Single((await Read<CrudPage<ReceiptLookupRow>>(await Post(staff, "LookupLines",
            new AcquisitionReportRequest { StoreId = 2 }))).Items).ReceiptId);
        using (var book = await Workbook(await Post(staff, "LookupLinesExport", new AcquisitionReportRequest { From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 30) })))
        {
            var sheet = book.Worksheet(1);
            Assert.Equal(["STT", "MFN", "Đơn nhận", "Tài liệu", "Ngày nhận", "Ngày tạo", "Nguồn", "Kho"], Enumerable.Range(1, 8).Select(c => sheet.Cell(6, c).GetString()));
            Assert.Equal(((double)sept.Id, "Kho chính", true), (sheet.Cell(7, 3).GetDouble(), sheet.Cell(7, 8).GetString(), sheet.Cell(8, 2).IsEmpty())); // một dòng dữ liệu
        }

        // Phân bổ kho: có điều kiện theo phiếu → chỉ hỏi holdings các phiếu khớp; không → mọi bản.
        var allocated = await Read<StoreAllocationReport>(await Post(staff, "StoreAllocation", new AcquisitionReportRequest { From = new DateOnly(2026, 10, 1) }));
        Assert.Equal([oct.PublicId], _factory.Holdings.LastReport!.ReceiptPublicIds);
        Assert.Equal((3, "Kho phụ"), (allocated.Total, Assert.Single(allocated.Items).StoreName));
        var everything = await Read<StoreAllocationReport>(await Post(staff, "StoreAllocation", new AcquisitionReportRequest()));
        Assert.Null(_factory.Holdings.LastReport!.ReceiptPublicIds);
        Assert.Equal((5, 2), (everything.Total, everything.Items.Count));
        await Read<StoreAllocationReport>(await Post(staff, "StoreAllocation", new AcquisitionReportRequest { StoreId = 1 }));
        Assert.Equal(1, _factory.Holdings.LastReport!.StoreId);
        using (var book = await Workbook(await Post(staff, "StoreAllocationExport", new AcquisitionReportRequest())))
            Assert.Equal(["Tổng cộng", "5"], [book.Worksheet(1).Cell(9, 2).GetString(), book.Worksheet(1).Cell(9, 3).GetString()]);

        // Sổ ĐKCB: ngày nhận / đơn nhận theo phiếu, đơn giá theo dòng phiếu.
        var register = await Read<CrudPage<AccessionRow>>(await Post(staff, "AccessionRegister", new AcquisitionReportRequest { From = new DateOnly(2026, 10, 1), PageSize = 2 }));
        Assert.Equal((3, 2), (register.TotalCount, register.Items.Count));
        var first = register.Items[0];
        Assert.Equal((new DateOnly(2026, 10, 5), oct.Id, 2m, "USD", 25000m), (first.Date, first.ReceiptId, first.Price, first.Currency, first.Rate));
        Assert.Equal(["DO0003", "DO0004"], register.Items.Select(r => r.Barcode)); // theo số ĐKCB
        using (var book = await Workbook(await Post(staff, "AccessionRegisterExport", new AcquisitionReportRequest { From = new DateOnly(2026, 10, 1) })))
            Assert.Equal("Số ĐKCB", book.Worksheet(1).Cell(6, 2).GetString());

        // Holdings không trả lời: phân bổ kho / sổ ĐKCB báo 503, tra cứu vẫn chạy (chỉ thiếu tên kho).
        _factory.Holdings.Down = true;
        try
        {
            Assert.Equal((HttpStatusCode.ServiceUnavailable, "HOLDINGS_UNAVAILABLE"), await Failure(await Post(staff, "StoreAllocation", new AcquisitionReportRequest())));
            Assert.Equal((HttpStatusCode.ServiceUnavailable, "HOLDINGS_UNAVAILABLE"), await Failure(await Post(staff, "AccessionRegister", new AcquisitionReportRequest())));
            var degraded = await Read<CrudPage<ReceiptLookupRow>>(await Post(staff, "LookupLines", new AcquisitionReportRequest()));
            Assert.Equal((2, true), (degraded.TotalCount, degraded.Items.All(r => r.StoreName is null)));
        }
        finally
        {
            _factory.Holdings.Down = false;
        }

        // Chỉ người có quyền xem phiếu nhận.
        var orderOnly = Staff(tenantId, "AB_ORDERS:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(orderOnly, "AcquisitionList", new AcquisitionReportRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(orderOnly, "LookupLines", new AcquisitionReportRequest())).StatusCode);
        var other = Staff(NewTenant());
        Assert.Equal(0, (await Read<AcquisitionListPage>(await Post(other, "AcquisitionList", new AcquisitionReportRequest()))).Total);
    }

    [Fact]
    public async Task New_book_catalog_orders_by_cataloging_date_with_unknown_dates_last()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var old = await PublishBib(tenantId, "Sách cũ", catalogedAt: new DateTimeOffset(2026, 8, 1, 3, 0, 0, TimeSpan.Zero));
        var recent = await PublishBib(tenantId, "Sách mới", catalogedAt: new DateTimeOffset(2026, 10, 10, 3, 0, 0, TimeSpan.Zero));
        var unknown = await PublishBib(tenantId, "Sách không rõ ngày");

        var all = await Read<CrudPage<NewBookRow>>(await Post(staff, "NewBooks", new AcquisitionReportRequest()));
        Assert.Equal([recent.Mfn, old.Mfn, unknown.Mfn], all.Items.Select(b => b.Mfn));
        Assert.Equal((new DateOnly(2026, 10, 10), (DateOnly?)null), (all.Items[0].CatalogedOn, all.Items[2].CatalogedOn));
        var october = await Read<CrudPage<NewBookRow>>(await Post(staff, "NewBooks", new AcquisitionReportRequest { From = new DateOnly(2026, 10, 1), To = new DateOnly(2026, 10, 31) }));
        Assert.Equal(recent.Mfn, Assert.Single(october.Items).Mfn); // lọc theo ngày thì bỏ biểu ghi không rõ ngày
        using var book = await Workbook(await Post(staff, "NewBooksExport", new AcquisitionReportRequest { From = new DateOnly(2026, 8, 1), To = new DateOnly(2026, 8, 31) }));
        Assert.Equal("Sách cũ", book.Worksheet(1).Cell(7, 3).GetString());
    }

    // Hợp đồng JSON với holdings (/internal/tenants/{id}/receipt-items/Report): holdings có test với cùng hai chuỗi này (ReceiptReportApiTests).
    public const string WireRequest = """{"receiptPublicIds":["00000000-0000-0000-0000-000000000001"],"storeId":7,"skip":10,"take":20,"mfnFrom":5,"mfnTo":null,"title":"kieu","author":null,"publisher":null,"publishYear":null}""";
    public const string WirePage = """{"items":[{"barcode":"AA0001","mfn":12,"receiptPublicId":null,"linePublicId":null,"storeId":1,"storeName":"Kho 1","title":"T","author":null,"publisher":null,"publishYear":null,"ddc":"895","registeredOn":"2026-10-01"}],"total":1}""";

    [Fact]
    public void Holdings_report_wire_format_is_the_one_holdings_serves()
    {
        var request = new HoldingsReportRequest([Guid.Parse("00000000-0000-0000-0000-000000000001")], 7, 10, 20, MfnFrom: 5, Title: "kieu");
        Assert.Equal(WireRequest, JsonSerializer.Serialize(request, JsonSerializerOptions.Web));
        var page = JsonSerializer.Deserialize<HoldingsReportPage>(WirePage, JsonSerializerOptions.Web)!;
        var row = Assert.Single(page.Items);
        Assert.Equal((1, "AA0001", 12L, 1L, "Kho 1", "895", new DateOnly(2026, 10, 1)), (page.Total, row.Barcode, row.Mfn, row.StoreId, row.StoreName, row.Ddc, row.RegisteredOn));
    }

    private static async Task<(HttpStatusCode Status, string? Code)> Failure(HttpResponseMessage response) => (response.StatusCode, await ErrorCode(response));

    private static async Task<XLWorkbook> Workbook(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(CrudExcel.ContentType, response.Content.Headers.ContentType?.MediaType);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }
}
