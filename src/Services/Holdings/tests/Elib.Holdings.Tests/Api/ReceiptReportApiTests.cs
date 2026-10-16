using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Acquisition;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Platform;
using Elib.Holdings.Application;

namespace Elib.Holdings.Tests.Api;

/// <summary>Báo cáo bản sách theo phiếu nhận cho acquisition, và người thao tác thật của lời gọi nội bộ.</summary>
public sealed class ReceiptReportApiTests : IClassFixture<HoldingsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 9700;
    private static long _nextUserId = 19700;
    private static long _nextMfn = 7500;
    private static long _nextCode = 500;

    private readonly HoldingsApiFactory _factory;

    public ReceiptReportApiTests(HoldingsApiFactory factory) => _factory = factory;

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

    private async Task<BibChanged> PublishBib(long tenantId, string title, string? author = null)
    {
        var bib = new BibChanged
        {
            TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Author = author, Publisher = "NXB Kim Đồng",
            PublishYear = "2021", Isbns = [], Status = 2,
        };
        await _factory.PublishAsync(bib);
        await _factory.WaitConsumedAsync(bib);
        return bib;
    }

    private static ReceiptChanged Receipt(long tenantId, BibChanged bib, int quantity) => new()
    {
        TenantId = tenantId, ReceiptPublicId = Guid.CreateVersion7(), Code = Interlocked.Increment(ref _nextCode), Name = "Mua",
        ReceivedOn = new DateOnly(2026, 9, 5), Version = 1,
        Lines = [new ReceiptLineState { LinePublicId = Guid.CreateVersion7(), BibPublicId = bib.BibPublicId, Mfn = bib.Mfn, Quantity = quantity }],
    };

    private static Task<HttpResponseMessage> Register(HttpClient client, long tenantId, ReceiptChanged receipt, int quantity, string prefix, long? storeId = null) =>
        client.PostAsJsonAsync(U($"/internal/tenants/{tenantId}/receipt-items/Register"),
            new ReceiptItemsRequest(receipt, receipt.Lines[0].LinePublicId, quantity, prefix, 4, StoreId: storeId), Json);

    private static Task<HttpResponseMessage> Report(HttpClient client, long tenantId, ReceiptItemReportRequest request, string action = "Report") =>
        client.PostAsJsonAsync(U($"/internal/tenants/{tenantId}/receipt-items/{action}"), request, Json);

    [Fact]
    public async Task Report_pages_items_by_accession_number_and_filters_by_receipts_store_and_bib()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var service = Service();
        var main = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("KC", "Kho chính"), Json));
        var side = await Read<StoreDto>(await staff.PostAsJsonAsync(U("/api/stores/Add"), new StoreRequest("KP", "Kho phụ"), Json));
        var demen = await PublishBib(tenantId, "Dế mèn phiêu lưu ký", "Tô Hoài");
        var tatDen = await PublishBib(tenantId, "Tắt đèn", "Ngô Tất Tố");
        var first = Receipt(tenantId, demen, 3);
        var second = Receipt(tenantId, tatDen, 2);
        await Read<List<ItemDto>>(await Register(service, tenantId, first, 3, "AA", main.Id));
        await Read<List<ItemDto>>(await Register(service, tenantId, second, 2, "BB", side.Id));
        await Read<List<ItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Register"), new RegisterItemsRequest(demen.Mfn, 1, "ZZ", 4), Json)); // không qua phiếu nhận

        // Không chỉ định phiếu: mọi bản (kể cả bản không qua phiếu nhận), theo số ĐKCB.
        var all = await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest()));
        Assert.Equal(6, all.Total);
        Assert.Equal(["AA0001", "AA0002", "AA0003", "BB0001", "BB0002", "ZZ0001"], all.Items.Select(i => i.Barcode));
        var firstRow = all.Items[0];
        Assert.Equal(("Dế mèn phiêu lưu ký", "Tô Hoài", "NXB Kim Đồng", "Kho chính", first.ReceiptPublicId), (firstRow.Title, firstRow.Author, firstRow.Publisher, firstRow.StoreName, firstRow.ReceiptPublicId));
        Assert.Null(all.Items[5].ReceiptPublicId);

        // Chỉ các phiếu được chỉ định; danh sách rỗng = không phiếu nào khớp (không phải "tất cả").
        Assert.Equal(3, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest([first.ReceiptPublicId])))).Total);
        Assert.Equal(5, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest([first.ReceiptPublicId, second.ReceiptPublicId])))).Total);
        Assert.Equal(0, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest([])))).Total);

        // Phân trang, kho, bộ lọc biểu ghi.
        var paged = await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest(Skip: 2, Take: 2)));
        Assert.Equal(6, paged.Total);
        Assert.Equal(["AA0003", "BB0001"], paged.Items.Select(i => i.Barcode));
        Assert.Equal(2, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest(StoreId: side.Id)))).Total);
        Assert.Equal(4, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest(Title: "DẾ MÈN")))).Total); // 3 theo phiếu + 1 đăng ký thẳng
        Assert.Equal(2, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest(Author: "tất tố")))).Total);
        Assert.Equal(2, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest(MfnFrom: tatDen.Mfn, MfnTo: tatDen.Mfn)))).Total);
        Assert.Equal(6, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest(Publisher: "kim", PublishYear: "2021")))).Total);
        Assert.Equal(0, (await Read<ReceiptItemReportPage>(await Report(service, tenantId, new ReceiptItemReportRequest(PublishYear: "1999")))).Total);

        // Số bản theo kho và tên kho.
        var counts = await Read<List<ReceiptItemStoreCount>>(await Report(service, tenantId, new ReceiptItemReportRequest(), "StoreCounts"));
        Assert.Equal(["Kho chính:3", "Kho phụ:2", ":1"], counts.Select(c => $"{c.StoreName}:{c.Copies}"));
        Assert.Equal(["Kho chính", "Kho phụ"], (await Read<List<ReceiptStoreName>>(await service.GetAsync(U($"/internal/tenants/{tenantId}/receipt-items/Stores")))).Select(s => s.Name));

        // Quá nhiều phiếu → thu hẹp bộ lọc; đơn vị khác không thấy bản sách này; chỉ service gọi được.
        var tooMany = await Report(service, tenantId, new ReceiptItemReportRequest([.. Enumerable.Range(0, ReceiptItemReports.MaxReceipts + 1).Select(_ => Guid.NewGuid())]));
        Assert.Equal((HttpStatusCode.BadRequest, "REPORT_TOO_WIDE"), (tooMany.StatusCode, await ErrorCode(tooMany)));
        Assert.Equal(0, (await Read<ReceiptItemReportPage>(await Report(service, NewTenant(), new ReceiptItemReportRequest()))).Total);
        Assert.Equal(HttpStatusCode.Forbidden, (await Report(staff, tenantId, new ReceiptItemReportRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenantId}/receipt-items/Stores"))).StatusCode);
    }

    // Hợp đồng JSON với acquisition (HoldingsReportRequest / HoldingsReportPage): acquisition có test với cùng hai chuỗi này (AcquisitionReportTests).
    private const string WireRequest = """{"receiptPublicIds":["00000000-0000-0000-0000-000000000001"],"storeId":7,"skip":10,"take":20,"mfnFrom":5,"mfnTo":null,"title":"kieu","author":null,"publisher":null,"publishYear":null}""";
    private const string WirePage = """{"items":[{"barcode":"AA0001","mfn":12,"receiptPublicId":null,"linePublicId":null,"storeId":1,"storeName":"Kho 1","title":"T","author":null,"publisher":null,"publishYear":null,"ddc":"895","registeredOn":"2026-10-01"}],"total":1}""";

    [Fact]
    public void Report_wire_format_is_the_one_acquisition_sends_and_expects()
    {
        var request = JsonSerializer.Deserialize<ReceiptItemReportRequest>(WireRequest, JsonSerializerOptions.Web)!;
        Assert.Equal([Guid.Parse("00000000-0000-0000-0000-000000000001")], request.ReceiptPublicIds);
        Assert.Equal((7L, 10, 20, 5L, "kieu"), (request.StoreId, request.Skip, request.Take, request.MfnFrom, request.Title));
        var page = new ReceiptItemReportPage([new ReceiptItemReportRow("AA0001", 12, null, null, 1, "Kho 1", "T", null, null, null, "895", new DateOnly(2026, 10, 1))], 1);
        Assert.Equal(WirePage, JsonSerializer.Serialize(page, JsonSerializerOptions.Web));
    }

    [Fact]
    public async Task Registration_is_attributed_to_the_real_user_acquisition_forwards_not_the_service_account()
    {
        var tenantId = NewTenant();
        var bib = await PublishBib(tenantId, "Số đỏ");
        var receipt = Receipt(tenantId, bib, 6);

        var acting = Service();
        acting.DefaultRequestHeaders.Add(ActingAsHeaders.Actor, "staff:4321");
        acting.DefaultRequestHeaders.Add(ActingAsHeaders.Name, Uri.EscapeDataString("Nguyễn Thị Mai (mai.nt)"));
        acting.DefaultRequestHeaders.Add(ActingAsHeaders.Ip, "10.1.2.3");
        var items = await Read<List<ItemDto>>(await Register(acting, tenantId, receipt, 2, "SD"));
        var audit = _factory.PublishedOf<AuditRecorded>().Single(a => a.TenantId == tenantId && a.Action == "ADD" && a.Summary?.Contains("SD0001", StringComparison.Ordinal) == true);
        Assert.Equal((4321L, "staff", "Nguyễn Thị Mai (mai.nt)", "10.1.2.3"), (audit.Actor.Id, audit.Actor.Kind, audit.ActorName, audit.IpAddress));
        Assert.All(items, i => Assert.Equal(4321L, _factory.PublishedOf<ItemChanged>().Where(e => e.ItemPublicId == i.PublicId).MaxBy(e => e.Version)!.Actor.Id));

        // Xoá ĐKCB nhầm: cũng ghi đúng người.
        var removal = Service();
        removal.DefaultRequestHeaders.Add(ActingAsHeaders.Actor, "staff:4322");
        removal.DefaultRequestHeaders.Add(ActingAsHeaders.Name, Uri.EscapeDataString("Lê Văn Bình (binh.lv)"));
        Assert.Equal(HttpStatusCode.NoContent, (await removal.DeleteAsync(U($"/internal/tenants/{tenantId}/receipt-items/{receipt.ReceiptPublicId}/{items[0].PublicId}"))).StatusCode);
        var removed = _factory.PublishedOf<AuditRecorded>().Single(a => a.TenantId == tenantId && a.Action == "DELETE");
        Assert.Equal((4322L, "Lê Văn Bình (binh.lv)"), (removed.Actor.Id, removed.ActorName));

        // Không gửi người thao tác (job hệ thống): giữ tài khoản service. Header sai dạng / loại không hợp lệ: bỏ qua.
        await Read<List<ItemDto>>(await Register(Service(), tenantId, receipt, 1, "HT"));
        Assert.Equal("service", _factory.PublishedOf<AuditRecorded>().Single(a => a.TenantId == tenantId && a.Summary?.Contains("HT0001", StringComparison.Ordinal) == true).Actor.Kind);
        foreach (var (value, prefix) in new[] { ("admin:1", "XA"), ("staff:abc", "XB"), ("staff", "XC") })
        {
            var bogus = Service();
            bogus.DefaultRequestHeaders.Add(ActingAsHeaders.Actor, value);
            await Read<List<ItemDto>>(await Register(bogus, tenantId, receipt, 1, prefix));
            Assert.Equal("service", _factory.PublishedOf<AuditRecorded>().Single(a => a.TenantId == tenantId && a.Summary?.Contains(prefix + "0001", StringComparison.Ordinal) == true).Actor.Kind);
        }

        // Người dùng thường không giả được: endpoint nội bộ chặn từ trước khi đọc header.
        var spoof = Staff(tenantId);
        spoof.DefaultRequestHeaders.Add(ActingAsHeaders.Actor, "staff:1");
        Assert.Equal(HttpStatusCode.Forbidden, (await Register(spoof, tenantId, receipt, 1, "SP")).StatusCode);
    }
}
