using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Search.Application;
using Elib.Search.Domain;
using Elib.Search.Infrastructure.Z3950;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Elib.Search.Tests.Api;

/// <summary>Tra cứu liên thư viện: cấu hình máy chủ, tra Z39.50 thật qua TCP (máy chủ giả), OPAC chỉ thấy máy chủ được bật.</summary>
public sealed class Z3950ApiTests(SearchApiFactory factory) : IClassFixture<SearchApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7300;
    private static long _nextUserId = 17300;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "SEARCH"));
        return tenantId;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private HttpClient Opac(long tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(SearchApiFactory.GatewayKey, tenantId));
        return client;
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    [Fact]
    public async Task Staff_searches_z3950_server_page_by_page_and_opac_sees_only_enabled_servers()
    {
        await using var remote = new FakeZ3950Server(
            FakeZ3950Server.Iso2709("Truyện Kiều", "Nguyễn Du", "9786042000011"),
            FakeZ3950Server.Iso2709("Truyện Kiều (chú giải)", "Nguyễn Du"),
            FakeZ3950Server.Iso2709("Kiều truyện", "Nguyễn Du"));
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var server = await Read<Z3950ServerDto>(await staff.PostAsJsonAsync(U("/api/z3950-servers/Add"),
            new Z3950ServerRequest("Thư viện Quốc gia (thử)", "127.0.0.1", remote.Port, "VNLIB", null, "elib", "bi-mat"), Json));
        Assert.Equal(("USMARC", true, false), (server.RecordSyntax, server.HasPassword, server.ShowOnOpac));
        Assert.DoesNotContain("bi-mat", await (await staff.GetAsync(U($"/api/z3950-servers/GetById/{server.PublicId}"))).Content.ReadAsStringAsync());
        var test = await Read<Z3950TestResult>(await staff.PostAsync(U($"/api/z3950-servers/{server.PublicId}/Test"), null));
        Assert.True(test.Ok, test.Error);

        var page1 = Assert.Single(await Read<List<Z3950ServerResult>>(await staff.PostAsJsonAsync(U("/api/z3950/Search"),
            new Z3950SearchRequest { Title = "Truyện Kiều", PageSize = 2 }, Json)));
        Assert.Equal((true, 3, null), (page1.Connected, page1.Total, page1.Error));
        Assert.Equal([(1, "Truyện Kiều"), (2, "Truyện Kiều (chú giải)")], page1.Hits.Select(h => (h.Position, h.Title)));
        Assert.Equal(("Nguyễn Du", "Văn học", "2019", "9786042000011"), (page1.Hits[0].Author, page1.Hits[0].Publisher, page1.Hits[0].Year, page1.Hits[0].Isbn));
        Assert.Contains(page1.Hits[0].Record.Fields, f => f.Tag == "245" && f.Subfields![0].Value == "Truyện Kiều /");
        var searchPdu = remote.Requests.First(p => p[0] == 0xB6);
        Assert.Contains("Truyện Kiều", Encoding.UTF8.GetString(searchPdu), StringComparison.Ordinal); // câu tìm UTF-8 trong RPN
        Assert.Contains("VNLIB", Encoding.ASCII.GetString(searchPdu), StringComparison.Ordinal);
        var initPdu = remote.Requests.First(p => p[0] == 0xB4);
        Assert.Contains("bi-mat", Encoding.UTF8.GetString(initPdu), StringComparison.Ordinal); // idPass

        var page2 = Assert.Single(await Read<List<Z3950ServerResult>>(await staff.PostAsJsonAsync(U("/api/z3950/Search"),
            new Z3950SearchRequest { Title = "Truyện Kiều", PageSize = 2, Page = 2, ServerIds = [server.PublicId] }, Json)));
        Assert.Equal([(3, "Kiều truyện")], page2.Hits.Select(h => (h.Position, h.Title)));

        // Cán bộ biên mục (không có quyền cấu hình) vẫn tra được; không có quyền nào → 403.
        Assert.True((await Staff(tenantId, "CATALOG_BIBS:add").PostAsJsonAsync(U("/api/z3950/Search"), new Z3950SearchRequest { Author = "x" }, Json)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Staff(tenantId, "SEARCH_STATS:view").PostAsJsonAsync(U("/api/z3950/Search"), new Z3950SearchRequest { Author = "x" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(U("/api/z3950/Search"), new Z3950SearchRequest(), Json)).StatusCode);

        // OPAC: máy chủ chưa bật "hiện trên OPAC" → không thấy; bật (giữ mật khẩu cũ khi không gửi) → tra được.
        var opac = Opac(tenantId);
        Assert.Empty(await Read<List<Z3950ServerOption>>(await opac.GetAsync(U("/api/opac/z3950/servers"))));
        Assert.Equal(HttpStatusCode.BadRequest, (await opac.PostAsJsonAsync(U("/api/opac/z3950/Search"), new Z3950SearchRequest { Title = "kieu" }, Json)).StatusCode);
        var updated = await Read<Z3950ServerDto>(await staff.PutAsJsonAsync(U($"/api/z3950-servers/Update/{server.PublicId}"),
            new Z3950ServerRequest("Thư viện Quốc gia (thử)", "127.0.0.1", remote.Port, "VNLIB", "MARC21", "elib", null, ShowOnOpac: true), Json));
        Assert.True(updated.HasPassword);
        Assert.Equal("Thư viện Quốc gia (thử)", Assert.Single(await Read<List<Z3950ServerOption>>(await opac.GetAsync(U("/api/opac/z3950/servers")))).Name);
        Assert.Equal(3, Assert.Single(await Read<List<Z3950ServerResult>>(await opac.PostAsJsonAsync(U("/api/opac/z3950/Search"),
            new Z3950SearchRequest { Title = "Truyện Kiều" }, Json))).Total);
        Assert.Empty(await Read<List<Z3950ServerOption>>(await Opac(NewTenant()).GetAsync(U("/api/opac/z3950/servers")))); // đơn vị khác

        // Máy chủ từ chối phiên / không chạy → báo lỗi trong kết quả, không lỗi 500.
        remote.RejectInit = true;
        var rejected = Assert.Single(await Read<List<Z3950ServerResult>>(await staff.PostAsJsonAsync(U("/api/z3950/Search"), new Z3950SearchRequest { Isbn = "978-604" }, Json)));
        Assert.False(rejected.Connected);
        Assert.Contains("từ chối", rejected.Error, StringComparison.Ordinal);
        var closedPort = await Read<Z3950ServerDto>(await staff.PostAsJsonAsync(U("/api/z3950-servers/Add"),
            new Z3950ServerRequest("Máy chủ tắt", "127.0.0.1", 1, "X"), Json));
        Assert.False((await Read<Z3950TestResult>(await staff.PostAsync(U($"/api/z3950-servers/{closedPort.PublicId}/Test"), null))).Ok);
    }

    [Fact]
    public async Task Server_config_is_validated()
    {
        var staff = Staff(NewTenant());
        foreach (var bad in new[]
        {
            new Z3950ServerRequest("A", "http://lx2.loc.gov", 210, "LCDB"),
            new Z3950ServerRequest("A", "lx2.loc.gov:210", 210, "LCDB"),
            new Z3950ServerRequest("A", "lx2.loc.gov", 0, "LCDB"),
            new Z3950ServerRequest("A", "lx2.loc.gov", 210, "LC DB"),
            new Z3950ServerRequest("A", "lx2.loc.gov", 210, "LCDB", "XML"),
            new Z3950ServerRequest("A", "lx2.loc.gov", 210, "LCDB", SruUrl: "ftp://x"),
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(U("/api/z3950-servers/Add"), bad, Json)).StatusCode);
        Assert.True((await staff.PostAsJsonAsync(U("/api/z3950-servers/Add"), new Z3950ServerRequest("LOC", "LX2.loc.gov", 210, "LCDB"), Json)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await staff.PostAsJsonAsync(U("/api/z3950-servers/Add"), new Z3950ServerRequest("LOC", "z3950.loc.gov", 7090, "Voyager"), Json)).StatusCode);
    }

    [Fact]
    public async Task Private_network_addresses_are_blocked_unless_allowed()
    {
        var client = new Z3950Client(null!, Options.Create(new Z3950Options { AllowPrivateNetworks = false, TimeoutSeconds = 3 }), NullLogger<Z3950Client>.Instance);
        var result = await client.SearchAsync(new Z3950Target("127.0.0.1", 5432, "x", "USMARC", null, null, null), [new Z3950Term("title", "a")], 1, 1, default);
        Assert.False(result.Connected);
        Assert.Contains("mạng nội bộ", result.Error, StringComparison.Ordinal);

        foreach (var ip in new[] { "10.1.2.3", "172.20.0.5", "192.168.1.1", "127.0.0.1", "169.254.169.254", "100.64.0.1", "::1", "fd00::1", "fe80::1", "::ffff:10.0.0.1" })
            Assert.False(Z3950Client.IsPublic(IPAddress.Parse(ip)), ip);
        foreach (var ip in new[] { "140.147.249.67", "8.8.8.8", "2001:4860:4860::8888" })
            Assert.True(Z3950Client.IsPublic(IPAddress.Parse(ip)), ip);
    }

    [Fact]
    public void Reads_marcxml_from_sru_and_summarizes_unimarc()
    {
        const string xml = """
            <searchRetrieveResponse xmlns="http://www.loc.gov/zing/srw/"><numberOfRecords>1</numberOfRecords><records><record><recordData>
              <record xmlns="http://www.loc.gov/MARC21/slim">
                <leader>00000cam a2200000 a 4500</leader>
                <controlfield tag="001">123</controlfield>
                <datafield tag="200" ind1="1" ind2=" "><subfield code="a">Le petit prince</subfield><subfield code="f">Saint-Exupéry</subfield></datafield>
                <datafield tag="210" ind1=" " ind2=" "><subfield code="c">Gallimard</subfield><subfield code="d">1999</subfield></datafield>
              </record>
            </recordData></record></records></searchRetrieveResponse>
            """;
        var record = Assert.Single(Z3950Marc.ReadMarcXml(xml));
        Assert.Equal(("123", 3), (record.Fields[0].Value, record.Fields.Count));
        Assert.Equal(("Le petit prince", "Saint-Exupéry", "Gallimard", "1999", (string?)null), record.Summary(unimarc: true));
    }
}
