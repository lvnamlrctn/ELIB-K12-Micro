using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Digital;
using Elib.Contracts.Events.Holdings;
using Elib.Search.Application;

namespace Elib.Search.Tests.Api;

public sealed class UnifiedSearchApiTests(SearchApiFactory factory) : IClassFixture<SearchApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7800;
    private static long _nextMfn = 900;
    private static long _nextEbookId = 5000;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "SEARCH"));
        return tenantId;
    }

    private HttpClient Opac(long tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(SearchApiFactory.GatewayKey, tenantId));
        return client;
    }

    private static BibChanged Bib(long tenantId, string title, string year, string? author = null) => new()
    {
        TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = title, Author = author, PublishYear = year,
        Status = 2, BibTypeName = "Sách", Language = "vie", Publisher = "Kim Đồng", Isbns = ["9786042000001"], Summary = "Tóm tắt " + title,
    };

    private static ItemChanged Item(BibChanged bib, string barcode, string status = "R") => new()
    {
        TenantId = bib.TenantId, ItemPublicId = Guid.CreateVersion7(), Barcode = barcode, BibPublicId = bib.BibPublicId, Mfn = bib.Mfn,
        StoreId = 1, StoreName = "Kho mở", Status = status,
    };

    private static EbookChanged Ebook(long tenantId, string title, string year, string collection = "Văn học", string? author = null) => new()
    {
        TenantId = tenantId, EbookPublicId = Guid.CreateVersion7(), EbookId = Interlocked.Increment(ref _nextEbookId), Title = title, Author = author,
        PublishYear = year, Status = 2, Version = 1, Free = true, HasFile = true, CollectionName = collection, Metadata = [],
    };

    private async Task Publish(params IntegrationEvent[] events)
    {
        foreach (var e in events) await factory.PublishAsAsync(e);
        foreach (var e in events) await factory.WaitConsumedAsync(e);
    }

    private static async Task<OpacAllResult> All(HttpClient opac, OpacAllRequest request)
    {
        var response = await opac.PostAsJsonAsync(U("/api/opac/all/Search"), request, Json);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<OpacAllResult>(Json))!;
    }

    [Fact]
    public async Task One_list_mixes_print_and_digital_in_a_single_order_across_page_boundaries_with_counts_per_kind()
    {
        var tenantId = NewTenant();
        var p2001 = Bib(tenantId, "Lịch sử Việt Nam", "2001");
        var p1990 = Bib(tenantId, "Việt Nam sử lược", "1990");
        var p2015 = Bib(tenantId, "Văn hoá Việt", "2015");
        var d2010 = Ebook(tenantId, "Việt Nam văn hiến", "2010");
        var d2020 = Ebook(tenantId, "Việt Nam thế kỷ 21", "2020", "Lịch sử");
        var other = Bib(tenantId, "Toán cao cấp", "2000");
        await Publish(p2001, p1990, p2015, other, d2010, d2020);
        var opac = Opac(tenantId);

        var request = new OpacAllRequest { Q = "viet", Sort = "newest", PageSize = 2 };
        var first = await All(opac, request);
        Assert.Equal((5, 3, 2), (first.Total, first.PrintTotal, first.DigitalTotal));
        Assert.Equal(["Việt Nam thế kỷ 21", "Văn hoá Việt"], first.Items.Select(i => i.Title));
        Assert.Equal(["digital", "print"], first.Items.Select(i => i.Kind));
        var second = await All(opac, request with { Page = 2 });
        Assert.Equal(["Việt Nam văn hiến", "Lịch sử Việt Nam"], second.Items.Select(i => i.Title));
        var third = await All(opac, request with { Page = 3 });
        Assert.Equal(["Việt Nam sử lược"], third.Items.Select(i => i.Title));

        // Lọc loại: tổng theo loại đang chọn, số trên chip vẫn là của cả hai loại.
        var printOnly = await All(opac, request with { Kind = "print", PageSize = 10 });
        Assert.Equal((3, 3, 2), (printOnly.Total, printOnly.PrintTotal, printOnly.DigitalTotal));
        Assert.All(printOnly.Items, i => Assert.Equal("print", i.Kind));
        var digitalOnly = await All(opac, request with { Kind = "digital", PageSize = 10 });
        Assert.Equal(["Việt Nam thế kỷ 21", "Việt Nam văn hiến"], digitalOnly.Items.Select(i => i.Title));
        Assert.Equal((true, true), (digitalOnly.Items[0].Free, digitalOnly.Items[0].HasFile));

        var title = await All(opac, new OpacAllRequest { Q = "viet", Sort = "title", PageSize = 10 });
        Assert.Equal(["Lịch sử Việt Nam", "Văn hoá Việt", "Việt Nam sử lược", "Việt Nam thế kỷ 21", "Việt Nam văn hiến"], title.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Collection_filter_keeps_only_digital_of_that_collection_and_lists_collections_found_by_the_query()
    {
        var tenantId = NewTenant();
        await Publish(Bib(tenantId, "Truyện ngắn chọn lọc", "2000"), Ebook(tenantId, "Truyện Kiều", "1999"), Ebook(tenantId, "Truyện cổ tích", "2005", "Thiếu nhi"));
        var opac = Opac(tenantId);

        var all = await All(opac, new OpacAllRequest { Q = "truyen" });
        Assert.Equal((3, 1, 2), (all.Total, all.PrintTotal, all.DigitalTotal));
        Assert.Equal(["Thiếu nhi", "Văn học"], all.Collections.Select(c => c.Key).Order());

        var kids = await All(opac, new OpacAllRequest { Q = "truyen", Collection = "Thiếu nhi" });
        Assert.Equal(1, kids.Total);
        Assert.Equal("Truyện cổ tích", Assert.Single(kids.Items).Title);
        Assert.Equal(2, kids.Collections.Count); // danh sách bộ sưu tập không bị chính bộ lọc thu hẹp
    }

    [Fact]
    public async Task Available_only_filters_print_copies_phrases_must_match_and_other_tenants_are_not_seen()
    {
        var tenantId = NewTenant();
        var shelved = Bib(tenantId, "Đắc nhân tâm", "2010");
        var lost = Bib(tenantId, "Đắc nhân tâm bản cũ", "1980");
        await Publish(shelved, lost, Item(shelved, "DK-1"), Item(lost, "DK-2", status: "L"), Ebook(tenantId, "Đắc nhân tâm điện tử", "2018"));
        var opac = Opac(tenantId);

        var available = await All(opac, new OpacAllRequest { Q = "dac nhan tam", AvailableOnly = true });
        Assert.Equal((2, 1, 1), (available.Total, available.PrintTotal, available.DigitalTotal));
        Assert.Equal((1, 1), (available.Items.Single(i => i.Kind == "print").Copies, available.Items.Single(i => i.Kind == "print").Available));

        Assert.Equal(1, (await All(opac, new OpacAllRequest { Q = "\"nhan tam dien\"" })).Total);
        Assert.Equal(0, (await All(opac, new OpacAllRequest { Q = "\"tam nhan\"" })).Total);
        Assert.Equal(0, (await All(Opac(NewTenant()), new OpacAllRequest { Q = "nhan tam" })).Total);
        Assert.Equal(3, (await All(opac, new OpacAllRequest { Q = "nhan tam", PageSize = 500, Page = 99 })).Total); // trang quá sâu bị chặn, không lỗi
    }
}
