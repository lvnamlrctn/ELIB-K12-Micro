using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Acquisition;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Holdings;
using Elib.Holdings.Application;

namespace Elib.Holdings.Tests.Api;

/// <summary>ĐKCB theo phiếu nhận (acquisition gọi /internal), giao nhận theo đơn nhận, báo cáo kho theo ngày nhận.</summary>
public sealed class ReceiptItemsApiTests : IClassFixture<HoldingsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 9500;
    private static long _nextUserId = 19500;
    private static long _nextMfn = 7000;
    private static long _nextCode = 100;

    private readonly HoldingsApiFactory _factory;

    public ReceiptItemsApiTests(HoldingsApiFactory factory) => _factory = factory;

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

    private static ReceiptChanged Receipt(long tenantId, BibChanged bib, int quantity, DateOnly? receivedOn = null, bool completed = false, long version = 1) => new()
    {
        TenantId = tenantId, ReceiptPublicId = Guid.CreateVersion7(), Code = Interlocked.Increment(ref _nextCode), Name = "Mua đợt 1",
        ReceivedOn = receivedOn ?? new DateOnly(2026, 9, 5), Completed = completed, Version = version,
        Lines = [new ReceiptLineState { LinePublicId = Guid.CreateVersion7(), BibPublicId = bib.BibPublicId, Mfn = bib.Mfn, Quantity = quantity }],
    };

    private Task<HttpResponseMessage> Register(long tenantId, ReceiptChanged receipt, int quantity = 0, string? barcode = null, string prefix = "BS") =>
        Service().PostAsJsonAsync(U($"/internal/tenants/{tenantId}/receipt-items/Register"),
            new ReceiptItemsRequest(receipt, receipt.Lines[0].LinePublicId, quantity, prefix, 4, Barcode: barcode), Json);

    [Fact]
    public async Task Registration_is_capped_at_the_line_quantity_and_refused_once_the_receipt_is_completed()
    {
        var tenantId = NewTenant();
        var bib = await PublishBib(tenantId, "Lão Hạc");
        var receipt = Receipt(tenantId, bib, quantity: 3);

        var batch = await Read<List<ItemDto>>(await Register(tenantId, receipt, quantity: 2));
        Assert.Equal(["BS0001", "BS0002"], batch.Select(i => i.Barcode));
        Assert.All(batch, i => Assert.Equal((receipt.ReceiptPublicId, receipt.Code, "I"), (i.ReceiptPublicId, i.ReceiptCode, i.Status)));

        var over = await Register(tenantId, receipt, quantity: 2);
        Assert.Equal("RECEIPT_LINE_FULL", await ErrorCode(over));
        Assert.Contains("còn 1", await over.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("BS-LE-01", (await Read<List<ItemDto>>(await Register(tenantId, receipt, barcode: "BS-LE-01")))[0].Barcode);
        Assert.Equal("RECEIPT_LINE_FULL", await ErrorCode(await Register(tenantId, receipt, barcode: "BS-LE-02")));

        // Danh sách theo phiếu (acquisition đếm số đã đăng ký) và lọc ở màn tìm tài liệu.
        var listed = await Read<List<ReceiptItemDto>>(await Service().GetAsync(U($"/internal/tenants/{tenantId}/receipt-items/{receipt.ReceiptPublicId}")));
        Assert.Equal(3, listed.Count(i => i.LinePublicId == receipt.Lines[0].LinePublicId));
        var staff = Staff(tenantId);
        var page = await Read<CrudPage<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), new ItemSearch { ReceiptPublicId = receipt.ReceiptPublicId }, Json));
        Assert.Equal(3, page.TotalCount);

        // Hoàn tất (trạng thái mới hơn) → khoá; trạng thái cũ hơn tới sau không mở lại.
        var completed = receipt with { Completed = true, Version = 5, Lines = [receipt.Lines[0] with { Quantity = 10 }] };
        Assert.Equal("RECEIPT_COMPLETED", await ErrorCode(await Register(tenantId, completed, quantity: 1)));
        Assert.Equal("RECEIPT_COMPLETED", await ErrorCode(await Register(tenantId, receipt with { Lines = completed.Lines }, quantity: 1)));

        // Chỉ service gọi được.
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenantId}/receipt-items/{receipt.ReceiptPublicId}"))).StatusCode);
    }

    [Fact]
    public async Task Concurrent_registrations_never_exceed_the_line_quantity()
    {
        var tenantId = NewTenant();
        var bib = await PublishBib(tenantId, "Chí Phèo");
        var receipt = Receipt(tenantId, bib, quantity: 2);
        await Read<List<ItemDto>>(await Register(tenantId, receipt, barcode: "CP-SEED")); // bản sao phiếu đã có — hai phiên dưới cùng tranh một dòng

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Register(tenantId, receipt, quantity: 1, prefix: "CP")));
        var listed = await Read<List<ReceiptItemDto>>(await Service().GetAsync(U($"/internal/tenants/{tenantId}/receipt-items/{receipt.ReceiptPublicId}")));
        Assert.Equal(2, listed.Count);
        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        foreach (var failed in results.Where(r => !r.IsSuccessStatusCode))
            // Thua cuộc tuỳ thời điểm: thấy dòng đã đủ, thấy ĐKCB phiên kia vừa lấy, hoặc bị khoá lạc quan chặn lúc ghi.
            Assert.True(await ErrorCode(failed) is "RECEIPT_LINE_FULL" or "BARCODE_EXISTS" or "RECEIPT_BUSY", await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Remove_only_unshelved_items_not_in_a_delivery_and_deliveries_pick_by_receipt()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Vợ nhặt");
        var receipt = Receipt(tenantId, bib, quantity: 4);
        var items = await Read<List<ItemDto>>(await Register(tenantId, receipt, quantity: 4, prefix: "VN"));
        var direct = await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(bib.Mfn, 1, "TT", 4), Json));

        // Phiếu nhận chưa hoàn tất chưa hiện ở ô chọn đơn nhận của giao nhận.
        Assert.DoesNotContain(await Read<List<DeliveryReceiptOption>>(await staff.GetAsync(U("/api/deliveries/Receipts"))), r => r.PublicId == receipt.ReceiptPublicId);
        var done = receipt with { Completed = true, Version = 2 };
        await _factory.PublishAsync(done);
        await _factory.WaitConsumedAsync(done);
        var option = Assert.Single(await Read<List<DeliveryReceiptOption>>(await staff.GetAsync(U("/api/deliveries/Receipts"))), r => r.PublicId == receipt.ReceiptPublicId);
        Assert.Equal((receipt.Code, 4), (option.Code, option.Pending));

        var delivery = await Read<DeliveryDto>(await staff.PostAsJsonAsync(U("/api/deliveries/Add"),
            new DeliveryRequest(ReceiverName: "Kho mở", Status: 2, ReceiptPublicId: receipt.ReceiptPublicId), Json));
        Assert.Equal(receipt.Code, delivery.ReceiptCode);
        var candidates = await Read<CrudPage<ItemDto>>(await staff.PostAsJsonAsync(U("/api/deliveries/Candidates"),
            new TransferCandidateSearch { TransferId = delivery.PublicId, ReceiptPublicId = receipt.ReceiptPublicId }, Json));
        Assert.Equal(["VN0001", "VN0002", "VN0003", "VN0004"], candidates.Items.Select(i => i.Barcode)); // không lẫn bản đăng ký thẳng TT0001
        await Read<ItemBatchResult>(await staff.PostAsJsonAsync(U($"/api/deliveries/AddLines/{delivery.PublicId}"), new AddLinesRequest([items[0].PublicId]), Json));
        await Read<ShelveResult>(await staff.PostAsJsonAsync(U("/api/items/Shelve"), new ShelveRequest(Ids: [items[1].PublicId]), Json));

        Task<HttpResponseMessage> Remove(Guid item) => Service().DeleteAsync(U($"/internal/tenants/{tenantId}/receipt-items/{receipt.ReceiptPublicId}/{item}"));
        Assert.Equal("ITEM_IN_DELIVERY", await ErrorCode(await Remove(items[0].PublicId)));
        Assert.Equal("ITEM_IN_USE", await ErrorCode(await Remove(items[1].PublicId)));
        Assert.Equal(HttpStatusCode.NotFound, (await Remove(direct[0].PublicId)).StatusCode); // không thuộc phiếu
        Assert.Equal(HttpStatusCode.NoContent, (await Remove(items[2].PublicId)).StatusCode);
        Assert.Contains(_factory.PublishedOf<ItemChanged>(), e => e.ItemPublicId == items[2].PublicId && e.Deleted);

        // Ký nhận: bản của đơn nhận trong phiếu sang sẵn sàng ở kho nhận.
        var signed = await Read<CompleteResult>(await staff.PostAsync(U($"/api/deliveries/Sign/{delivery.PublicId}"), null));
        Assert.Equal(1, signed.Done);
        Assert.Equal(1, Assert.Single(await Read<List<DeliveryReceiptOption>>(await staff.GetAsync(U("/api/deliveries/Receipts"))),
            r => r.PublicId == receipt.ReceiptPublicId).Pending); // VN0004 còn chờ phân bổ
    }

    [Fact]
    public async Task Store_reports_filter_by_receipt_date_and_fall_back_to_registration_date()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await PublishBib(tenantId, "Số đỏ");
        var receipt = Receipt(tenantId, bib, quantity: 2, receivedOn: new DateOnly(2020, 1, 15));
        await Read<List<ItemDto>>(await Register(tenantId, receipt, quantity: 2, prefix: "SD"));
        await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(bib.Mfn, 1, "DT", 4), Json));
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        async Task<StoreBookReport> Report(DateOnly from, DateOnly to) =>
            await Read<StoreBookReport>(await staff.PostAsJsonAsync(U("/api/store-book-report/Search"), new StoreBookReportRequest(From: from, To: to), Json));

        var january = await Report(new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 31));
        Assert.Equal(["SD0001", "SD0002"], january.Rows.Select(r => r.Barcode).Order(StringComparer.Ordinal));
        Assert.All(january.Rows, r => Assert.Equal((new DateOnly(2020, 1, 15), receipt.Code, today), (r.ReceivedOn, r.ReceiptCode, r.RegisteredOn)));
        var recent = await Report(today.AddDays(-1), today);
        Assert.Equal(["DT0001"], recent.Rows.Select(r => r.Barcode)); // bản không có phiếu nhận: theo ngày đăng ký

        var stats = await Read<DocumentStats>(await staff.PostAsJsonAsync(U("/api/statistics/Document"),
            new DocumentStatsRequest("status", new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 31)), Json));
        Assert.Equal(2, stats.Copies);
    }
}
