using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Application;

namespace Elib.Tenant.Tests.Api;

public sealed class ImportApiTests : IClassFixture<TenantApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextUserId = 8000;

    private readonly TenantApiFactory _factory;

    public ImportApiTests(TenantApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<TenantDto> NewTenantAsync()
    {
        var code = "I" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return await Read<TenantDto>(await _factory.CreateClient().WithClaims(TestClaims.SuperAdmin)
            .PostAsJsonAsync("/api/tenants", new CreateTenantRequest(code, "Trường " + code, code.ToLowerInvariant(), null, null, []), Json));
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    /// <summary>File .xlsx: dòng đầu là tiêu đề.</summary>
    private static byte[] Xlsx(params string?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } value) sheet.Cell(r + 1, c + 1).Value = value;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, string path, byte[] content, string fileName = "data.xlsx")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(CrudExcel.ContentType);
        form.Add(file, "file", fileName);
        return client.PostAsync(U(path), form);
    }

    private static async Task<IReadOnlyList<NamedItemDto>> Search(HttpClient client, string resource, string keyword) =>
        (await Read<CrudPage<NamedItemDto>>(await client.PostAsJsonAsync($"/api/{resource}/Search", new CrudSearch { Keyword = keyword, PageSize = 100 }))).Items;

    [Fact]
    public async Task Template_is_an_xlsx_with_the_column_headers()
    {
        var tenant = await NewTenantAsync();
        var response = await Staff(tenant.Id).GetAsync(U("/api/ethnicities/ImportTemplate"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CrudExcel.ContentType, response.Content.Headers.ContentType?.MediaType);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Tên *", workbook.Worksheet(1).Cell(1, 1).GetString());

        using var orgs = new XLWorkbook(await (await Staff(tenant.Id).GetAsync(U("/api/orgs/ImportTemplate"))).Content.ReadAsStreamAsync());
        Assert.Equal("Thứ tự", orgs.Worksheet(1).Cell(1, 2).GetString());
    }

    [Fact]
    public async Task Import_adds_all_rows_and_records_one_audit_entry()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);
        var tag = Guid.NewGuid().ToString("N")[..6];

        // Tiêu đề kiểu monolith ("Name"), có dòng trống ở giữa và khoảng trắng thừa.
        var result = await Read<CrudImportResult>(await Upload(staff, "/api/nationalities/Import",
            Xlsx(["Name"], [$"Xứ A {tag}"], [null], [$"  Xứ B {tag}  "])));

        Assert.Equal(2, result.Imported);
        Assert.Empty(result.Errors);
        Assert.Equal([$"Xứ A {tag}", $"Xứ B {tag}"], (await Search(staff, "nationalities", tag)).Select(x => x.Name));
        Assert.Contains(_factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenant.Id && a.Action == "IMPORT" && a.Summary!.Contains("2 bản ghi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Any_bad_row_rejects_the_whole_file_with_row_numbers()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);
        var tag = Guid.NewGuid().ToString("N")[..6];

        // "Kinh" có sẵn trong danh mục mặc định; dòng 4 trùng dòng 2 trong cùng file.
        var response = await Upload(staff, "/api/ethnicities/Import", Xlsx(["Tên dân tộc *", "Ghi chú"], [$"Mới {tag}", "x"], ["Kinh", null], [$"Mới {tag}", null]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CrudImportResult>(Json))!;
        Assert.Equal(0, result.Imported);
        Assert.Equal([3, 4], result.Errors.Select(e => e.Row));
        Assert.Contains("đã có", result.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("dòng lỗi", JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Empty(await Search(staff, "ethnicities", tag)); // không ghi dòng hợp lệ nào

        // Chọn bỏ qua dòng đã có → nhập dòng mới, bỏ qua 2 dòng trùng.
        var skipped = await Read<CrudImportResult>(await Upload(staff, "/api/ethnicities/Import?skipDuplicates=true",
            Xlsx(["Tên"], [$"Mới {tag}"], ["Kinh"], [$"Mới {tag}"])));
        Assert.Equal((1, 2), (skipped.Imported, skipped.Skipped));
        Assert.Single(await Search(staff, "ethnicities", tag));
    }

    [Fact]
    public async Task Org_import_reads_optional_order_and_validates_numbers()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var bad = await Upload(staff, "/api/orgs/Import", Xlsx(["Tên phòng ban", "Thứ tự"], [$"Tổ Toán {tag}", "hai"]));
        var error = Assert.Single((await bad.Content.ReadFromJsonAsync<CrudImportResult>(Json))!.Errors);
        Assert.Equal(2, error.Row);
        Assert.Contains("số nguyên", error.Message, StringComparison.Ordinal);

        Assert.Equal(2, (await Read<CrudImportResult>(await Upload(staff, "/api/orgs/Import",
            Xlsx(["Ten phong ban", "Thu tu"], [$"Tổ Toán {tag}", "2"], [$"Tổ Văn {tag}", null])))).Imported);
        var orgs = await Read<IReadOnlyList<OrgDto>>(await staff.PostAsJsonAsync("/api/orgs/SearchAll", new OrgSearch { Keyword = tag }));
        Assert.Equal([0, 2], orgs.Select(o => o.SortOrder).Order());
        Assert.All(orgs, o => Assert.Null(o.ParentId));
    }

    [Fact]
    public async Task Bad_files_and_missing_permission_are_refused()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);

        async Task<string?> Code(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

        Assert.Equal("IMPORT_FILE_INVALID", await Code(await Upload(staff, "/api/degrees/Import", "Name\nThạc sĩ"u8.ToArray(), "data.csv")));
        Assert.Equal("IMPORT_COLUMN_MISSING", await Code(await Upload(staff, "/api/degrees/Import", Xlsx(["Mã"], ["X"]))));
        Assert.Equal("IMPORT_NO_DATA", await Code(await Upload(staff, "/api/degrees/Import", Xlsx(["Tên"]))));

        var viewer = Staff(tenant.Id, "DEGREES:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(viewer, "/api/degrees/Import", Xlsx(["Tên"], ["Tiến sĩ khoa học"]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(U("/api/degrees/ImportTemplate"))).StatusCode);

        // Danh mục không khai báo import thì không có endpoint.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Upload(staff, "/api/currencies/Import", Xlsx(["Tên"], ["X"]))).StatusCode);
    }
}
