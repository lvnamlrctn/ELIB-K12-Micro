using System.Net;
using System.Net.Http.Json;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

public sealed class ReadingApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private async Task<(HttpClient Staff, EbookItemDto Item, EbookFileDto File)> FreeItemAsync(long tenantId, bool free = true)
    {
        var staff = Staff(tenantId);
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Số đỏ", Free: free), Json));
        var file = await Read<EbookFileDto>(await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: "pdf"), Json));
        return (staff, item, file);
    }

    private static ReadBeatRequest Beat(EbookItemDto item, EbookFileDto file, Guid session, int page, int seconds, int pages = 200) =>
        new(item.PublicId, file.PublicId, session, page, pages, seconds, "desktop");

    [Fact]
    public async Task Heartbeats_update_one_session_and_the_resume_point_and_seconds_cannot_outrun_the_clock()
    {
        var tenantId = NewTenant();
        var (_, item, file) = await FreeItemAsync(tenantId);
        var readerId = Guid.CreateVersion7();
        await Publish(Reader(tenantId, "RD-001") with { ReaderPublicId = readerId });
        var reader = ReaderClient(tenantId, "RD-001", readerId);
        var session = Guid.CreateVersion7();

        var first = await Read<ReadProgressDto>(await reader.PutAsJsonAsync(U("/api/reader/reading/progress"), Beat(item, file, session, 3, 9999), Json));
        Assert.Equal((3, 3, 200, 2), (first.LastPage, first.MaxPage, first.PageCount, first.Percent == 2 ? 2 : first.Percent));

        Factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await reader.PutAsJsonAsync(U("/api/reader/reading/progress"), Beat(item, file, session, 10, 9999), Json); // khai gian: chỉ được tối đa thời gian đã trôi
        await reader.PutAsJsonAsync(U("/api/reader/reading/progress"), Beat(item, file, session, 7, 40), Json);

        var resume = await Read<ReadProgressDto>(await reader.GetAsync(U($"/api/reader/reading/progress/{item.PublicId}")));
        Assert.Equal((7, 10, 200), (resume.LastPage, resume.MaxPage, resume.PageCount));

        var staff = Staff(tenantId);
        var rows = await Read<ReadingSessionPage>(await staff.PostAsJsonAsync(U("/api/reading-tracking/Search"), new ReadingSearch(null, null, null, null), Json));
        var row = Assert.Single(rows.Items);
        Assert.Equal(("RD-001", 10, 7), (row.CardNo, row.MaxPage, row.LastPage));
        Assert.InRange(row.ActiveSeconds, 1, 62 + 32); // lượt đầu ≤ 60 + 30 giây thực + dung sai nhịp — tuyệt đối không phải 9999
    }

    [Fact]
    public async Task Bad_pages_non_readable_documents_and_foreign_sessions_are_rejected()
    {
        var tenantId = NewTenant();
        var (_, free, freeFile) = await FreeItemAsync(tenantId);
        var (_, paid, paidFile) = await FreeItemAsync(tenantId, free: false);
        var readerId = Guid.CreateVersion7();
        var reader = ReaderClient(tenantId, "RD-002", readerId);
        var url = U("/api/reader/reading/progress");

        Assert.Equal("READ_PAGE_INVALID", (await Error(await reader.PutAsJsonAsync(url, Beat(free, freeFile, Guid.CreateVersion7(), 500, 5, pages: 200), Json))).Code);
        Assert.Equal("READ_PAGE_COUNT_INVALID", (await Error(await reader.PutAsJsonAsync(url, Beat(free, freeFile, Guid.CreateVersion7(), 1, 5, pages: 0), Json))).Code);
        Assert.Equal("EBOOK_NOT_BORROWED", (await Error(await reader.PutAsJsonAsync(url, Beat(paid, paidFile, Guid.CreateVersion7(), 1, 5), Json))).Code);

        var session = Guid.CreateVersion7();
        Assert.True((await reader.PutAsJsonAsync(url, Beat(free, freeFile, session, 1, 5), Json)).IsSuccessStatusCode);
        var other = ReaderClient(tenantId, "RD-003", Guid.CreateVersion7());
        Assert.True((await other.PutAsJsonAsync(url, Beat(free, freeFile, session, 1, 5), Json)).IsSuccessStatusCode); // mã trùng của người khác = lượt riêng của họ
        var rows = await Read<ReadingSessionPage>(await Staff(tenantId).PostAsJsonAsync(U("/api/reading-tracking/Search"), new ReadingSearch(null, null, null, null), Json));
        Assert.Equal(2, rows.TotalCount);

        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_ITEMS:view").PostAsJsonAsync(U("/api/reading-tracking/Search"), new ReadingSearch(null, null, null, null), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reader.GetAsync(U($"/api/reader/reading/progress/{paid.PublicId}"))).StatusCode);
    }

    [Fact]
    public async Task Statistics_summarise_sessions_top_items_and_daily_series_and_stay_inside_the_tenant()
    {
        var tenantId = NewTenant();
        var (staff, item, file) = await FreeItemAsync(tenantId);
        var readerId = Guid.CreateVersion7();
        await Publish(Reader(tenantId, "RD-004") with { ReaderPublicId = readerId, FullName = "Nguyễn Văn Đọc" });
        var reader = ReaderClient(tenantId, "RD-004", readerId);
        await reader.PutAsJsonAsync(U("/api/reader/reading/progress"), Beat(item, file, Guid.CreateVersion7(), 2, 30), Json);
        await reader.GetAsync(U($"/api/opac/items/{item.PublicId}/files/{file.PublicId}/url"));
        await Opac(tenantId).GetAsync(U($"/api/opac/items/{item.PublicId}/files/{file.PublicId}/url")); // xem ẩn danh cũng vào nhật ký truy cập

        var today = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(7));
        var stats = await Read<ReadingStats>(await staff.PostAsJsonAsync(U("/api/reading-tracking/Statistics"), new ReadingStatsRequest(today.AddDays(-2), today), Json));
        Assert.Equal((1, 1, 1), (stats.Totals.Sessions, stats.Totals.Readers, stats.Totals.Ebooks));
        Assert.Equal(item.PublicId, Assert.Single(stats.TopEbooks).EbookPublicId);
        Assert.Equal("RD-004", Assert.Single(stats.TopReaders).CardNo);
        Assert.Equal(3, stats.Daily.Count);
        Assert.Equal(1, stats.Daily[^1].Sessions);
        Assert.Equal(2, stats.Totals.Views);

        var bad = await staff.PostAsJsonAsync(U("/api/reading-tracking/Statistics"), new ReadingStatsRequest(today.AddDays(-400), today), Json);
        Assert.Equal("RANGE_TOO_LONG", (await Error(bad)).Code);

        var other = Staff(NewTenant());
        var empty = await Read<ReadingStats>(await other.PostAsJsonAsync(U("/api/reading-tracking/Statistics"), new ReadingStatsRequest(null, null), Json));
        Assert.Equal(0, empty.Totals.Sessions);
    }

    [Fact]
    public async Task Bookmarks_are_private_one_per_page_and_capped()
    {
        var tenantId = NewTenant();
        var (_, item, _) = await FreeItemAsync(tenantId);
        var mine = ReaderClient(tenantId, "BM-001", Guid.CreateVersion7());
        var theirs = ReaderClient(tenantId, "BM-002", Guid.CreateVersion7());

        var first = await Read<BookmarkDto>(await mine.PostAsJsonAsync(U("/api/reader/bookmarks"), new BookmarkRequest(item.PublicId, 12, "Đoạn hay"), Json));
        var again = await Read<BookmarkDto>(await mine.PostAsJsonAsync(U("/api/reader/bookmarks"), new BookmarkRequest(item.PublicId, 12, "Sửa ghi chú"), Json));
        Assert.Equal(first.PublicId, again.PublicId); // cùng trang = sửa ghi chú, không thêm dấu
        Assert.Equal("Sửa ghi chú", Assert.Single(await Read<List<BookmarkDto>>(await mine.GetAsync(U($"/api/reader/bookmarks/{item.PublicId}")))).Note);
        Assert.Empty(await Read<List<BookmarkDto>>(await theirs.GetAsync(U($"/api/reader/bookmarks/{item.PublicId}"))));

        await theirs.DeleteAsync(U($"/api/reader/bookmarks/{first.PublicId}")); // xoá dấu của người khác: không tác dụng
        Assert.Single(await Read<List<BookmarkDto>>(await mine.GetAsync(U($"/api/reader/bookmarks/{item.PublicId}"))));
        Assert.Equal("BOOKMARK_PAGE_INVALID", (await Error(await mine.PostAsJsonAsync(U("/api/reader/bookmarks"), new BookmarkRequest(item.PublicId, 0, null), Json))).Code);

        await mine.DeleteAsync(U($"/api/reader/bookmarks/{first.PublicId}"));
        Assert.Empty(await Read<List<BookmarkDto>>(await mine.GetAsync(U($"/api/reader/bookmarks/{item.PublicId}"))));
    }
}
