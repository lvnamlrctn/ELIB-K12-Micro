using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;
using Elib.Contracts.Events.Catalog;
using Elib.Contracts.Events.Platform;

namespace Elib.Catalog.Tests.Api;

public sealed class CatalogApiTests : IClassFixture<CatalogApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7000;
    private static long _nextUserId = 17000;

    private readonly CatalogApiFactory _factory;

    public CatalogApiTests(CatalogApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant(bool licensed = true)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        if (licensed) _factory.Licenses.Licensed.Add((tenantId, "CATALOG"));
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

    private static MarcField Data(string tag, string ind1, string ind2, params (string Code, string Value)[] subfields) =>
        new(tag, ind1, ind2, Subfields: [.. subfields.Select(s => new MarcSubfield(s.Code, s.Value))]);

    private static Task<HttpResponseMessage> AddBib(HttpClient client, BibRequest request) =>
        client.PostAsJsonAsync(U("/api/bibs/Add"), request, Json);

    private static BibRequest Book(string title, string? isbn = null, long? typeId = null) => new(typeId,
    [
        .. isbn is null ? Array.Empty<MarcField>() : [Data("020", " ", " ", ("a", isbn))],
        Data("100", "0", " ", ("a", "Nguyễn Du")),
        Data("245", "1", "0", ("a", title + " /"), ("c", "Nguyễn Du")),
        Data("260", " ", " ", ("a", "H. :"), ("b", "Văn học,"), ("c", "2019")),
    ]);

    private static async Task<BibTypeDto> AddType(HttpClient staff, string code, string recordType, string bibLevel) =>
        await Read<BibTypeDto>(await staff.PostAsJsonAsync(U("/api/bib-types/Add"), new BibTypeRequest("Loại " + code, code, recordType, bibLevel), Json));

    [Fact]
    public async Task New_tenant_gets_default_bib_types_and_worksheets_from_provisioning()
    {
        var tenantId = NewTenant();
        await _factory.PublishAsync(new TenantProvisioned
        {
            TenantId = tenantId, Code = "C" + tenantId, Name = "Trường " + tenantId, Subdomain = "c" + tenantId, TimeZone = "Asia/Ho_Chi_Minh", Modules = [],
        });

        await CatalogApiFactory.WaitForAsync(() => Task.FromResult(_factory.PublishedOf<TenantSeeded>().FirstOrDefault(s => s.TenantId == tenantId)), "TenantSeeded");
        var seeded = Assert.Single(_factory.PublishedOf<TenantSeeded>(), s => s.TenantId == tenantId);
        Assert.True(seeded.Succeeded, seeded.Error);
        Assert.Equal("catalog", seeded.Service);

        var staff = Staff(tenantId);
        var types = await Read<IReadOnlyList<BibTypeDto>>(await staff.PostAsJsonAsync(U("/api/bib-types/SearchAll"), new CrudSearch(), Json));
        Assert.Equal(BibTypeResource.Defaults.Select(d => d.Code).Order(), types.Select(t => t.Code).Order());
        var periodical = Assert.Single(types, t => t.Code == "TAPCHI");
        Assert.Equal("s", periodical.BibLevel);

        var sheets = await Read<IReadOnlyList<WorksheetDto>>(await staff.GetAsync(U($"/api/worksheets/GetByBibType/{types.Single(t => t.Code == "SACH").Id}")));
        var book = Assert.Single(sheets);
        Assert.Contains(book.Fields, f => f.Tag == "245" && f.Subfields!.Select(s => s.Code).SequenceEqual(["a", "b", "c"]));
        Assert.Contains(book.Fields, f => f.Tag == "041" && f.Subfields![0].Value == "vie");

        // Biểu ghi mới của đơn vị này có 003 = mã đơn vị (từ bản sao đơn vị).
        var bib = await Read<BibDto>(await AddBib(staff, Book("Truyện Kiều")));
        Assert.Equal("C" + tenantId, Assert.Single(bib.Fields, f => f.Tag == "003").Value);

        // Chạy lại không thêm trùng.
        var again = await staff.PostAsync(U("/api/bib-types/RestoreDefaults"), null);
        Assert.Equal(0, JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement.GetProperty("added").GetInt32());
    }

    [Fact]
    public async Task Saving_marc_normalizes_fields_builds_control_fields_and_publishes_bib_changed()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var serial = await AddType(staff, "TC", "a", "s");

        var request = new BibRequest(serial.Id,
        [
            new MarcField("001", Value: "9999"), // hệ thống tự quản — bị bỏ
            Data("650", "#", "7", ("A", "Toán học"), ("x", "")),
            Data("245", "1", "0", ("a", "Toán tuổi thơ :"), ("b", "tạp chí")),
            Data("020", "", "", ("a", "978-604-0-00000-1 (bìa mềm)")),
            Data("260", " ", " ", ("c", "c2021.")),
            Data("500", " ", " ", ("a", "")), // trường rỗng — bị bỏ
            Data("650", " ", "7", ("a", "Tạp chí")),
        ]);
        var created = await Read<BibDto>(await AddBib(staff, request));

        Assert.True(created.Mfn > 0);
        Assert.Equal(created.Id, created.Mfn);
        Assert.Equal("Toán tuổi thơ tạp chí", created.Title);
        Assert.Equal("2021", created.PublishYear);
        Assert.Equal(["9786040000001"], created.Isbns);
        Assert.Equal("Toán học; Tạp chí", created.Keywords);
        Assert.Equal('s', created.Leader[7]);
        Assert.Equal(24, created.Leader.Length);
        Assert.Equal(["001", "005", "008", "020", "245", "260", "650", "650"], created.Fields.Select(f => f.Tag));
        Assert.Equal(created.Mfn.ToString(System.Globalization.CultureInfo.InvariantCulture), created.Fields[0].Value);
        Assert.Equal("2021", created.Fields.Single(f => f.Tag == "008").Value![7..11]);
        var subject = created.Fields.First(f => f.Tag == "650");
        Assert.Equal((" ", "7"), (subject.Ind1, subject.Ind2));
        Assert.Equal([new MarcSubfield("a", "Toán học")], subject.Subfields);

        // Sửa: đổi nhan đề, ẩn khỏi OPAC, xoá.
        var updated = await Read<BibDto>(await staff.PutAsJsonAsync(U($"/api/bibs/Update/{created.PublicId}"),
            request with { Fields = [.. request.Fields.Where(f => f.Tag != "245"), Data("245", "1", "0", ("a", "Toán tuổi thơ 2"))], Status = 1 }, Json));
        Assert.Equal(("Toán tuổi thơ 2", 1), (updated.Title, updated.Status));
        Assert.Equal(created.Mfn, (await Read<BibDto>(await staff.GetAsync(U($"/api/bibs/GetByMfn/{created.Mfn}")))).Mfn);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/bibs/Delete/{created.PublicId}"))).StatusCode);

        var events = _factory.PublishedOf<BibChanged>().Where(e => e.BibPublicId == created.PublicId).ToList();
        Assert.Equal(3, events.Count);
        Assert.All(events, e => Assert.Equal((tenantId, created.Mfn), (e.TenantId, e.Mfn)));
        Assert.Equal(["Toán tuổi thơ tạp chí", "Toán tuổi thơ 2", "Toán tuổi thơ 2"], events.Select(e => e.Title));
        Assert.True(events.Select(e => e.Version).Distinct().Count() == 3 && events.Select(e => e.Version).SequenceEqual(events.Select(e => e.Version).Order()));
        Assert.Equal([false, false, true], events.Select(e => e.Deleted));
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U($"/api/bibs/GetByMfn/{created.Mfn}"))).StatusCode);
    }

    [Fact]
    public async Task Invalid_marc_is_rejected_with_clear_codes()
    {
        var staff = Staff(NewTenant());
        Assert.Equal("MARC_TITLE_REQUIRED", await ErrorCode(await AddBib(staff, new BibRequest(null, [Data("100", "0", " ", ("a", "Ai đó"))]))));
        Assert.Equal("MARC_TAG_INVALID", await ErrorCode(await AddBib(staff, new BibRequest(null, [Data("24A", " ", " ", ("a", "x"))]))));
        Assert.Equal("MARC_SUBFIELD_INVALID", await ErrorCode(await AddBib(staff, new BibRequest(null, [Data("245", " ", " ", ("$", "x"))]))));
        Assert.Equal("MARC_INDICATOR_INVALID", await ErrorCode(await AddBib(staff, new BibRequest(null, [Data("245", "10", " ", ("a", "x"))]))));
        Assert.Equal("BIB_TYPE_NOT_FOUND", await ErrorCode(await AddBib(staff, Book("X", typeId: 987654))));
    }

    [Fact]
    public async Task Search_ignores_diacritics_and_isbn_check_finds_duplicates_within_tenant_only()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var kieu = await Read<BibDto>(await AddBib(staff, Book("Truyện Kiều", "8935236401234")));
        await Read<BibDto>(await AddBib(staff, Book("Lục Vân Tiên")));

        async Task<string[]> Find(BibSearch s) =>
            [.. (await Read<CrudPage<BibDto>>(await staff.PostAsJsonAsync(U("/api/bibs/Search"), s, Json))).Items.Select(b => b.Title).Order()];

        Assert.Equal(["Truyện Kiều"], await Find(new BibSearch { Keyword = "truyen kieu" }));
        Assert.Equal(["Lục Vân Tiên", "Truyện Kiều"], await Find(new BibSearch { Keyword = "NGUYEN DU" }));
        Assert.Equal(["Truyện Kiều"], await Find(new BibSearch { Isbn = "893-5236-40123-4" }));
        Assert.Equal(["Lục Vân Tiên", "Truyện Kiều"], await Find(new BibSearch { Keyword = "van hoc" }));

        var dupes = await Read<IReadOnlyList<IsbnMatch>>(await staff.GetAsync(U("/api/bibs/CheckIsbn?isbn=8935236401234")));
        Assert.Equal(kieu.Mfn, Assert.Single(dupes).Mfn);
        Assert.Empty(await Read<IReadOnlyList<IsbnMatch>>(await staff.GetAsync(U($"/api/bibs/CheckIsbn?isbn=8935236401234&excludePublicId={kieu.PublicId}"))));

        var other = Staff(NewTenant());
        Assert.Empty(await Read<IReadOnlyList<IsbnMatch>>(await other.GetAsync(U("/api/bibs/CheckIsbn?isbn=8935236401234"))));
        Assert.Equal(0, (await Read<CrudPage<BibDto>>(await other.PostAsJsonAsync(U("/api/bibs/Search"), new BibSearch { Keyword = "kieu" }, Json))).TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(U($"/api/bibs/GetByMfn/{kieu.Mfn}"))).StatusCode);
    }

    [Fact]
    public async Task Worksheets_keep_empty_template_fields_and_bib_type_in_use_cannot_be_deleted()
    {
        var staff = Staff(NewTenant());
        var type = await AddType(staff, "SACH", "a", "m");
        var sheet = await Read<WorksheetDto>(await staff.PostAsJsonAsync(U("/api/worksheets/Add"),
            new WorksheetRequest("Sách tham khảo", type.Id, [Data("245", "1", "0", ("a", ""), ("c", "")), Data("041", "0", " ", ("a", "vie"))]), Json));
        Assert.Equal(["245", "041"], sheet.Fields.Select(f => f.Tag)); // giữ thứ tự người dùng sắp
        Assert.Equal(["a", "c"], sheet.Fields[0].Subfields!.Select(s => s.Code));

        Assert.Equal("WORKSHEET_EMPTY", await ErrorCode(await staff.PostAsJsonAsync(U("/api/worksheets/Add"), new WorksheetRequest("Rỗng", null, []), Json)));

        var delete = await staff.DeleteAsync(U($"/api/bib-types/Delete/{type.PublicId}"));
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("BIB_TYPE_IN_USE", await ErrorCode(delete));
        Assert.Equal("BIB_TYPE_CODE_EXISTS", await ErrorCode(await staff.PostAsJsonAsync(U("/api/bib-types/Add"), new BibTypeRequest("Khác", "sach"), Json)));
        Assert.Equal("BIB_TYPE_LEADER_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/bib-types/Add"), new BibTypeRequest("Sai", "SAI", "z", "m"), Json)));

        var fields = await Read<IReadOnlyList<MarcFieldDefinition>>(await staff.GetAsync(U("/api/marc21/fields")));
        Assert.Contains(fields, f => f.Tag == "245" && f.Subfields.Any(s => s.Code == "a" && s.Name == "Nhan đề chính"));
    }

    [Fact]
    public async Task Unlicensed_tenant_and_missing_permissions_are_rejected()
    {
        var unlicensed = Staff(NewTenant(licensed: false));
        var blocked = await unlicensed.PostAsJsonAsync(U("/api/bibs/Search"), new BibSearch(), Json);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("MODULE_NOT_LICENSED", await ErrorCode(blocked));
        Assert.Equal(HttpStatusCode.Forbidden, (await unlicensed.GetAsync(U("/api/marc21/fields"))).StatusCode);

        var tenantId = NewTenant();
        var viewer = Staff(tenantId, "CATALOG_BIBS:view");
        Assert.Equal(HttpStatusCode.OK, (await viewer.PostAsJsonAsync(U("/api/bibs/Search"), new BibSearch(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AddBib(viewer, Book("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(U("/api/worksheets/Search"), new WorksheetSearch(), Json)).StatusCode);
    }
}
