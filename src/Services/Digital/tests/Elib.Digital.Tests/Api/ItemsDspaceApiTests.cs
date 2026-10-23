using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using Elib.BuildingBlocks.Crud;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

/// <summary>Nhập/xuất gói DSpace Simple Archive kèm file PDF (như ELIB-LRC: ImportDSpace, ExportDSpaceSaf).</summary>
public sealed class ItemsDspaceApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static readonly byte[] PdfA = [.. "%PDF-1.4\n"u8, .. "bản A"u8];
    private static readonly byte[] PdfB = [.. "%PDF-1.4\n"u8, .. "bản B khác"u8];

    private static string Dc(string title, string? author = null) =>
        $"""<dublin_core schema="dc"><dcvalue element="title" qualifier="none">{title}</dcvalue>{(author is null ? "" : $"""<dcvalue element="contributor" qualifier="author">{author}</dcvalue>""")}</dublin_core>""";

    private static byte[] Zip(params (string Path, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, content) in entries)
            {
                using var target = zip.CreateEntry(path).Open();
                target.Write(content);
            }
        return stream.ToArray();
    }

    private static byte[] Text(string value) => Encoding.UTF8.GetBytes(value);

    private static MultipartFormDataContent Form(byte[] zip, Dictionary<string, string>? fields = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", "goi.zip" } };
        foreach (var (key, value) in fields ?? []) form.Add(new StringContent(value), key);
        return form;
    }

    private static async Task<CrudPage<EbookItemDto>> All(HttpClient staff) =>
        await Read<CrudPage<EbookItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), new EbookItemSearch { PageSize = 100 }, Json));

    private static async Task<List<EbookFileDto>> FilesOf(HttpClient staff, EbookItemDto item) =>
        (await Read<CrudPage<EbookFileDto>>(await staff.PostAsJsonAsync(U("/api/files/Search"), new EbookFileSearch { EbookItemPublicId = item.PublicId, PageSize = 50 }, Json))).Items.ToList();

    [Fact]
    public async Task Import_creates_the_items_attaches_the_pdfs_and_reports_what_it_skipped()
    {
        var staff = Staff(NewTenant());
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Gói"), Json));
        var zip = Zip(
            ("item_001/dublin_core.xml", Text(Dc("Có hai file", "Nguyễn Văn A"))),
            ("item_001/contents", Text("a.pdf\tbundle:ORIGINAL\nb.pdf\tbundle:ORIGINAL\nc.docx\tbundle:ORIGINAL\nmat.pdf\tbundle:ORIGINAL\n")),
            ("item_001/a.pdf", PdfA), ("item_001/b.pdf", PdfB), ("item_001/c.docx", Text("word")),
            ("item_002/dublin_core.xml", Text(Dc("Chỉ metadata"))),
            ("item_003/dublin_core.xml", Text("<dublin_core><dcvalue element=\"publisher\" qualifier=\"none\">Không tiêu đề</dcvalue></dublin_core>")),
            ("item_003/x.pdf", PdfA));

        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportDspace"), Form(zip,
            new() { ["collectionId"] = collection.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ["published"] = "true" })));

        Assert.Equal((2, 1), (result.Imported, result.Failed));
        Assert.Contains(result.Errors, e => e.StartsWith("item_003:", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("c.docx") && w.Contains("PDF"));
        Assert.Contains(result.Warnings, w => w.Contains("mat.pdf") && w.Contains("thiếu"));

        var items = (await All(staff)).Items;
        var two = items.Single(i => i.Title == "Có hai file");
        Assert.Equal((collection.Id, "Nguyễn Văn A", 2), (two.CollectionId, two.Author, two.TotalFile));
        var files = await FilesOf(staff, two);
        Assert.Equal(["Document", "Document"], files.Select(f => f.Type));
        Assert.Equal([1, 2], files.Select(f => f.SortOrder));
        Assert.Equal([PdfA, PdfB], files.Select(f => Factory.Store.Files[f.MediaFileId]));
        Assert.Equal(0, items.Single(i => i.Title == "Chỉ metadata").TotalFile);
        Assert.DoesNotContain(items, i => i.Title == "Không tiêu đề");
    }

    [Fact]
    public async Task Import_without_a_contents_list_takes_every_pdf_in_the_folder()
    {
        var staff = Staff(NewTenant());
        var zip = Zip(("x/dublin_core.xml", Text(Dc("Không contents"))), ("x/one.pdf", PdfA), ("x/two.PDF", PdfB), ("x/notes.txt", Text("ghi chú")));
        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportDspace"), Form(zip)));
        Assert.Equal(2, (await FilesOf(staff, (await All(staff)).Items.Single())).Count);
        Assert.Contains(result.Warnings, w => w.Contains("notes.txt"));
    }

    [Fact]
    public async Task A_file_the_store_refuses_is_a_warning_and_the_item_is_still_imported()
    {
        var staff = Staff(NewTenant());
        var zip = Zip(("x/dublin_core.xml", Text(Dc("PDF giả"))), ("x/fake.pdf", Text("đây không phải PDF")));
        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportDspace"), Form(zip)));
        Assert.Equal(1, result.Imported);
        Assert.Contains(result.Warnings, w => w.Contains("fake.pdf") && w.Contains("không tải được"));
        Assert.Equal(0, (await All(staff)).Items.Single().TotalFile);
    }

    [Theory]
    [InlineData("../evil/dublin_core.xml")]
    [InlineData("a/../../evil/dublin_core.xml")]
    [InlineData("/abs/dublin_core.xml")]
    [InlineData("C:/windows/dublin_core.xml")]
    public async Task Import_refuses_paths_that_escape_the_package(string path)
    {
        var staff = Staff(NewTenant());
        var zip = Zip((path, Text(Dc("Độc hại"))));
        Assert.Equal("IMPORT_ZIP_UNSAFE_PATH", (await Error(await staff.PostAsync(U("/api/items/ImportDspace"), Form(zip)))).Code);
        Assert.Empty((await All(staff)).Items);
    }

    [Fact]
    public async Task Import_rejects_non_zip_empty_and_metadata_less_packages()
    {
        var staff = Staff(NewTenant());
        Assert.Equal("IMPORT_FILE_INVALID", (await Error(await staff.PostAsync(U("/api/items/ImportDspace"), Form(Text("không phải zip"))))).Code);
        Assert.Equal("IMPORT_FILE_EMPTY", (await Error(await staff.PostAsync(U("/api/items/ImportDspace"), Form(Zip(("x/a.pdf", PdfA)))))).Code);
    }

    [Fact]
    public async Task Import_refuses_dtd_in_the_metadata()
    {
        var staff = Staff(NewTenant());
        var evil = Text("""<?xml version="1.0"?><!DOCTYPE x [<!ENTITY e SYSTEM "file:///etc/passwd">]><dublin_core><dcvalue element="title" qualifier="none">&e;</dcvalue></dublin_core>""");
        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportDspace"), Form(Zip(("x/dublin_core.xml", evil)))));
        Assert.Equal((0, 1), (result.Imported, result.Failed));
        Assert.Empty((await All(staff)).Items);
    }

    [Fact]
    public async Task Import_needs_the_add_permission()
    {
        var tenantId = NewTenant();
        var zip = Zip(("x/dublin_core.xml", Text(Dc("Quyền"))));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_ITEMS:view").PostAsync(U("/api/items/ImportDspace"), Form(zip))).StatusCode);
    }

    [Fact]
    public async Task Export_with_files_packs_metadata_contents_and_the_pdfs_and_lists_unreadable_files()
    {
        var staff = Staff(NewTenant());
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Có file", Author: "Tô Hoài"), Json));
        var empty = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Không file"), Json));
        await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Factory.Store.Add(PdfA), FileExt: "pdf", SortOrder: 1), Json);
        await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Factory.Store.Add(PdfB), FileExt: "pdf", SortOrder: 2), Json);
        await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: "pdf", SortOrder: 3), Json); // file mất ở kho

        var response = await staff.PostAsJsonAsync(U("/api/items/ExportWithFiles"), new ItemExportRequest(new ItemSelection([item.PublicId, empty.PublicId])), Json);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        string Read(string name) { using var r = new StreamReader(zip.GetEntry(name)!.Open(), Encoding.UTF8); return r.ReadToEnd(); }
        byte[] Bytes(string name) { using var m = new MemoryStream(); using var s = zip.GetEntry(name)!.Open(); s.CopyTo(m); return m.ToArray(); }

        var names = zip.Entries.Select(e => e.FullName).ToHashSet();
        // Thư mục theo thứ tự xuất (mới nhất trước): item_001 là "Không file".
        Assert.Contains("Không file", Read("item_001/dublin_core.xml"));
        Assert.DoesNotContain("item_001/contents", names);
        Assert.Contains("Tô Hoài", Read("item_002/dublin_core.xml"));
        Assert.Equal("document.pdf\tbundle:ORIGINAL\ndocument_2.pdf\tbundle:ORIGINAL\n", Read("item_002/contents"));
        Assert.Equal(PdfA, Bytes("item_002/document.pdf"));
        Assert.Equal(PdfB, Bytes("item_002/document_2.pdf"));
        Assert.Contains("không đọc được file", Read("export-errors.txt"));
    }

    [Fact]
    public async Task An_exported_package_can_be_imported_back_with_its_files()
    {
        var staff = Staff(NewTenant());
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Vòng quanh", Author: "Hà Nội"), Json));
        await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Factory.Store.Add(PdfA), FileExt: "pdf"), Json);
        var exported = await (await staff.PostAsJsonAsync(U("/api/items/ExportWithFiles"), new ItemExportRequest(new ItemSelection([item.PublicId])), Json)).Content.ReadAsByteArrayAsync();

        var result = await Read<ItemImportResult>(await staff.PostAsync(U("/api/items/ImportDspace"), Form(exported)));

        Assert.Equal((1, 0), (result.Imported, result.Failed));
        var copy = (await All(staff)).Items[0]; // mới nhất
        Assert.NotEqual(item.PublicId, copy.PublicId);
        Assert.Equal(("Vòng quanh", "Hà Nội", 1), (copy.Title, copy.Author, copy.TotalFile));
        Assert.Equal(PdfA, Factory.Store.Files[(await FilesOf(staff, copy)).Single().MediaFileId]);
    }

    [Fact]
    public async Task Export_with_files_needs_the_view_permission_and_a_selection()
    {
        var tenantId = NewTenant();
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_COLLECTIONS:view").PostAsJsonAsync(U("/api/items/ExportWithFiles"),
            new ItemExportRequest(new ItemSelection([Guid.NewGuid()])), Json)).StatusCode);
    }
}
