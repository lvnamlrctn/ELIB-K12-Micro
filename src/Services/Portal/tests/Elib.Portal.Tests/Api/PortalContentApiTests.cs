using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Portal.Application;

namespace Elib.Portal.Tests.Api;

public sealed class PortalContentApiTests(PortalApiFactory factory) : PortalTestBase(factory)
{
    private static NewsRequest Draft(string title, string? content = null, long? categoryId = null, int status = IHasStatus.Inactive, bool allowComment = false,
        DateTimeOffset? start = null, DateTimeOffset? end = null, bool featured = false) =>
        new(title, Content: content, CategoryId: categoryId, Status: status, AllowComment: allowComment, StartTime: start, EndTime: end, Featured: featured);

    private static async Task<NewsDto> AddNews(HttpClient staff, NewsRequest request) => await Read<NewsDto>(await staff.PostAsJsonAsync(U("/api/news/Add"), request, Json));

    private static async Task<OpacNewsPage> OpacNews(HttpClient opac, OpacNewsSearch? search = null) =>
        await Read<OpacNewsPage>(await opac.PostAsJsonAsync(U("/api/opac/news/Search"), search ?? new OpacNewsSearch(), Json));

    [Fact]
    public async Task A_draft_is_invisible_until_published_and_its_html_is_cleaned_and_its_slug_comes_from_the_title()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);

        var news = await AddNews(staff, Draft("Hội sách mùa thu 2026", "<p>Xin chào</p><script>alert(1)</script><img src=\"x\" onerror=\"alert(2)\">"));
        Assert.Equal("hoi-sach-mua-thu-2026", news.Slug);
        Assert.Equal("<p>Xin chào</p>", news.Content);
        Assert.Empty((await OpacNews(opac)).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U($"/api/opac/news/{news.Slug}"))).StatusCode);

        var twin = await AddNews(staff, Draft("Hội sách mùa thu 2026"));
        Assert.Equal("hoi-sach-mua-thu-2026-2", twin.Slug); // slug luôn duy nhất

        await staff.PutAsJsonAsync(U("/api/news/ChangeStatus"), new ChangeStatusRequest(news.PublicId, IHasStatus.Active), Json);
        var page = await OpacNews(opac);
        var item = Assert.Single(page.Items);
        Assert.Equal(news.Slug, item.Slug);
        var detail = await Read<OpacNewsDetail>(await opac.GetAsync(U($"/api/opac/news/{news.Slug}")));
        Assert.Equal("<p>Xin chào</p>", detail.Content);

        await opac.PostAsync(U($"/api/opac/news/{news.Slug}/view"), null);
        await opac.PostAsync(U($"/api/opac/news/{news.Slug}/view"), null);
        Assert.Equal(2, (await Read<OpacNewsDetail>(await opac.GetAsync(U($"/api/opac/news/{news.Slug}")))).TotalView);
    }

    [Fact]
    public async Task A_slug_typed_by_the_editor_must_be_unique_and_well_formed()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var first = await AddNews(staff, Draft("Bài một") with { Slug = "bai-mot" });
        Assert.Equal("bai-mot", first.Slug);
        Assert.Equal("NEWS_SLUG_EXISTS", await ErrorCode(await staff.PostAsJsonAsync(U("/api/news/Add"), Draft("Bài hai") with { Slug = "bai-mot" }, Json)));
        Assert.Equal("SLUG_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/news/Add"), Draft("Bài ba") with { Slug = "Có dấu / lạ" }, Json)));
        var second = await AddNews(staff, Draft("Bài hai") with { Slug = "bai-hai" });
        Assert.Equal("NEWS_SLUG_EXISTS", await ErrorCode(await staff.PutAsJsonAsync(U($"/api/news/Update/{second.PublicId}"), Draft("Bài hai") with { Slug = "bai-mot" }, Json)));
        Assert.True((await staff.PutAsJsonAsync(U($"/api/news/Update/{second.PublicId}"), Draft("Bài hai sửa") with { Slug = "bai-hai" }, Json)).IsSuccessStatusCode); // giữ nguyên slug của chính nó
    }

    [Fact]
    public async Task Publishing_and_editing_published_news_needs_the_publish_permission()
    {
        var tenantId = NewTenant();
        var editor = Staff(tenantId, "PORTAL_NEWS:view", "PORTAL_NEWS:add", "PORTAL_NEWS:edit", "PORTAL_NEWS:delete");
        var publisher = Staff(tenantId);

        var draft = await AddNews(editor, Draft("Bản nháp"));
        Assert.Equal("PERMISSION_DENIED", await ErrorCode(await editor.PostAsJsonAsync(U("/api/news/Add"), Draft("Đăng luôn", status: IHasStatus.Active), Json)));
        Assert.Equal("PERMISSION_DENIED", await ErrorCode(await editor.PutAsJsonAsync(U("/api/news/ChangeStatus"), new ChangeStatusRequest(draft.PublicId, IHasStatus.Active), Json)));
        Assert.Equal("PERMISSION_DENIED", await ErrorCode(await editor.PutAsJsonAsync(U($"/api/news/Update/{draft.PublicId}"), Draft("Sửa", status: IHasStatus.Active), Json)));

        // Bản nháp sửa được; bản đã đăng thì người chỉ có quyền sửa không đụng vào được (kể cả xoá).
        Assert.True((await editor.PutAsJsonAsync(U($"/api/news/Update/{draft.PublicId}"), Draft("Bản nháp sửa"), Json)).IsSuccessStatusCode);
        await publisher.PutAsJsonAsync(U("/api/news/ChangeStatus"), new ChangeStatusRequest(draft.PublicId, IHasStatus.Active), Json);
        Assert.Equal("PERMISSION_DENIED", await ErrorCode(await editor.PutAsJsonAsync(U($"/api/news/Update/{draft.PublicId}"), Draft("Sửa nội dung đã đăng"), Json)));
        Assert.Equal("PERMISSION_DENIED", await ErrorCode(await editor.DeleteAsync(U($"/api/news/Delete/{draft.PublicId}"))));
        Assert.True((await publisher.DeleteAsync(U($"/api/news/Delete/{draft.PublicId}"))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Scheduled_news_appears_and_disappears_with_the_clock_and_login_only_categories_stay_off_the_anonymous_path()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var now = Factory.Clock.GetUtcNow();

        await AddNews(staff, Draft("Hẹn giờ", status: IHasStatus.Active, start: now.AddHours(1), end: now.AddHours(3)));
        Assert.Empty((await OpacNews(opac)).Items);
        Factory.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Single((await OpacNews(opac)).Items);
        Factory.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Empty((await OpacNews(opac)).Items);

        var secret = await Read<NewsCategoryDto>(await staff.PostAsJsonAsync(U("/api/news-categories/Add"), new NewsCategoryRequest("Nội bộ", RequiresLogin: true), Json));
        await AddNews(staff, Draft("Tin nội bộ", categoryId: secret.Id, status: IHasStatus.Active));
        Assert.Empty((await OpacNews(opac)).Items);
        Assert.DoesNotContain(await Read<List<OpacCategory>>(await opac.GetAsync(U("/api/opac/categories"))), c => c.Id == secret.Id);
    }

    [Fact]
    public async Task Category_branch_filter_language_filter_and_search_work_and_categories_in_use_cannot_be_deleted()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var root = await Read<NewsCategoryDto>(await staff.PostAsJsonAsync(U("/api/news-categories/Add"), new NewsCategoryRequest("Sự kiện"), Json));
        var child = await Read<NewsCategoryDto>(await staff.PostAsJsonAsync(U("/api/news-categories/Add"), new NewsCategoryRequest("Hội sách", ParentId: root.Id), Json));
        Assert.Equal(2, child.Level);
        await AddNews(staff, Draft("Khai mạc hội sách", categoryId: child.Id, status: IHasStatus.Active));
        await AddNews(staff, Draft("Tin khác", status: IHasStatus.Active) with { Language = "en" });

        Assert.Single((await OpacNews(opac, new OpacNewsSearch { CategoryId = root.Id })).Items); // lọc theo cả nhánh con
        // Nội dung không gắn ngôn ngữ hiện ở mọi ngôn ngữ; tin gắn "en" thì chỉ hiện ở giao diện en.
        Assert.Equal(["Tin khác", "Khai mạc hội sách"], (await OpacNews(opac, new OpacNewsSearch { Language = "en" })).Items.Select(i => i.Title));
        Assert.Equal(["Khai mạc hội sách"], (await OpacNews(opac, new OpacNewsSearch { Language = "vi" })).Items.Select(i => i.Title));
        Assert.Single((await OpacNews(opac, new OpacNewsSearch { Q = "khai mac" })).Items);

        Assert.Equal("TREE_HAS_CHILDREN", await ErrorCode(await staff.DeleteAsync(U($"/api/news-categories/Delete/{root.PublicId}"))));
        Assert.Equal("CATEGORY_HAS_NEWS", await ErrorCode(await staff.DeleteAsync(U($"/api/news-categories/Delete/{child.PublicId}"))));
        Assert.Equal("TREE_MOVE_CYCLE", await ErrorCode(await staff.PutAsJsonAsync(U($"/api/news-categories/Update/{root.PublicId}"), new NewsCategoryRequest("Sự kiện", ParentId: child.Id), Json)));
    }

    [Fact]
    public async Task Comments_wait_for_moderation_and_bots_and_closed_news_are_refused()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var open = await AddNews(staff, Draft("Mở bình luận", status: IHasStatus.Active, allowComment: true));
        var closed = await AddNews(staff, Draft("Đóng bình luận", status: IHasStatus.Active));

        Assert.Equal(HttpStatusCode.Accepted, (await opac.PostAsJsonAsync(U($"/api/opac/news/{open.Slug}/comments"), new OpacCommentSubmission("Bạn đọc", "a@example.com", null, "Hay quá"), Json)).StatusCode);
        await opac.PostAsJsonAsync(U($"/api/opac/news/{open.Slug}/comments"), new OpacCommentSubmission("Bot", null, null, "spam", Website: "http://spam"), Json);
        Assert.Empty(await Read<List<OpacComment>>(await opac.GetAsync(U($"/api/opac/news/{open.Slug}/comments"))));

        var pending = await Read<CrudPage<NewsCommentDto>>(await staff.PostAsJsonAsync(U("/api/news-comments/Search"), new NewsChildSearch { NewsId = open.Id }, Json));
        var comment = Assert.Single(pending.Items); // bình luận của bot không được lưu
        Assert.Equal(IHasStatus.Inactive, comment.Status);
        await staff.PutAsJsonAsync(U("/api/news-comments/ChangeStatus"), new ChangeStatusRequest(comment.PublicId, IHasStatus.Active), Json);
        Assert.Equal("Hay quá", Assert.Single(await Read<List<OpacComment>>(await opac.GetAsync(U($"/api/opac/news/{open.Slug}/comments")))).Content);

        Assert.Equal("COMMENTS_CLOSED", await ErrorCode(await opac.PostAsJsonAsync(U($"/api/opac/news/{closed.Slug}/comments"), new OpacCommentSubmission("A", null, null, "x"), Json)));
    }

    [Fact]
    public async Task Attachments_must_be_this_tenants_uploads_and_news_content_is_isolated_between_tenants()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var news = await AddNews(staff, Draft("Có tệp", status: IHasStatus.Active));
        var good = $"/s3/media-public/{tenantId}/cms-attachment/2026/10/{new string('b', 32)}.pdf";
        var foreign = $"/s3/media-public/{tenantId + 99}/cms-attachment/2026/10/{new string('b', 32)}.pdf";

        Assert.Equal("MEDIA_URL_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/news-attachments/Add"), new NewsAttachmentRequest(news.Id, "Tệp", foreign), Json)));
        Assert.Equal("MEDIA_URL_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/news-attachments/Add"), new NewsAttachmentRequest(news.Id, "Tệp", "javascript:alert(1)"), Json)));
        Assert.True((await staff.PostAsJsonAsync(U("/api/news-attachments/Add"), new NewsAttachmentRequest(news.Id, "Thể lệ", good, 1024), Json)).IsSuccessStatusCode);
        var detail = await Read<OpacNewsDetail>(await Opac(tenantId).GetAsync(U($"/api/opac/news/{news.Slug}")));
        Assert.Equal(good, Assert.Single(detail.Attachments).Url);

        var other = NewTenant();
        Assert.Empty((await OpacNews(Opac(other))).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await Opac(other).GetAsync(U($"/api/opac/news/{news.Slug}"))).StatusCode);
        Assert.Equal(0, (await Read<CrudPage<NewsDto>>(await Staff(other).PostAsJsonAsync(U("/api/news/Search"), new NewsSearch(), Json))).TotalCount);
    }
}
