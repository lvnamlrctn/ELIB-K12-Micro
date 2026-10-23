using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

/// <summary>Xuất Excel/metadata và nhập Excel/Dublin Core XML của tài liệu số (như ELIB-LRC).</summary>
public sealed class ItemsImportExportApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static byte[] Workbook(string[] headers, params string?[][] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Sheet1");
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } v) sheet.Cell(r + 2, c + 1).Value = v;
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent Form(string name, string fileName, byte[] content, Dictionary<string, string>? fields = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(content), name, fileName } };
        foreach (var (key, value) in fields ?? []) form.Add(new StringContent(value), key);
        return form;
    }

    private static async Task<CrudPage<EbookItemDto>> All(HttpClient staff) =>
        await Read<CrudPage<EbookItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), new EbookItemSearch { PageSize = 100 }, Json));

    [Fact]
    public async Task Export_writes_the_chosen_columns_in_the_import_header_names_and_respects_the_selection()
    {
        var staff = Staff(NewTenant());
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Văn học"), Json));
        await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Tắt đèn", Author: "Ngô Tất Tố", CollectionId: collection.Id, Free: true, PublishDate: "1939"), Json);
        var other = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Số đỏ", Author: "Vũ Trọng Phụng"), Json));

        var fields = await Read<List<CrudExportField>>(await staff.GetAsync(U("/api/items/GetExportFields")));
        Assert.Contains(fields, f => f is { Code: "title", Name: "Tiêu đề" });

        var response = await staff.PostAsJsonAsync(U("/api/items/Export"), new ItemExportRequest(Fields: ["title", "author", "free", "collection"]), Json);
        Assert.Equal(CrudExcel.ContentType, response.Content.Headers.ContentType?.MediaType);
        using var book = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = book.Worksheet(1);
        Assert.Equal(["STT", "Tiêu đề", "Tác giả", "Miễn phí", "Bộ sưu tập"], Enumerable.Range(1, 5).Select(c => sheet.Cell(1, c).GetString()));
        Assert.Equal(["Số đỏ", "Tắt đèn"], new[] { sheet.Cell(2, 2).GetString(), sheet.Cell(3, 2).GetString() }); // mới nhất trước
        Assert.Equal(("Có", "Văn học"), (sheet.Cell(3, 4).GetString(), sheet.Cell(3, 5).GetString()));

        // Chỉ xuất các dòng đã chọn.
        var picked = await staff.PostAsJsonAsync(U("/api/items/Export"), new ItemExportRequest(new ItemSelection([other.PublicId]), ["title"]), Json);
        using var pickedBook = new XLWorkbook(await picked.Content.ReadAsStreamAsync());
        Assert.Equal(2, pickedBook.Worksheet(1).LastRowUsed()!.RowNumber());

        Assert.Equal("EXPORT_FIELD_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/items/Export"), new ItemExportRequest(Fields: ["khong-co"]), Json))).Code);
    }

    [Fact]
    public async Task An_exported_file_can_be_imported_back()
    {
        var staff = Staff(NewTenant());
        await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Dế Mèn", Author: "Tô Hoài; Khác", Keyword: "thiếu nhi; phiêu lưu", Language: "vi", Free: true), Json);
        var exported = await (await staff.PostAsJsonAsync(U("/api/items/Export"), new ItemExportRequest(), Json)).Content.ReadAsByteArrayAsync();

        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportExcel"), Form("file", "xuat.xlsx", exported,
            new() { ["published"] = "true" })));

        Assert.Equal((1, 0), (result.Imported, result.Failed));
        var copy = (await All(staff)).Items[0]; // mới nhất
        Assert.Equal(("Dế Mèn", "Tô Hoài; Khác", "thiếu nhi; phiêu lưu", true), (copy.Title, copy.Author, copy.Keyword, copy.Free));
        Assert.Equal(2, copy.Authors.Count);
    }

    [Fact]
    public async Task Excel_import_applies_defaults_reports_bad_rows_and_still_imports_the_others()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Mặc định"), Json));
        var file = Workbook(["Tiêu đề", "Tác giả", "Năm xuất bản", "Ngôn ngữ", "Miễn phí", "Ảnh bìa"],
            ["Sách 1", "A; B", "2020", "English", "Không", "https://nguoi-la.example/anh.png"],
            ["Sách lỗi ngôn ngữ", null, null, "Klingon", null, null],
            [null, "Không tiêu đề", null, null, null, null],
            ["Sách 4", null, "2021", null, null, null]);

        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportExcel"), Form("file", "a.xlsx", file,
            new() { ["collectionId"] = collection.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ["free"] = "true", ["allowDownload"] = "true" })));

        Assert.Equal((2, 2), (result.Imported, result.Failed)); // dòng 3 và 4 lỗi; dòng 2 và 5 vẫn vào
        Assert.Contains(result.Errors, e => e.StartsWith("Dòng 3:", StringComparison.Ordinal) && e.Contains("Klingon", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("Dòng 4:", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.StartsWith("Dòng 2:", StringComparison.Ordinal)); // URL ảnh ngoài bị bỏ
        var items = (await All(staff)).Items.OrderBy(i => i.Id).ToList();
        Assert.Equal(["Sách 1", "Sách 4"], items.Select(i => i.Title));
        Assert.All(items, i => Assert.Equal(collection.Id, i.CollectionId));
        Assert.All(items, i => Assert.Equal(IHasStatus.Inactive, i.Status)); // mặc định ẩn như LRC
        Assert.Equal(("en", false, true, null), (items[0].Language, items[0].Free, items[1].Free, items[0].CoverUrl)); // ô trong file thắng mặc định
        Assert.Equal(2020, items[0].PublishYear);
        Assert.Equal(2, items[0].Authors.Count);

        Assert.Equal("IMPORT_COLUMN_MISSING", (await Error(await staff.PostAsync(U("/api/items/ImportExcel"), Form("file", "b.xlsx", Workbook(["Tác giả"], ["x"]))))).Code);
        Assert.Equal("IMPORT_FILE_INVALID", (await Error(await staff.PostAsync(U("/api/items/ImportExcel"), Form("file", "c.xlsx", Encoding.UTF8.GetBytes("không phải excel"))))).Code);
    }

    [Fact]
    public async Task Metadata_zip_has_one_dublin_core_per_item_and_dublin_core_files_import_with_bad_files_reported_by_name()
    {
        var staff = Staff(NewTenant());
        await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Truyện Kiều", Authors: [new EbookPerson { Last = "Nguyễn", First = "Du" }],
            Abstract: "Truyện thơ", Identifiers: [new EbookIdentifier { Type = "isbn", Value = "978-1" }], PublishDate: "1820"), Json);

        var zipBytes = await (await staff.PostAsJsonAsync(U("/api/items/ExportMetadata"), new ItemExportRequest(), Json)).Content.ReadAsByteArrayAsync();
        using var zip = new ZipArchive(new MemoryStream(zipBytes));
        var entry = Assert.Single(zip.Entries);
        Assert.Equal("item_001/dublin_core.xml", entry.FullName);
        string xml;
        using (var reader = new StreamReader(entry.Open())) xml = await reader.ReadToEndAsync();
        Assert.Contains("<dcvalue element=\"contributor\" qualifier=\"author\">Nguyễn Du</dcvalue>", xml, StringComparison.Ordinal);

        const string Evil = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><dublin_core><dcvalue element=\"title\" qualifier=\"none\">&e;</dcvalue></dublin_core>";
        const string NoTitle = "<dublin_core><dcvalue element=\"publisher\" qualifier=\"none\">NXB</dcvalue></dublin_core>";
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(xml)), "file", "tot.xml" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(Evil)), "file", "xxe.xml" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(NoTitle)), "file", "thieu.xml" },
            { new StringContent("true"), "published" },
        };
        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportXml"), form));

        Assert.Equal((1, 2), (result.Imported, result.Failed));
        Assert.Contains(result.Errors, e => e.StartsWith("xxe.xml:", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("thieu.xml:", StringComparison.Ordinal));
        var copy = (await All(staff)).Items[0];
        Assert.Equal(("Truyện Kiều", "Nguyễn Du", "Truyện thơ", IHasStatus.Active), (copy.Title, copy.Author, copy.Abstract, copy.Status));
        Assert.Equal("978-1", Assert.Single(copy.Identifiers).Value);
    }

    [Fact]
    public async Task Import_needs_the_add_permission_and_export_the_view_permission()
    {
        var tenantId = NewTenant();
        var viewer = Staff(tenantId, "DIGITAL_ITEMS:view");
        Assert.Equal(HttpStatusCode.OK, (await viewer.PostAsJsonAsync(U("/api/items/Export"), new ItemExportRequest(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(U("/api/items/ImportExcel"), Form("file", "a.xlsx", Workbook(["Tiêu đề"], ["X"])))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_ITEMS:add").PostAsJsonAsync(U("/api/items/Export"), new ItemExportRequest(), Json)).StatusCode);
    }
}
