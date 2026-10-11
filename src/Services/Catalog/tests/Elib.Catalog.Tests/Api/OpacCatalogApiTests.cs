using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;
using Elib.Contracts.Events.Catalog;

namespace Elib.Catalog.Tests.Api;

/// <summary>Trang chi tiết OPAC: xem MARC/ISBD, tải biểu ghi (.mrc/MARCXML) — công khai theo host đơn vị; ảnh bìa do cán bộ đặt.</summary>
public sealed class OpacCatalogApiTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>, IAsyncLifetime
{
    private const string GatewayKey = "test-gateway-key-0123456789abcdef";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7700;
    private static long _nextUserId = 17700;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "CATALOG"));
        return tenantId;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    /// <summary>Bạn đọc ẩn danh — gateway gắn đơn vị theo host (header ký).</summary>
    private HttpClient Opac(long tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(GatewayKey, tenantId));
        return client;
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static MarcField Data(string tag, string ind1, string ind2, params (string Code, string Value)[] subfields) =>
        new(tag, ind1, ind2, Subfields: [.. subfields.Select(s => new MarcSubfield(s.Code, s.Value))]);

    private static readonly MarcField[] Kieu =
    [
        Data("020", " ", " ", ("a", "9786042000011"), ("c", "45000đ")),
        Data("100", "0", " ", ("a", "Nguyễn Du")),
        Data("245", "1", "0", ("a", "Truyện Kiều /"), ("c", "Nguyễn Du ; Đào Duy Anh hiệu đính")),
        Data("250", " ", " ", ("a", "Tái bản lần thứ 3")),
        Data("260", " ", " ", ("a", "H. :"), ("b", "Văn học,"), ("c", "2019")),
        Data("300", " ", " ", ("a", "250 tr. ;"), ("c", "21 cm")),
        Data("490", "0", " ", ("a", "Văn học trong nhà trường"), ("v", "T.1")),
        Data("500", " ", " ", ("a", "Đầu trang tên sách ghi: Danh tác Việt Nam")),
    ];

    [Fact]
    public async Task Opac_shows_marc_isbd_and_downloads_visible_bib_only()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await Read<BibDto>(await staff.PostAsJsonAsync(U("/api/bibs/Add"), new BibRequest(null, Kieu), Json));
        var opac = Opac(tenantId);

        var marc = await Read<OpacMarcDto>(await opac.GetAsync(U($"/api/opac/bibs/{bib.PublicId}/marc")));
        Assert.Equal(("001", bib.Mfn.ToString(CultureInfo.InvariantCulture)), (marc.Fields[0].Tag, marc.Fields[0].Value));
        Assert.Contains(marc.Fields, f => f.Tag == "245");
        Assert.Equal(
        [
            "Truyện Kiều / Nguyễn Du ; Đào Duy Anh hiệu đính. — Tái bản lần thứ 3. — H. : Văn học, 2019. — 250 tr. ; 21 cm. — (Văn học trong nhà trường ; T.1)",
            "Đầu trang tên sách ghi: Danh tác Việt Nam.",
            "ISBN 9786042000011 : 45000đ.",
        ], marc.Isbd);

        var iso = await opac.GetAsync(U($"/api/opac/bibs/{bib.PublicId}/export"));
        Assert.Equal(("application/marc", $"mfn-{bib.Mfn}.mrc"), (iso.Content.Headers.ContentType?.MediaType, iso.Content.Headers.ContentDisposition?.FileName));
        var record = Assert.Single(MarcFormats.ReadIso2709(await iso.Content.ReadAsByteArrayAsync()));
        Assert.Equal("Truyện Kiều /", record.Fields.Single(f => f.Tag == "245").Values('a').Single());
        var xml = await opac.GetAsync(U($"/api/opac/bibs/{bib.PublicId}/export?format=marcxml"));
        Assert.Equal("application/marcxml+xml", xml.Content.Headers.ContentType?.MediaType);

        // Ẩn khỏi OPAC / đơn vị khác / không có header đơn vị → không thấy.
        Assert.Equal(HttpStatusCode.NotFound, (await Opac(NewTenant()).GetAsync(U($"/api/opac/bibs/{bib.PublicId}/marc"))).StatusCode);
        Assert.True((await staff.PutAsJsonAsync(U("/api/bibs/ChangeStatus"), new { bib.PublicId, Status = 1 }, Json)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U($"/api/opac/bibs/{bib.PublicId}/marc"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U($"/api/opac/bibs/{bib.PublicId}/export"))).StatusCode);
        Assert.False((await factory.CreateClient().GetAsync(U($"/api/opac/bibs/{bib.PublicId}/marc"))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Staff_sets_cover_from_own_upload_or_https_and_looks_it_up_by_isbn()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await Read<BibDto>(await staff.PostAsJsonAsync(U("/api/bibs/Add"), new BibRequest(null, Kieu), Json));

        var upload = $"/s3/media-public/{tenantId}/bib-cover/2026/10/{Guid.NewGuid():N}.jpg";
        var set = await Read<BibDto>(await staff.PutAsJsonAsync(U("/api/bibs/Cover"), new SetCoverRequest(bib.PublicId, upload), Json));
        Assert.Equal((upload, bib.Version + 1), (set.CoverUrl, set.Version));
        Assert.Contains(factory.PublishedOf<BibChanged>(), e => e.BibPublicId == bib.PublicId && e.CoverUrl == upload && e.Version == set.Version);

        foreach (var bad in new[]
        {
            $"/s3/media-public/{tenantId + 1}/bib-cover/2026/10/{Guid.NewGuid():N}.jpg", // ảnh của đơn vị khác
            $"/s3/media-public/{tenantId}/attachment/2026/10/{Guid.NewGuid():N}.jpg",
            "http://example.com/a.jpg", "javascript:alert(1)", "data:image/png;base64,AAAA", "/api/admin/x",
        })
        {
            var response = await staff.PutAsJsonAsync(U("/api/bibs/Cover"), new SetCoverRequest(bib.PublicId, bad), Json);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, bad);
        }

        factory.Covers.Covers["9786042000011"] = "https://books.google.com/books/content?id=x&img=1";
        var found = await Read<CoverLookupResult>(await staff.GetAsync(U("/api/bibs/LookupCover?isbn=978-604-2000-01-1")));
        Assert.Equal("https://books.google.com/books/content?id=x&img=1", found.Url);
        Assert.Null((await Read<CoverLookupResult>(await staff.GetAsync(U("/api/bibs/LookupCover?isbn=9780000000002")))).Url);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.GetAsync(U("/api/bibs/LookupCover?isbn=abc"))).StatusCode);

        var cleared = await Read<BibDto>(await staff.PutAsJsonAsync(U("/api/bibs/Cover"), new SetCoverRequest(bib.PublicId, found.Url), Json));
        Assert.Equal(found.Url, cleared.CoverUrl);
        Assert.Null((await Read<BibDto>(await staff.PutAsJsonAsync(U("/api/bibs/Cover"), new SetCoverRequest(bib.PublicId, null), Json))).CoverUrl);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Staff(tenantId, "CATALOG_BIBS:view").PutAsJsonAsync(U("/api/bibs/Cover"), new SetCoverRequest(bib.PublicId, null), Json)).StatusCode);
    }
}
