using System.Globalization;
using System.Xml.Linq;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Catalog;

namespace Elib.Search.Tests.Api;

/// <summary>OAI-PMH 2.0 (monolith/ELIB-LRC: OaiPmhController) — chỉ "printbook" ở GĐ1 (search chưa có ebook).</summary>
public sealed class OaiPmhApiTests(SearchApiFactory factory) : IClassFixture<SearchApiFactory>, IAsyncLifetime
{
    private static long _nextTenantId = 27000;
    private static long _nextMfn = 9000;

    private static readonly XNamespace Oai = "http://www.openarchives.org/OAI/2.0/";
    private static readonly XNamespace OaiDc = "http://www.openarchives.org/OAI/2.0/oai_dc/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "SEARCH"));
        return tenantId;
    }

    /// <summary>Bạn đọc/hệ ngoài ẩn danh trên OPAC — gateway gắn đơn vị theo host (header ký, như SearchApiTests.Opac).</summary>
    private HttpClient Opac(long tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(SearchApiFactory.GatewayKey, tenantId));
        return client;
    }

    private static BibChanged Bib(long tenantId, string title, int status = 2, bool deleted = false, long version = 0, long? mfn = null) => new()
    {
        TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = mfn ?? Interlocked.Increment(ref _nextMfn), Title = title, Author = "Tô Hoài",
        Publisher = "Kim Đồng", PublishYear = "2020", Language = "vie", Ddc = "895.9223", Keywords = "văn học thiếu nhi", Status = status, Version = version,
        Deleted = deleted,
    };

    private async Task Publish(params BibChanged[] events)
    {
        foreach (var e in events) await factory.PublishAsAsync(e);
        foreach (var e in events) await factory.WaitConsumedAsync(e);
    }

    private static async Task<XDocument> Oai2Pmh(HttpClient opac, string query)
    {
        var response = await opac.GetAsync(new Uri($"/api/opac/oai?{query}", UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return XDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string? ErrorCode(XDocument doc) => doc.Root?.Element(Oai + "error")?.Attribute("code")?.Value;

    [Fact]
    public async Task Identify_and_catalog_metadata_describe_the_repository()
    {
        var tenantId = NewTenant();
        var opac = Opac(tenantId);

        var identify = await Oai2Pmh(opac, "verb=Identify");
        var root = identify.Root!.Element(Oai + "Identify")!;
        Assert.Equal("ELIB", root.Element(Oai + "repositoryName")?.Value);
        Assert.Equal("2.0", root.Element(Oai + "protocolVersion")?.Value);
        Assert.Equal("transient", root.Element(Oai + "deletedRecord")?.Value);
        Assert.NotNull(root.Element(Oai + "earliestDatestamp")?.Value);

        var formats = await Oai2Pmh(opac, "verb=ListMetadataFormats");
        var prefixes = formats.Root!.Element(Oai + "ListMetadataFormats")!.Elements(Oai + "metadataFormat")
            .Select(f => f.Element(Oai + "metadataPrefix")?.Value).ToList();
        Assert.Equal(["oai_dc", "marc21"], prefixes);

        var sets = await Oai2Pmh(opac, "verb=ListSets");
        Assert.Equal("printbook", sets.Root!.Element(Oai + "ListSets")!.Element(Oai + "set")!.Element(Oai + "setSpec")?.Value);
    }

    [Fact]
    public async Task GetRecord_returns_dublin_core_and_marc21_for_a_visible_bib_and_errors_otherwise()
    {
        var tenantId = NewTenant();
        var opac = Opac(tenantId);
        var bib = Bib(tenantId, "Dế Mèn phiêu lưu ký");
        await Publish(bib);
        var identifier = $"oai:printbook:{bib.Mfn.ToString(CultureInfo.InvariantCulture)}";

        var dc = await Oai2Pmh(opac, $"verb=GetRecord&identifier={Uri.EscapeDataString(identifier)}&metadataPrefix=oai_dc");
        var dcMetadata = dc.Root!.Element(Oai + "GetRecord")!.Element(Oai + "record")!.Element(Oai + "metadata")!.Element(OaiDc + "dc")!;
        Assert.Equal("Dế Mèn phiêu lưu ký", dcMetadata.Elements(Dc + "title").Select(e => e.Value).Single());
        Assert.Equal("Tô Hoài", dcMetadata.Elements(Dc + "creator").Select(e => e.Value).Single());

        var marc = await Oai2Pmh(opac, $"verb=GetRecord&identifier={Uri.EscapeDataString(identifier)}&metadataPrefix=marc21");
        var marcNs = XNamespace.Get("http://www.loc.gov/MARC21/slim");
        var record = marc.Root!.Element(Oai + "GetRecord")!.Element(Oai + "record")!.Element(Oai + "metadata")!.Element(marcNs + "record")!;
        Assert.Equal("Dế Mèn phiêu lưu ký",
            record.Elements(marcNs + "datafield").Single(d => d.Attribute("tag")?.Value == "245").Element(marcNs + "subfield")?.Value);

        // Thiếu identifier/metadataPrefix, prefix lạ, bản ghi không tồn tại.
        Assert.Equal("badArgument", ErrorCode(await Oai2Pmh(opac, "verb=GetRecord")));
        Assert.Equal("cannotDisseminateFormat", ErrorCode(await Oai2Pmh(opac, $"verb=GetRecord&identifier={Uri.EscapeDataString(identifier)}&metadataPrefix=rdf")));
        Assert.Equal("idDoesNotExist", ErrorCode(await Oai2Pmh(opac, $"verb=GetRecord&identifier={Uri.EscapeDataString("oai:printbook:999999")}&metadataPrefix=oai_dc")));
    }

    [Fact]
    public async Task GetRecord_reports_a_hidden_or_deleted_bib_as_a_tombstone_without_metadata()
    {
        var tenantId = NewTenant();
        var opac = Opac(tenantId);
        var hidden = Bib(tenantId, "Chưa duyệt xuất bản", status: 1);
        var removed = Bib(tenantId, "Đã gỡ bỏ", deleted: true);
        await Publish(hidden, removed);

        foreach (var bib in new[] { hidden, removed })
        {
            var bibId = Uri.EscapeDataString($"oai:printbook:{bib.Mfn.ToString(CultureInfo.InvariantCulture)}");
            var doc = await Oai2Pmh(opac, $"verb=GetRecord&identifier={bibId}&metadataPrefix=oai_dc");
            var record = doc.Root!.Element(Oai + "GetRecord")!.Element(Oai + "record")!;
            Assert.Equal("deleted", record.Element(Oai + "header")!.Attribute("status")?.Value);
            Assert.Null(record.Element(Oai + "metadata"));
        }
    }

    [Fact]
    public async Task ListRecords_pages_through_a_resumption_token()
    {
        var tenantId = NewTenant();
        var opac = Opac(tenantId);
        await Publish(Bib(tenantId, "Tập 1"), Bib(tenantId, "Tập 2"), Bib(tenantId, "Tập 3"));

        var first = await Oai2Pmh(opac, "verb=ListRecords&metadataPrefix=oai_dc");
        var firstEl = first.Root!.Element(Oai + "ListRecords")!;
        var firstRecords = firstEl.Elements(Oai + "record").ToList();
        Assert.Equal(2, firstRecords.Count); // OaiPmh:PageSize=2 trong SearchApiFactory
        var token = firstEl.Element(Oai + "resumptionToken")!;
        Assert.Equal("3", token.Attribute("completeListSize")?.Value);
        Assert.Equal("0", token.Attribute("cursor")?.Value);
        Assert.False(string.IsNullOrWhiteSpace(token.Value));

        var second = await Oai2Pmh(opac, $"verb=ListRecords&resumptionToken={Uri.EscapeDataString(token.Value)}");
        var secondEl = second.Root!.Element(Oai + "ListRecords")!;
        Assert.Single(secondEl.Elements(Oai + "record"));
        var secondToken = secondEl.Element(Oai + "resumptionToken")!;
        Assert.True(string.IsNullOrEmpty(secondToken.Value)); // hết danh sách — token rỗng báo kết thúc

        // Token hỏng / hết hạn.
        Assert.Equal("badResumptionToken", ErrorCode(await Oai2Pmh(opac, "verb=ListIdentifiers&resumptionToken=not-base64!!")));
    }

    [Fact]
    public async Task ListIdentifiers_without_matching_records_or_verb_reports_oai_errors()
    {
        var tenantId = NewTenant();
        var opac = Opac(tenantId);

        Assert.Equal("noRecordsMatch", ErrorCode(await Oai2Pmh(opac, "verb=ListIdentifiers&metadataPrefix=oai_dc")));
        Assert.Equal("badArgument", ErrorCode(await Oai2Pmh(opac, "verb=ListIdentifiers")));
        Assert.Equal("badVerb", ErrorCode(await Oai2Pmh(opac, "")));
        Assert.Equal("badVerb", ErrorCode(await Oai2Pmh(opac, "verb=Frobnicate")));
    }
}
