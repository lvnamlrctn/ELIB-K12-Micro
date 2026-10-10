using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Search.Application;

namespace Elib.Search.Tests.Api;

public sealed class SearchApiTests(SearchApiFactory factory) : IClassFixture<SearchApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7000;
    private static long _nextUserId = 17000;
    private static long _nextMfn = 300;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "SEARCH"));
        return tenantId;
    }

    /// <summary>Bạn đọc ẩn danh trên OPAC — gateway gắn đơn vị theo host (header ký).</summary>
    private HttpClient Opac(long tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(SearchApiFactory.GatewayKey, tenantId));
        return client;
    }

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

    private static BibChanged Bib(long tenantId, string title, string? author = null, string? year = null, int status = 2, long version = 0,
        Guid? id = null, long? mfn = null, string? type = "Sách", string? ddc = null, string? keywords = null, bool deleted = false) => new()
    {
        TenantId = tenantId, BibPublicId = id ?? Guid.CreateVersion7(), Mfn = mfn ?? Interlocked.Increment(ref _nextMfn), Title = title, Author = author,
        PublishYear = year, Status = status, Version = version, BibTypeName = type, Ddc = ddc, Keywords = keywords, Language = "vie",
        Publisher = "Kim Đồng", Isbns = ["9786042000001"], Summary = "Tóm tắt " + title, Deleted = deleted,
    };

    private static ItemChanged Item(BibChanged bib, string barcode, string status = "R", string store = "Kho mở", long version = 0, Guid? id = null) => new()
    {
        TenantId = bib.TenantId, ItemPublicId = id ?? Guid.CreateVersion7(), Barcode = barcode, BibPublicId = bib.BibPublicId, Mfn = bib.Mfn,
        StoreId = 1, StoreName = store, Status = status, Version = version,
    };

    private static LoanChanged Loan(ItemChanged item, bool returned = false, long version = 0, Guid? id = null) => new()
    {
        TenantId = item.TenantId, LoanPublicId = id ?? Guid.CreateVersion7(), ReaderPublicId = Guid.CreateVersion7(), CardNo = "HS01",
        ItemPublicId = item.ItemPublicId, Barcode = item.Barcode, Mfn = item.Mfn, LoanedAt = DateTimeOffset.UtcNow,
        DueAt = DateTimeOffset.UtcNow.AddDays(14), ReturnedAt = returned ? DateTimeOffset.UtcNow : null, Version = version,
    };

    private async Task Publish(params IntegrationEvent[] events)
    {
        foreach (var e in events) await factory.PublishAsAsync(e);
        foreach (var e in events) await factory.WaitConsumedAsync(e);
    }

    private static async Task<OpacSearchResult> Search(HttpClient opac, OpacSearchRequest request) =>
        await Read<OpacSearchResult>(await opac.PostAsJsonAsync(U("/api/opac/bibs/Search"), request, Json));

    [Fact]
    public async Task Opac_finds_visible_bibs_without_diacritics_with_availability_and_facets()
    {
        var tenantId = NewTenant();
        var demen = Bib(tenantId, "Dế mèn phiêu lưu ký", "Tô Hoài", "2020", ddc: "895.9223", keywords: "Truyện thiếu nhi");
        var vochong = Bib(tenantId, "Vợ chồng A Phủ", "Tô Hoài", "2018", ddc: "895.9223");
        var hidden = Bib(tenantId, "Dế mèn bản nháp", "Tô Hoài", status: 1);
        var other = Bib(tenantId, "Số đỏ", "Vũ Trọng Phụng", "2015", type: "Báo, tạp chí");
        var i1 = Item(demen, "DM001");
        var i2 = Item(demen, "DM002", store: "Kho đóng");
        var i3 = Item(vochong, "VC001");
        var i4 = Item(vochong, "VC002", status: "L"); // mất — không hiện trên OPAC
        await Publish(demen, vochong, hidden, other);
        await Publish(i1, i2, i3, i4);
        var loan = Loan(i3);
        await Publish(loan);

        var opac = Opac(tenantId);
        var result = await Search(opac, new OpacSearchRequest { Q = "de MEN" });
        var hit = Assert.Single(result.Items);
        Assert.Equal(("Dế mèn phiêu lưu ký", 2, 2, "Sách"), (hit.Title, hit.Copies, hit.Available, hit.MaterialType));

        var byAuthor = await Search(opac, new OpacSearchRequest { Author = "to hoai", Sort = "newest" });
        Assert.Equal(["Dế mèn phiêu lưu ký", "Vợ chồng A Phủ"], byAuthor.Items.Select(i => i.Title));
        Assert.Equal(("Vợ chồng A Phủ", 1, 0), (byAuthor.Items[1].Title, byAuthor.Items[1].Copies, byAuthor.Items[1].Available));
        Assert.Equal([new FacetCount("Tô Hoài", 2)], byAuthor.Facets.Authors);
        Assert.Equal(["2020", "2018"], byAuthor.Facets.Years.Select(y => y.Key));
        Assert.Equal(1, byAuthor.Facets.AvailableCount);
        Assert.Equal([new FacetCount("Kho mở", 2), new FacetCount("Kho đóng", 1)], byAuthor.Facets.Stores);

        Assert.Equal(["Dế mèn phiêu lưu ký"], (await Search(opac, new OpacSearchRequest { Author = "to hoai", AvailableOnly = true })).Items.Select(i => i.Title));
        Assert.Equal(["Số đỏ"], (await Search(opac, new OpacSearchRequest { MaterialTypes = ["Báo, tạp chí"] })).Items.Select(i => i.Title));
        Assert.Equal(["Vợ chồng A Phủ"], (await Search(opac, new OpacSearchRequest { Barcode = "vc0" })).Items.Select(i => i.Title));
        Assert.Equal(["Dế mèn phiêu lưu ký"], (await Search(opac, new OpacSearchRequest { Keyword = "thieu nhi" })).Items.Select(i => i.Title));
        Assert.Equal(2, (await Search(opac, new OpacSearchRequest { YearFrom = 2016, YearTo = 2020 })).Total);
        Assert.Equal(0, (await Search(opac, new OpacSearchRequest { Q = "ban nhap" })).Total); // biểu ghi ẩn khỏi OPAC

        var detail = await Read<OpacBibDetail>(await opac.GetAsync(U($"/api/opac/bibs/{vochong.BibPublicId}")));
        Assert.Equal(("VC001", "on-loan", "Đang mượn"), (Assert.Single(detail.Holdings).Barcode, detail.Holdings[0].Status, detail.Holdings[0].StatusName));
        Assert.NotNull(detail.Holdings[0].DueAt);
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U($"/api/opac/bibs/{hidden.BibPublicId}"))).StatusCode);

        Assert.Equal(["Dế mèn phiêu lưu ký"], await Read<List<string>>(await opac.GetAsync(U("/api/opac/suggest?q=de%20me"))));
        Assert.Equal(["Vợ chồng A Phủ"], (await Read<List<OpacBib>>(await opac.GetAsync(U($"/api/opac/bibs/{demen.BibPublicId}/similar")))).Select(b => b.Title));

        // Trả sách → còn bản; event cũ hơn không ghi đè; xoá biểu ghi → biến mất.
        var returned = Loan(i3, returned: true, version: 1, id: loan.LoanPublicId);
        var stale = Bib(tenantId, "Tên cũ", version: -1, id: demen.BibPublicId, mfn: demen.Mfn);
        await Publish(returned, stale);
        Assert.Equal(1, (await Search(opac, new OpacSearchRequest { Q = "vo chong" })).Items[0].Available);
        Assert.Equal(1, (await Search(opac, new OpacSearchRequest { Q = "phieu luu" })).Total);
        await Publish(Bib(tenantId, other.Title, version: 1, id: other.BibPublicId, mfn: other.Mfn, deleted: true));
        Assert.Equal(0, (await Search(opac, new OpacSearchRequest { Q = "so do" })).Total);

        // Đơn vị khác không thấy.
        Assert.Equal(0, (await Search(Opac(NewTenant()), new OpacSearchRequest { Q = "de men" })).Total);
    }

    [Fact]
    public async Task Rebuild_reads_source_services_page_by_page_and_marks_missing_records_deleted()
    {
        var tenantId = NewTenant();
        var a = Bib(tenantId, "Tắt đèn", "Ngô Tất Tố", "2010");
        var b = Bib(tenantId, "Lão Hạc", "Nam Cao", "2011");
        var c = Bib(tenantId, "Chí Phèo", "Nam Cao", "2012");
        var gone = Bib(tenantId, "Đã xoá ở catalog");
        await Publish(gone); // có trong chỉ mục nhưng không còn ở catalog
        var ia = Item(a, "TD001");
        var ib = Item(b, "LH001");
        factory.Sources.Bibs[tenantId] = [a, b, c];
        factory.Sources.Items[tenantId] = [ia, ib];
        factory.Sources.Loans[tenantId] = [Loan(ib)];

        var reader = Staff(tenantId, "SEARCH_INDEX:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync(U("/api/index/Rebuild"), null)).StatusCode);
        var admin = Staff(tenantId);
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync(U("/api/index/Rebuild"), null)).StatusCode);
        var status = await SearchApiFactory.WaitForAsync(async () =>
        {
            var s = await Read<SearchIndexStatus>(await reader.GetAsync(U("/api/index/Status")));
            return s.Status == "Done" ? s : null;
        }, "dựng lại chỉ mục");
        Assert.Equal((3, 2, 1, 3, 2), (status.Bibs, status.Items, status.Loans, status.IndexedBibs, status.IndexedItems));

        var opac = Opac(tenantId);
        var namCao = await Search(opac, new OpacSearchRequest { Author = "nam cao", Sort = "oldest" });
        Assert.Equal(["Lão Hạc", "Chí Phèo"], namCao.Items.Select(i => i.Title));
        Assert.Equal((1, 0), (namCao.Items[0].Copies, namCao.Items[0].Available));
        Assert.Equal(0, (await Search(opac, new OpacSearchRequest { Q = "da xoa" })).Total);

        // Không có license SEARCH → gateway chặn; service: quản trị chỉ mục cũng cần license.
        var unlicensed = Staff(Interlocked.Increment(ref _nextTenantId));
        Assert.Equal(HttpStatusCode.Forbidden, (await unlicensed.GetAsync(U("/api/index/Status"))).StatusCode);
    }
}
