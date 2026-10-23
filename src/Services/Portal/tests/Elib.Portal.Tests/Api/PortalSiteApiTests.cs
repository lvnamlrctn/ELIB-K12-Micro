using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Contracts.Events.Platform;
using Elib.Portal.Application;

namespace Elib.Portal.Tests.Api;

public sealed class PortalSiteApiTests(PortalApiFactory factory) : PortalTestBase(factory)
{
    private static readonly string[] MenuErrors = ["LINK_INVALID", "MENU_LINK_INVALID", "MENU_TARGET_REQUIRED", "MENU_TARGET_NOT_FOUND"];

    private static string Img(long tenantId, string name = "a") => $"/s3/media-public/{tenantId}/cms-image/2026/10/{name.PadRight(32, 'c')}.png";

    private static async Task<T> Add<T>(HttpClient staff, string path, object request) => await Read<T>(await staff.PostAsJsonAsync(U($"/api/{path}/Add"), request, Json));

    [Fact]
    public async Task Static_pages_publish_by_permission_have_unique_slugs_and_clean_html()
    {
        var tenantId = NewTenant();
        var admin = Staff(tenantId);
        var editor = Staff(tenantId, "PORTAL_PAGES:view", "PORTAL_PAGES:add", "PORTAL_PAGES:edit");
        var opac = Opac(tenantId);

        var page = await Add<PortalPageDto>(editor, "pages", new PortalPageRequest(null, "Giới thiệu thư viện", "<p>Chào</p><script>x</script>"));
        Assert.Equal(("gioi-thieu-thu-vien", "<p>Chào</p>"), (page.Slug, page.Content));
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U($"/api/opac/pages/{page.Slug}"))).StatusCode);
        Assert.Equal("PERMISSION_DENIED", await ErrorCode(await editor.PutAsJsonAsync(U("/api/pages/ChangeStatus"), new ChangeStatusRequest(page.PublicId, IHasStatus.Active), Json)));
        Assert.Equal("PAGE_SLUG_EXISTS", await ErrorCode(await editor.PostAsJsonAsync(U("/api/pages/Add"), new PortalPageRequest("gioi-thieu-thu-vien", "Trùng"), Json)));
        Assert.Equal("SLUG_INVALID", await ErrorCode(await editor.PostAsJsonAsync(U("/api/pages/Add"), new PortalPageRequest("Có dấu & ký tự lạ", "X"), Json)));

        await admin.PutAsJsonAsync(U("/api/pages/ChangeStatus"), new ChangeStatusRequest(page.PublicId, IHasStatus.Active), Json);
        Assert.Equal("Giới thiệu thư viện", (await Read<OpacPage>(await opac.GetAsync(U($"/api/opac/pages/{page.Slug}")))).Title);
    }

    [Fact]
    public async Task Menus_only_accept_safe_targets_and_resolve_to_hrefs_with_nesting()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var type = await Add<MenuTypeDto>(staff, "menu-types", new MenuTypeRequest("main", "Menu chính"));
        var page = await Add<PortalPageDto>(staff, "pages", new PortalPageRequest("lien-he-nhanh", "Liên hệ nhanh", "<p>x</p>", Status: IHasStatus.Active));
        var category = await Add<NewsCategoryDto>(staff, "news-categories", new NewsCategoryRequest("Tin"));

        foreach (var (linkType, link, target) in new (string, string?, long?)[]
                 { ("url", "javascript:alert(1)", null), ("url", "/trong", null), ("path", "https://x.example", null), ("path", "//evil.example", null), ("page", null, null), ("page", null, 999999), ("category", null, 999999) })
            Assert.Contains(await ErrorCode(await staff.PostAsJsonAsync(U("/api/menus/Add"), new MenuRequest("Xấu", type.Id, linkType, Link: link, TargetId: target), Json)),
                MenuErrors);

        var root = await Add<MenuDto>(staff, "menus", new MenuRequest("Trang chủ", type.Id, "path", Link: "/", SortOrder: 1));
        await Add<MenuDto>(staff, "menus", new MenuRequest("Giới thiệu", type.Id, "page", ParentId: root.Id, TargetId: page.Id, SortOrder: 1));
        await Add<MenuDto>(staff, "menus", new MenuRequest("Tin", type.Id, "category", ParentId: root.Id, TargetId: category.Id, SortOrder: 2));
        await Add<MenuDto>(staff, "menus", new MenuRequest("Facebook", type.Id, "url", Link: "https://facebook.com/thuvien", SortOrder: 2, OpenInNewTab: true));

        var menus = await Read<List<OpacMenu>>(await opac.GetAsync(U("/api/opac/menus")));
        var main = Assert.Single(menus, m => m.Code == "main");
        Assert.Equal(["Trang chủ", "Facebook"], main.Items.Select(i => i.Name));
        Assert.Equal(["/trang/lien-he-nhanh", $"/tin-tuc?chuyenmuc={category.Id}"], main.Items[0].Children.Select(c => c.Href));
        Assert.True(main.Items[1].NewTab);

        // Trang/chuyên mục đang được menu dùng thì không xoá được; ẩn trang thì mục menu trỏ tới nó biến mất khỏi OPAC.
        Assert.Equal("PAGE_IN_MENU", await ErrorCode(await staff.DeleteAsync(U($"/api/pages/Delete/{page.PublicId}"))));
        Assert.Equal("CATEGORY_IN_MENU", await ErrorCode(await staff.DeleteAsync(U($"/api/news-categories/Delete/{category.PublicId}"))));
        await staff.PutAsJsonAsync(U("/api/pages/ChangeStatus"), new ChangeStatusRequest(page.PublicId, IHasStatus.Inactive), Json);
        Assert.Equal(["/tin-tuc?chuyenmuc=" + category.Id], (await Read<List<OpacMenu>>(await opac.GetAsync(U("/api/opac/menus")))).Single(m => m.Code == "main").Items[0].Children.Select(c => c.Href));
        Assert.Equal("MENU_PARENT_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/menus/Add"),
            new MenuRequest("Con lạc loại", (await Add<MenuTypeDto>(staff, "menu-types", new MenuTypeRequest("footer", "Chân trang"))).Id, "path", ParentId: root.Id, Link: "/x"), Json)));
    }

    [Fact]
    public async Task Banners_ads_and_links_honour_the_display_window_status_and_media_ownership()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var today = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

        Assert.Equal("MEDIA_URL_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/banners/Add"), new BannerRequest("Xấu", Img(tenantId + 1)), Json)));
        Assert.Equal("LINK_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/banners/Add"), new BannerRequest("Xấu", Img(tenantId), Link: "javascript:alert(1)"), Json)));
        await Add<BannerDto>(staff, "banners", new BannerRequest("Hiện", Img(tenantId, "a"), Link: "https://thuvien.example", SortOrder: 2));
        await Add<BannerDto>(staff, "banners", new BannerRequest("Hẹn sau", Img(tenantId, "b"), ShowFrom: today.AddDays(1), SortOrder: 1));
        await Add<BannerDto>(staff, "banners", new BannerRequest("Đã ẩn", Img(tenantId, "d"), Status: IHasStatus.Inactive));
        Assert.Equal(["Hiện"], (await Read<List<OpacBanner>>(await opac.GetAsync(U("/api/opac/banners?position=home")))).Select(b => b.Name));
        Factory.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(["Hẹn sau", "Hiện"], (await Read<List<OpacBanner>>(await opac.GetAsync(U("/api/opac/banners")))).Select(b => b.Name));

        var group = await Add<AdsGroupDto>(staff, "ads-groups", new AdsGroupRequest("Cột phải", Width: 300, Height: 250));
        await Add<AdsDto>(staff, "ads", new AdsRequest(group.Id, "QC 1", Img(tenantId, "e"), Link: "https://qc.example"));
        Assert.Equal("ADS_GROUP_HAS_ADS", await ErrorCode(await staff.DeleteAsync(U($"/api/ads-groups/Delete/{group.PublicId}"))));
        var home = await Read<OpacHome>(await opac.GetAsync(U("/api/opac/home")));
        var adGroup = Assert.Single(home.AdGroups);
        Assert.Equal((300, 250, "QC 1"), (adGroup.Width, adGroup.Height, Assert.Single(adGroup.Ads).Name)); // quảng cáo kế thừa kích thước nhóm

        var links = await Add<LinkGroupDto>(staff, "link-groups", new LinkGroupRequest("Đối tác"));
        Assert.Empty(await Read<List<OpacLinkGroup>>(await opac.GetAsync(U("/api/opac/links")))); // nhóm rỗng không hiện
        Assert.Equal("LINK_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/links/Add"), new PortalLinkRequest(links.Id, "Xấu", "javascript:alert(1)"), Json)));
        await Add<PortalLinkDto>(staff, "links", new PortalLinkRequest(links.Id, "Bộ GD&ĐT", "https://moet.gov.vn", Images: Img(tenantId, "f")));
        Assert.Equal("Bộ GD&ĐT", Assert.Single(Assert.Single(await Read<List<OpacLinkGroup>>(await opac.GetAsync(U("/api/opac/links")))).Links).Name);
        Assert.Equal("LINK_GROUP_HAS_LINKS", await ErrorCode(await staff.DeleteAsync(U($"/api/link-groups/Delete/{links.PublicId}"))));
    }

    [Fact]
    public async Task Gallery_only_embeds_youtube_and_vimeo_by_video_id_and_hidden_albums_stay_hidden()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);

        foreach (var bad in new[] { "https://evil.example/watch?v=dQw4w9WgXcQ", "javascript:alert(1)", "https://www.youtube.com/watch?v=<script>", "https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ", "not a url" })
            Assert.Equal("VIDEO_LINK_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/videos/Add"), new VideoRequest("Xấu", bad), Json)));
        var yt = await Add<VideoDto>(staff, "videos", new VideoRequest("Giới thiệu", "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10"));
        var shortLink = await Add<VideoDto>(staff, "videos", new VideoRequest("Rút gọn", "https://youtu.be/dQw4w9WgXcR", SortOrder: 2));
        var vimeo = await Add<VideoDto>(staff, "videos", new VideoRequest("Vimeo", "https://vimeo.com/123456789", SortOrder: 3));
        Assert.Equal("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", yt.EmbedUrl);
        Assert.Equal("dQw4w9WgXcR", shortLink.VideoId);
        Assert.Equal("https://player.vimeo.com/video/123456789", vimeo.EmbedUrl);
        Assert.Equal(3, (await Read<List<OpacVideo>>(await opac.GetAsync(U("/api/opac/videos")))).Count);

        var album = await Add<PhotoAlbumDto>(staff, "photo-albums", new PhotoAlbumRequest("Hội sách", Image: Img(tenantId, "1")));
        var hidden = await Add<PhotoAlbumDto>(staff, "photo-albums", new PhotoAlbumRequest("Nháp", Status: IHasStatus.Inactive));
        await Add<PhotoDto>(staff, "photos", new PhotoRequest(album.Id, "Ảnh 1", Img(tenantId, "2")));
        await Add<PhotoDto>(staff, "photos", new PhotoRequest(album.Id, "Ảnh ẩn", Img(tenantId, "3"), Status: IHasStatus.Inactive));
        Assert.Equal("MEDIA_URL_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/photos/Add"), new PhotoRequest(album.Id, "Xấu", "https://tracker.example/p.png"), Json)));

        var listed = Assert.Single(await Read<List<OpacAlbum>>(await opac.GetAsync(U("/api/opac/albums"))));
        Assert.Equal((album.PublicId, 1), (listed.PublicId, listed.PhotoCount));
        Assert.Equal("Ảnh 1", Assert.Single(await Read<List<OpacPhoto>>(await opac.GetAsync(U($"/api/opac/albums/{album.PublicId}/photos")))).Name);
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U($"/api/opac/albums/{hidden.PublicId}/photos"))).StatusCode);
        Assert.Equal("ALBUM_HAS_PHOTOS", await ErrorCode(await staff.DeleteAsync(U($"/api/photo-albums/Delete/{album.PublicId}"))));
    }

    [Fact]
    public async Task Contacts_notify_the_group_mailbox_ignore_bots_and_are_handled_by_staff()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var group = await Add<ContactGroupDto>(staff, "contact-groups", new ContactGroupRequest("Hỏi đáp mượn sách", "muonsach@thuvien.example"));
        var quiet = await Add<ContactGroupDto>(staff, "contact-groups", new ContactGroupRequest("Góp ý", null));

        Assert.Equal("EMAIL_INVALID", await ErrorCode(await opac.PostAsJsonAsync(U("/api/opac/contacts"), new OpacContactSubmission(group.Id, "A", "khong-co-a-cong", null, null, null, "Xin chào"), Json)));
        Assert.Equal("CONTACT_GROUP_NOT_FOUND", await ErrorCode(await opac.PostAsJsonAsync(U("/api/opac/contacts"), new OpacContactSubmission(999999, "A", "a@example.com", null, null, null, "Xin chào"), Json)));
        await opac.PostAsJsonAsync(U("/api/opac/contacts"), new OpacContactSubmission(group.Id, "Bot", "bot@example.com", null, null, null, "spam", Website: "http://spam"), Json);
        Assert.Equal(HttpStatusCode.Accepted, (await opac.PostAsJsonAsync(U("/api/opac/contacts"), new OpacContactSubmission(group.Id, "Nguyễn Văn A", "a@example.com", "0901234567", null, "Mượn sách", "Em muốn hỏi về thẻ"), Json)).StatusCode);
        await opac.PostAsJsonAsync(U("/api/opac/contacts"), new OpacContactSubmission(quiet.Id, "B", "b@example.com", null, null, null, "Góp ý nhỏ"), Json);

        var notes = Factory.PublishedOf<NotificationRequested>().Where(n => n.TenantId == tenantId && n.TemplateCode == PortalTemplates.ContactReceived).ToList();
        var note = Assert.Single(notes); // chỉ nhóm có email nhận mới báo; bot không sinh liên hệ
        Assert.Equal("muonsach@thuvien.example", note.Recipient.Email);
        Assert.Equal("Em muốn hỏi về thẻ", note.Data["contact_content"]);

        var all = await Read<CrudPage<ContactDto>>(await staff.PostAsJsonAsync(U("/api/contacts/Search"), new ContactSearch(), Json));
        Assert.Equal(2, all.TotalCount);
        var contact = all.Items.Single(c => c.Name == "Nguyễn Văn A");
        var handled = await Read<ContactDto>(await staff.PostAsJsonAsync(U($"/api/contacts/{contact.PublicId}/Handle"), new ContactRequest("Đã gọi lại"), Json));
        Assert.Equal((IHasStatus.Active, "Đã gọi lại"), (handled.Status, handled.HandleNote));
        Assert.Equal(1, (await Read<CrudPage<ContactDto>>(await staff.PostAsJsonAsync(U("/api/contacts/Search"), new ContactSearch { Status = IHasStatus.Inactive }, Json))).TotalCount);
        Assert.Equal("CONTACT_GROUP_HAS_CONTACTS", await ErrorCode(await staff.DeleteAsync(U($"/api/contact-groups/Delete/{group.PublicId}"))));
    }

    [Fact]
    public async Task The_visit_counter_accumulates_per_day_and_is_scoped_to_the_tenant()
    {
        var tenantId = NewTenant();
        var opac = Opac(tenantId);
        await opac.PostAsync(U("/api/opac/counter/hit"), null);
        await opac.PostAsync(U("/api/opac/counter/hit"), null);
        Factory.Clock.Advance(TimeSpan.FromDays(1));
        var summary = await Read<CounterSummary>(await opac.PostAsync(U("/api/opac/counter/hit"), null));
        Assert.Equal((3, 1, 3), (summary.Total, summary.Today, summary.Last7Days));
        Assert.Equal(3, (await Read<CounterSummary>(await Staff(tenantId, "PORTAL_COUNTER:view").GetAsync(U("/api/counter")))).Total);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "PORTAL_NEWS:view").GetAsync(U("/api/counter"))).StatusCode);
        Assert.Equal(0, (await Read<CounterSummary>(await Opac(NewTenant()).GetAsync(U("/api/opac/counter")))).Total);
    }
}
