using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Application;
using Elib.Digital.Domain;
using Elib.Digital.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Elib.Digital.Tests.Api;

/// <summary>Đồng bộ nội dung (trích chữ PDF + vector hóa chạy nền) và tìm trong nội dung trên OPAC.</summary>
public sealed class ItemsContentApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static byte[] Pdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            if (text.Length > 0) page.AddText(text, 12, new PdfPoint(25, 700), font);
        }
        return builder.Build();
    }

    private static async Task<EbookItemDto> AddItem(HttpClient staff, string title, bool free = true, int status = IHasStatus.Active, long? topicId = null) =>
        await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields(title, Free: free, Status: status, TopicId: topicId), Json));

    private async Task AddPdf(HttpClient staff, Guid itemPublicId, byte[] pdf, int order = 1)
    {
        var mediaId = Factory.Store.Add(pdf);
        await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(itemPublicId, mediaId, FileExt: "pdf", SortOrder: order), Json));
    }

    private Task<int> RunJob() => Factory.Services.GetRequiredService<EbookContentSyncJob>().RunOnceAsync(CancellationToken.None);

    private static async Task<ContentStatusDto?> StatusOf(HttpClient staff, Guid publicId) =>
        (await Read<List<ContentStatusDto>>(await staff.PostAsJsonAsync(U("/api/items/ContentStatus"), new ContentStatusRequest([publicId]), Json))).FirstOrDefault();

    private static Task<HttpResponseMessage> Sync(HttpClient staff, params Guid[] ids) =>
        staff.PostAsJsonAsync(U("/api/items/SyncContent"), new ItemSelection(ids), Json);

    private static async Task<List<ContentHit>> Search(HttpClient opac, string q) =>
        await Read<List<ContentHit>>(await opac.PostAsJsonAsync(U("/api/opac/content/search"), new ContentSearchRequest(q), Json));

    private static Task<HttpResponseMessage> StaffSearch(HttpClient staff, string q, bool semantic) =>
        staff.PostAsJsonAsync(U("/api/items/SearchContent"), new ContentSearchRequest(q, Semantic: semantic), Json);

    [Fact]
    public async Task Sync_extracts_the_pdf_text_and_opac_search_finds_it_with_page_and_snippet()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        Factory.Embedder.IsEnabled = false;
        var item = await AddItem(staff, "Giáo trình kinh tế");
        await AddPdf(staff, item.PublicId, Pdf("Chapter one introduces macroeconomics.", "Marginal utility declines with consumption."));

        var queued = await Read<ContentSyncResult>(await Sync(staff, item.PublicId));
        Assert.Equal(1, queued.Queued);
        Assert.Equal("pending", (await StatusOf(staff, item.PublicId))!.Status);

        Assert.True(await RunJob() >= 1);
        var status = (await StatusOf(staff, item.PublicId))!;
        Assert.Equal(("done", 2), (status.Status, status.PageCount));
        Assert.True(status.ChunkCount >= 2);
        Assert.Equal(0, status.EmbeddedCount);
        Assert.NotNull(status.SyncedAt);

        var hits = await Search(Opac(tenantId), "Marginal UTILITY");
        var hit = Assert.Single(hits);
        Assert.Equal((item.PublicId, 2), (hit.PublicId, hit.Page));
        Assert.Contains("Marginal utility", hit.Snippet);
        Assert.Empty(await Search(Opac(tenantId), "thermodynamics"));
    }

    [Fact]
    public async Task Search_never_shows_snippets_of_documents_that_need_borrowing_or_a_login_and_skips_hidden_ones()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var loginTopic = await Read<EbookTopicDto>(await staff.PostAsJsonAsync(U("/api/topics/Add"), new EbookTopicRequest("Nội bộ", RequiresLogin: true), Json));
        var open = await AddItem(staff, "Công khai");
        var borrowed = await AddItem(staff, "Cần mượn", free: false);
        var gated = await AddItem(staff, "Chủ đề đăng nhập", topicId: loginTopic.Id);
        var hidden = await AddItem(staff, "Đã ẩn", status: IHasStatus.Inactive);
        foreach (var item in new[] { open, borrowed, gated, hidden }) await AddPdf(staff, item.PublicId, Pdf("The quantum secret is here."));
        await Read<ContentSyncResult>(await staff.PostAsJsonAsync(U("/api/items/SyncContent"),
            new ItemSelection([open.PublicId, borrowed.PublicId, gated.PublicId, hidden.PublicId]), Json));
        await RunJob();

        var hits = await Search(Opac(tenantId), "quantum secret");
        Assert.Equal(3, hits.Count); // không có tài liệu đã ẩn
        Assert.DoesNotContain(hits, h => h.PublicId == hidden.PublicId);
        Assert.Contains("quantum secret", hits.Single(h => h.PublicId == open.PublicId).Snippet);
        Assert.Null(hits.Single(h => h.PublicId == borrowed.PublicId).Snippet);
        Assert.Null(hits.Single(h => h.PublicId == gated.PublicId).Snippet);
    }

    [Fact]
    public async Task A_scanned_pdf_has_no_text_layer_and_is_reported_as_such()
    {
        var staff = Staff(NewTenant());
        var item = await AddItem(staff, "Bản quét");
        await AddPdf(staff, item.PublicId, Pdf("", ""));
        await Sync(staff, item.PublicId);
        await RunJob();
        var status = (await StatusOf(staff, item.PublicId))!;
        Assert.Equal(("no_text", 0), (status.Status, status.ChunkCount));
        Assert.Contains("OCR", status.Error);
    }

    [Fact]
    public async Task Items_without_a_pdf_are_skipped_and_a_missing_media_file_fails_the_item()
    {
        var staff = Staff(NewTenant());
        var plain = await AddItem(staff, "Không file");
        var broken = await AddItem(staff, "File mất");
        await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(broken.PublicId, Guid.CreateVersion7(), FileExt: "pdf"), Json));

        var result = await Read<ContentSyncResult>(await staff.PostAsJsonAsync(U("/api/items/SyncContent"), new ItemSelection([plain.PublicId, broken.PublicId]), Json));
        Assert.Equal(1, result.Queued);
        Assert.Equal("CONTENT_NO_PDF", Assert.Single(result.Skipped).Code);

        await RunJob();
        var status = (await StatusOf(staff, broken.PublicId))!;
        Assert.Equal("failed", status.Status);
        Assert.False(string.IsNullOrWhiteSpace(status.Error));
        Assert.Null(await StatusOf(staff, plain.PublicId));
    }

    [Fact]
    public async Task A_corrupt_pdf_fails_without_blocking_the_next_item()
    {
        var staff = Staff(NewTenant());
        var bad = await AddItem(staff, "PDF hỏng");
        var good = await AddItem(staff, "PDF tốt");
        await AddPdf(staff, bad.PublicId, "%PDF-1.4 this is not a real document"u8.ToArray());
        await AddPdf(staff, good.PublicId, Pdf("Readable content about astronomy."));
        await Read<ContentSyncResult>(await staff.PostAsJsonAsync(U("/api/items/SyncContent"), new ItemSelection([bad.PublicId, good.PublicId]), Json));
        await RunJob();
        Assert.Equal("failed", (await StatusOf(staff, bad.PublicId))!.Status);
        Assert.Equal("done", (await StatusOf(staff, good.PublicId))!.Status);
    }

    [Fact]
    public async Task Syncing_again_replaces_the_chunks_instead_of_duplicating_them()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var item = await AddItem(staff, "Làm lại");
        await AddPdf(staff, item.PublicId, Pdf("Original wording about volcanoes."));
        await Sync(staff, item.PublicId);
        await RunJob();
        var first = (await StatusOf(staff, item.PublicId))!;

        await Sync(staff, item.PublicId);
        await RunJob();
        var second = (await StatusOf(staff, item.PublicId))!;
        Assert.Equal(first.ChunkCount, second.ChunkCount);
        Assert.Single(await Search(Opac(tenantId), "volcanoes"));
    }

    [Fact]
    public async Task Removing_the_last_pdf_takes_its_text_out_of_search_at_once_and_adding_one_requeues_the_sync()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var item = await AddItem(staff, "Gỡ file");
        await AddPdf(staff, item.PublicId, Pdf("Secret blueprint of the old fortress."));
        await Sync(staff, item.PublicId);
        await RunJob();
        Assert.Single(await Search(Opac(tenantId), "fortress"));

        // Gỡ file PDF duy nhất: đoạn đã lập chỉ mục biến mất ngay, không đợi việc nền.
        var file = (await Read<CrudPageOfFiles>(await staff.PostAsJsonAsync(U("/api/files/Search"), new EbookFileSearch { EbookItemPublicId = item.PublicId, PageSize = 10 }, Json))).Items.Single();
        Assert.True((await staff.DeleteAsync(U($"/api/files/Delete/{file.PublicId}"))).IsSuccessStatusCode);
        Assert.Empty(await Search(Opac(tenantId), "fortress"));
        Assert.Equal("pending", (await StatusOf(staff, item.PublicId))!.Status);
        await RunJob();
        var status = (await StatusOf(staff, item.PublicId))!;
        Assert.Equal(("failed", 0), (status.Status, status.ChunkCount));
        Assert.Contains("không còn file PDF", status.Error);

        // Thêm file mới: tài liệu tự xếp lại hàng đợi và nội dung mới tìm được.
        await AddPdf(staff, item.PublicId, Pdf("Fresh chronicle of the harbour."));
        Assert.Equal("pending", (await StatusOf(staff, item.PublicId))!.Status);
        await RunJob();
        Assert.Single(await Search(Opac(tenantId), "harbour"));
        Assert.Empty(await Search(Opac(tenantId), "fortress"));
    }

    private sealed record CrudPageOfFiles(List<EbookFileDto> Items);

    [Fact]
    public async Task Semantic_search_needs_the_embedder_and_ranks_by_meaning_when_it_is_on()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var cooking = await AddItem(staff, "Nấu ăn");
        var space = await AddItem(staff, "Vũ trụ");
        await AddPdf(staff, cooking.PublicId, Pdf("Boil the noodles and add fresh basil sauce."));
        await AddPdf(staff, space.PublicId, Pdf("Planets orbit the sun in elliptical paths."));

        Factory.Embedder.IsEnabled = false;
        var off = await StaffSearch(staff, "planets orbit", semantic: true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, off.StatusCode);
        Assert.Equal("EMBEDDING_DISABLED", (await Error(off)).Code);

        Factory.Embedder.IsEnabled = true;
        Factory.Embedder.Fail = false;
        await Read<ContentSyncResult>(await staff.PostAsJsonAsync(U("/api/items/SyncContent"), new ItemSelection([cooking.PublicId, space.PublicId]), Json));
        await RunJob();
        var status = (await StatusOf(staff, space.PublicId))!;
        Assert.Equal(status.ChunkCount, status.EmbeddedCount);
        Assert.True(status.EmbeddedCount > 0);

        var hits = await Read<List<ContentHit>>(await StaffSearch(staff, "planets orbit the sun", semantic: true));
        Assert.Equal(space.PublicId, hits[0].PublicId);

        // Ẩn danh không được quét vector (tốn kém): yêu cầu ngữ nghĩa ở OPAC rơi về tìm theo từ khóa, không lỗi.
        var anonymous = await Read<List<ContentHit>>(await Opac(tenantId).PostAsJsonAsync(U("/api/opac/content/search"), new ContentSearchRequest("planets", Semantic: true), Json));
        Assert.Equal(space.PublicId, Assert.Single(anonymous).PublicId);
        Factory.Embedder.IsEnabled = false;
    }

    [Fact]
    public async Task When_the_embedder_goes_down_the_text_is_kept_and_keyword_search_still_works()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var item = await AddItem(staff, "Ollama sập");
        await AddPdf(staff, item.PublicId, Pdf("Glaciers carve valleys slowly."));
        Factory.Embedder.IsEnabled = true;
        Factory.Embedder.Fail = true;
        try
        {
            await Sync(staff, item.PublicId);
            await RunJob();
        }
        finally
        {
            Factory.Embedder.Fail = false;
            Factory.Embedder.IsEnabled = false;
        }
        var status = (await StatusOf(staff, item.PublicId))!;
        Assert.Equal(("done", 0), (status.Status, status.EmbeddedCount));
        Assert.Contains("Vector hóa dừng", status.Error);
        Assert.Single(await Search(Opac(tenantId), "glaciers"));
    }

    [Fact]
    public async Task Staff_content_search_sees_hidden_documents_and_every_snippet()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var borrowed = await AddItem(staff, "Cần mượn", free: false);
        var hidden = await AddItem(staff, "Đã ẩn", status: IHasStatus.Inactive);
        foreach (var item in new[] { borrowed, hidden }) await AddPdf(staff, item.PublicId, Pdf("Confidential pamphlet about lighthouses."));
        await Read<ContentSyncResult>(await staff.PostAsJsonAsync(U("/api/items/SyncContent"), new ItemSelection([borrowed.PublicId, hidden.PublicId]), Json));
        await RunJob();

        var hits = await Read<List<ContentHit>>(await StaffSearch(staff, "lighthouses", semantic: false));
        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Contains("lighthouses", h.Snippet));
        var anonymous = Assert.Single(await Search(Opac(tenantId), "lighthouses")); // OPAC: tài liệu ẩn không hiện, tài liệu cần mượn hiện không kèm đoạn trích
        Assert.Equal(borrowed.PublicId, anonymous.PublicId);
        Assert.Null(anonymous.Snippet);
    }

    [Fact]
    public async Task Sync_and_status_follow_the_permissions_and_search_validates_the_query()
    {
        var tenantId = NewTenant();
        var item = await AddItem(Staff(tenantId), "Quyền");
        Assert.Equal(HttpStatusCode.Forbidden, (await Sync(Staff(tenantId, "DIGITAL_ITEMS:view"), item.PublicId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "DIGITAL_ITEMS:view").PostAsJsonAsync(U("/api/items/ContentStatus"), new ContentStatusRequest([item.PublicId]), Json)).StatusCode);
        Assert.Equal("CONTENT_QUERY_INVALID", (await Error(await Opac(tenantId).PostAsJsonAsync(U("/api/opac/content/search"), new ContentSearchRequest("a"), Json))).Code);
        Assert.Equal("SELECTION_EMPTY", (await Error(await Sync(Staff(tenantId)))).Code);
    }

    /// <summary>Cùng cách dựng PDF tối thiểu (không thư viện) mà e2e dùng — bảo đảm bộ trích chữ đọc được lớp văn bản của nó.</summary>
    [Fact]
    public void The_text_extractor_reads_a_minimal_hand_built_pdf_and_rejects_garbage()
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var stream = "BT /F1 12 Tf 50 750 Td (noi dung e2e marginal utility) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var text = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(text.Length);
            text.Append(invariant, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = text.Length;
        text.Append(invariant, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append(offset.ToString("D10", invariant)).Append(" 00000 n \n");
        text.Append(invariant, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        var pages = new PdfPigTextExtractor().Extract(new MemoryStream(System.Text.Encoding.ASCII.GetBytes(text.ToString())), 10).ToList();
        Assert.Equal((1, "noi dung e2e marginal utility"), (pages[0].Page, pages[0].Text));
        Assert.Throws<InvalidDataException>(() => new PdfPigTextExtractor().Extract(new MemoryStream("không phải pdf"u8.ToArray()), 10).ToList());
    }

    [Fact]
    public void Split_cuts_each_page_into_overlapping_chunks_that_never_cross_a_page()
    {
        var sentence = "Kinh tế học vi mô nghiên cứu hành vi của người tiêu dùng. ";
        var pages = new[] { new PdfPageText(1, string.Concat(Enumerable.Repeat(sentence, 60))), new PdfPageText(2, "Trang hai ngắn."), new PdfPageText(3, "   ") };

        var chunks = EbookContentSync.Split(pages).ToList();

        Assert.All(chunks, c => Assert.InRange(c.Text.Length, 1, EbookContentSync.ChunkChars));
        Assert.True(chunks.Count(c => c.Page == 1) >= 3);
        Assert.Equal([(2, "Trang hai ngắn.")], chunks.Where(c => c.Page == 2).Select(c => (c.Page, c.Text)));
        Assert.DoesNotContain(chunks, c => c.Page == 3);
        // gối nhau: đoạn sau bắt đầu bằng phần cuối của đoạn trước, và không cắt giữa chữ
        var first = chunks.Where(c => c.Page == 1).ToList();
        Assert.All(first.Skip(1), c => Assert.Contains(" " + c.Text[..10], pages[0].Text, StringComparison.Ordinal)); // đoạn nào cũng bắt đầu ở đầu một từ
        Assert.Contains(first[0].Text[^20..].Trim().Split(' ')[^1], first[1].Text);
    }
}
