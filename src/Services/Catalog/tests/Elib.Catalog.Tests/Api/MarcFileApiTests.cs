using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;
using Elib.Contracts.Events.Catalog;

namespace Elib.Catalog.Tests.Api;

/// <summary>Nhập/xuất file MARC: đưa dữ liệu biên mục sẵn có vào hệ mới, và xuất ra để chuyển hệ/sao lưu.</summary>
public sealed class MarcFileApiTests : IClassFixture<CatalogApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7500;
    private static long _nextUserId = 17500;

    private readonly CatalogApiFactory _factory;

    public MarcFileApiTests(CatalogApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        _factory.Licenses.Licensed.Add((tenantId, "CATALOG"));
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

    private static MarcField Data(string tag, string ind1, string ind2, params (string Code, string Value)[] subfields) =>
        new(tag, ind1, ind2, Subfields: [.. subfields.Select(s => new MarcSubfield(s.Code, s.Value))]);

    private static MarcFileRecord Record(string leader, string title, string? isbn = null, string author = "Nguyễn Du") => new(leader,
    [
        new MarcField("001", Value: "OLD-" + title.Length),
        .. isbn is null ? Array.Empty<MarcField>() : [Data("020", " ", " ", ("a", isbn))],
        Data("100", "0", " ", ("a", author)),
        Data("245", "1", "0", ("a", title + " /"), ("c", author)),
        Data("260", " ", " ", ("a", "H. :"), ("b", "Kim Đồng,"), ("c", "2020")),
    ]);

    private static MultipartFormDataContent Form(byte[] data, string fileName) => new() { { new ByteArrayContent(data), "file", fileName } };

    private static async Task<BibTypeDto> AddType(HttpClient staff, string code, string recordType, string bibLevel) =>
        await Read<BibTypeDto>(await staff.PostAsJsonAsync(U("/api/bib-types/Add"), new BibTypeRequest("Loại " + code, code, recordType, bibLevel), Json));

    private static async Task<List<BibDto>> All(HttpClient staff) =>
        await Read<List<BibDto>>(await staff.PostAsJsonAsync(U("/api/bibs/SearchAll"), new BibSearch(), Json));

    [Fact]
    public async Task Import_rejects_whole_file_on_invalid_record_unless_asked_to_skip_and_skips_duplicates()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var book = await AddType(staff, "SACH", "a", "m");
        var map = await AddType(staff, "BANDO", "e", "m");
        await Read<BibDto>(await staff.PostAsJsonAsync(U("/api/bibs/Add"), new BibRequest(book.Id,
            [Data("020", " ", " ", ("a", "9786042000011")), Data("245", "1", "0", ("a", "Đã có sẵn"))]), Json));

        var file = MarcFormats.WriteIso2709(
        [
            Record("00000nam a2200000 a 4500", "Truyện Kiều", "978-604-2-00001-1"),     // trùng ISBN biểu ghi đã có
            Record("00000nem a2200000 a 4500", "Bản đồ hành chính Việt Nam"),           // Leader/06 = e → loại Bản đồ
            new("00000nam a2200000 a 4500", [Data("100", "0", " ", ("a", "Vô danh"))]), // thiếu 245
            Record("00000nam a2200000 a 4500", "Dế mèn phiêu lưu ký", "8935244812345", "Tô Hoài"),
            Record("00000nam a2200000 a 4500", "Dế mèn phiêu lưu ký", "8935244812345", "Tô Hoài"), // trùng trong file
        ]);

        var rejected = await staff.PostAsync(U("/api/bibs/ImportMarc"), Form(file, "du-lieu-cu.mrc"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var failed = (await rejected.Content.ReadFromJsonAsync<MarcImportResult>(Json))!;
        Assert.Equal((5, 0, 1), (failed.Total, failed.Imported, failed.Failed));
        var error = Assert.Single(failed.Errors);
        Assert.Equal(3, error.Record);
        Assert.Contains("245", error.Message, StringComparison.Ordinal);
        Assert.Single(await All(staff)); // không ghi gì

        var result = await Read<MarcImportResult>(await staff.PostAsync(U("/api/bibs/ImportMarc?skipInvalid=true&status=1"), Form(file, "du-lieu-cu.mrc")));
        Assert.Equal((5, 2, 2, 1), (result.Total, result.Imported, result.Skipped, result.Failed));

        var bibs = await All(staff);
        Assert.Equal(3, bibs.Count);
        var mapBib = Assert.Single(bibs, b => b.Title == "Bản đồ hành chính Việt Nam");
        Assert.Equal(map.Id, mapBib.BibTypeId);
        Assert.Equal('e', mapBib.Leader[6]);
        Assert.Equal(1, mapBib.Status);
        Assert.Equal(mapBib.Mfn.ToString(System.Globalization.CultureInfo.InvariantCulture), mapBib.Fields.Single(f => f.Tag == "001").Value); // 001 cũ bị thay bằng MFN mới
        Assert.Contains(mapBib.Fields, f => f.Tag == "008");
        var kid = Assert.Single(bibs, b => b.Author == "Tô Hoài");
        Assert.Equal(book.Id, kid.BibTypeId);

        // Mỗi biểu ghi nhập vào phát BibChanged có MFN để holdings/search dựng bản sao.
        await CatalogApiFactory.WaitForAsync(() => Task.FromResult(_factory.PublishedOf<BibChanged>().FirstOrDefault(e => e.BibPublicId == kid.PublicId)), "BibChanged");
        var changed = _factory.PublishedOf<BibChanged>().Single(e => e.BibPublicId == kid.PublicId);
        Assert.Equal((kid.Mfn, tenantId), (changed.Mfn, changed.TenantId));

        // Nhập lại cùng file: mọi biểu ghi hợp lệ đều đã có.
        var again = await Read<MarcImportResult>(await staff.PostAsync(U("/api/bibs/ImportMarc?skipInvalid=true"), Form(file, "du-lieu-cu.mrc")));
        Assert.Equal((0, 4), (again.Imported, again.Skipped));
    }

    [Fact]
    public async Task Export_round_trips_through_marcxml_and_iso2709()
    {
        var staff = Staff(NewTenant());
        var xml = new MemoryStream();
        MarcFormats.WriteMarcXml(xml, [Record("00000nam a2200000 a 4500", "Tắt đèn", author: "Ngô Tất Tố"), Record("00000nam a2200000 a 4500", "Số đỏ", author: "Vũ Trọng Phụng")]);
        var imported = await Read<MarcImportResult>(await staff.PostAsync(U("/api/bibs/ImportMarc"), Form(xml.ToArray(), "export.xml")));
        Assert.Equal(2, imported.Imported);
        var bibs = await All(staff);
        var target = bibs.Single(b => b.Author == "Ngô Tất Tố");

        var response = await staff.PostAsJsonAsync(U("/api/bibs/ExportMarc"), new MarcExportRequest(Ids: [target.PublicId], Format: "marcxml"), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/marcxml+xml", response.Content.Headers.ContentType?.MediaType);
        var record = Assert.Single(MarcFormats.ReadMarcXml(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal(target.Mfn.ToString(System.Globalization.CultureInfo.InvariantCulture), record.Fields[0].Value);
        Assert.Contains(record.Fields, f => f.Tag == "005");
        Assert.Equal("Tắt đèn /", record.Fields.Single(f => f.Tag == "245").Subfields![0].Value);

        var iso = await staff.PostAsJsonAsync(U("/api/bibs/ExportMarc"), new MarcExportRequest(new BibSearch { Keyword = "so do" }), Json);
        var exported = Assert.Single(MarcFormats.ReadIso2709(await iso.Content.ReadAsByteArrayAsync()));
        Assert.Equal("Số đỏ /", exported.Fields.Single(f => f.Tag == "245").Subfields![0].Value);

        var empty = await staff.PostAsJsonAsync(U("/api/bibs/ExportMarc"), new MarcExportRequest(new BibSearch { Keyword = "khong co" }), Json);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Preview_reads_file_without_saving_and_requires_add_or_edit()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var serial = await AddType(staff, "TAPCHI", "a", "s");
        var text = "Ldr\n\n00000nas a2200000 a 4500\n245\n10\n$aTạp chí Toán tuổi thơ\n900\n\nKHO1\n";

        var preview = await Read<MarcFilePreview>(await staff.PostAsync(U("/api/bibs/PreviewMarc"), Form(System.Text.Encoding.UTF8.GetBytes(text), "rc_1.txt")));
        var record = Assert.Single(preview.Records);
        Assert.Equal(("Tạp chí Toán tuổi thơ", serial.Id, (string?)null), (record.Title, record.BibTypeId, record.Error));
        Assert.Equal(["245"], record.Fields.Select(f => f.Tag));
        Assert.Empty(await All(staff));

        var viewer = Staff(tenantId, "CATALOG_BIBS:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(U("/api/bibs/PreviewMarc"), Form([1, 2, 3], "a.mrc"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(U("/api/bibs/ImportMarc"), Form([1, 2, 3], "a.mrc"))).StatusCode);

        var garbage = await staff.PostAsync(U("/api/bibs/PreviewMarc"), Form("không phải MARC"u8.ToArray(), "a.txt"));
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);
    }
}
