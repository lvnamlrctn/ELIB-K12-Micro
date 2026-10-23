using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Testing;
using Elib.Portal.Application;

namespace Elib.Portal.Tests.Api;

/// <summary>Đường bạn đọc đăng nhập (chuyên mục yêu cầu đăng nhập), đếm lượt nhấp quảng cáo và bộ đếm theo ngày.</summary>
public sealed class PortalMemberApiTests(PortalApiFactory factory) : PortalTestBase(factory)
{
    private static string Img(long tenantId) => $"/s3/media-public/{tenantId}/cms-image/2026/10/{"d".PadRight(32, 'c')}.png";

    private HttpClient Member(long tenantId) => Factory.CreateClient().WithClaims(TestClaims.Reader(tenantId, "TV001", Guid.NewGuid()));

    private static async Task<T> Add<T>(HttpClient staff, string path, object body) => await Read<T>(await staff.PostAsJsonAsync(U($"/api/{path}/Add"), body, Json));

    [Fact]
    public async Task Members_see_login_only_news_but_anonymous_visitors_never_do_through_any_endpoint()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var member = Member(tenantId);
        var secret = await Add<NewsCategoryDto>(staff, "news-categories", new NewsCategoryRequest("Nội bộ", RequiresLogin: true));
        var news = await Add<NewsDto>(staff, "news", new NewsRequest("Thông báo nội bộ", Content: "<p>Chỉ cho bạn đọc</p>", CategoryId: secret.Id, Status: IHasStatus.Active, AllowComment: true));
        await Add<NewsDto>(staff, "news", new NewsRequest("Tin chung", Status: IHasStatus.Active));
        var slug = news.Slug;

        // Ẩn danh: không thấy ở danh sách, chi tiết, chuyên mục, bình luận, lượt xem.
        Assert.Equal(["Tin chung"], (await Read<OpacNewsPage>(await opac.PostAsJsonAsync(U("/api/opac/news/Search"), new OpacNewsSearch(), Json))).Items.Select(i => i.Title));
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U($"/api/opac/news/{slug}"))).StatusCode);
        Assert.Empty(await Read<List<OpacComment>>(await opac.GetAsync(U($"/api/opac/news/{slug}/comments"))));
        Assert.Equal(HttpStatusCode.NotFound, (await opac.PostAsJsonAsync(U($"/api/opac/news/{slug}/comments"), new OpacCommentSubmission("A", null, null, "x", null), Json)).StatusCode);
        await opac.PostAsync(U($"/api/opac/news/{slug}/view"), null);
        Assert.DoesNotContain(await Read<List<OpacCategory>>(await opac.GetAsync(U("/api/opac/categories"))), c => c.Id == secret.Id);

        // Bạn đọc đăng nhập (đường /api/reader): thấy đủ, và lượt xem anonymous ở trên không được tính.
        Assert.Equal(2, (await Read<OpacNewsPage>(await member.PostAsJsonAsync(U("/api/reader/news/Search"), new OpacNewsSearch(), Json))).Items.Count);
        Assert.Equal(0, (await Read<OpacNewsDetail>(await member.GetAsync(U($"/api/reader/news/{slug}")))).TotalView);
        await member.PostAsync(U($"/api/reader/news/{slug}/view"), null);
        Assert.Equal(1, (await Read<OpacNewsDetail>(await member.GetAsync(U($"/api/reader/news/{slug}")))).TotalView);
        Assert.Contains(await Read<List<OpacCategory>>(await member.GetAsync(U("/api/reader/categories"))), c => c.Id == secret.Id);
        Assert.Equal(HttpStatusCode.Accepted, (await member.PostAsJsonAsync(U($"/api/reader/news/{slug}/comments"), new OpacCommentSubmission("Bạn đọc", null, null, "Cảm ơn", null), Json)).StatusCode);
    }

    [Fact]
    public async Task The_member_route_rejects_anonymous_and_staff_tokens()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Opac(tenantId).PostAsJsonAsync(U("/api/reader/news/Search"), new OpacNewsSearch(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId).PostAsJsonAsync(U("/api/reader/news/Search"), new OpacNewsSearch(), Json)).StatusCode);
    }

    [Fact]
    public async Task Ad_clicks_count_only_visible_ads_and_survive_an_edit()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var opac = Opac(tenantId);
        var group = await Add<AdsGroupDto>(staff, "ads-groups", new AdsGroupRequest("Cột phải"));
        var ad = await Add<AdsDto>(staff, "ads", new AdsRequest(group.Id, "QC", Img(tenantId), Link: "https://qc.example"));

        Assert.Equal(HttpStatusCode.NoContent, (await opac.PostAsync(U($"/api/opac/ads/{ad.PublicId}/click"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await opac.PostAsync(U($"/api/opac/ads/{ad.PublicId}/click"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await opac.PostAsync(U($"/api/opac/ads/{Guid.NewGuid()}/click"), null)).StatusCode); // không có: im lặng
        Assert.Equal(2, (await Read<AdsDto>(await staff.GetAsync(U($"/api/ads/GetById/{ad.PublicId}")))).Clicks);

        // Sửa quảng cáo không đặt lại số lượt nhấp.
        await Read<AdsDto>(await staff.PutAsJsonAsync(U($"/api/ads/Update/{ad.PublicId}"), new AdsRequest(group.Id, "QC mới", Img(tenantId), Link: "https://qc.example"), Json));
        Assert.Equal(2, (await Read<AdsDto>(await staff.GetAsync(U($"/api/ads/GetById/{ad.PublicId}")))).Clicks);

        // Quảng cáo đã ẩn: không tính.
        await staff.PutAsJsonAsync(U("/api/ads/ChangeStatus"), new { publicId = ad.PublicId, status = IHasStatus.Inactive }, Json);
        await opac.PostAsync(U($"/api/opac/ads/{ad.PublicId}/click"), null);
        Assert.Equal(2, (await Read<AdsDto>(await staff.GetAsync(U($"/api/ads/GetById/{ad.PublicId}")))).Clicks);

        // Bật lại quảng cáo nhưng tắt nhóm của nó: vẫn không tính.
        await staff.PutAsJsonAsync(U("/api/ads/ChangeStatus"), new { publicId = ad.PublicId, status = IHasStatus.Active }, Json);
        await opac.PostAsync(U($"/api/opac/ads/{ad.PublicId}/click"), null);
        Assert.Equal(3, (await Read<AdsDto>(await staff.GetAsync(U($"/api/ads/GetById/{ad.PublicId}")))).Clicks);
        await staff.PutAsJsonAsync(U("/api/ads-groups/ChangeStatus"), new { publicId = group.PublicId, status = IHasStatus.Inactive }, Json);
        await opac.PostAsync(U($"/api/opac/ads/{ad.PublicId}/click"), null);
        Assert.Equal(3, (await Read<AdsDto>(await staff.GetAsync(U($"/api/ads/GetById/{ad.PublicId}")))).Clicks);

        // Hết hạn hiển thị (ShowTo là hôm qua): không tính.
        await staff.PutAsJsonAsync(U("/api/ads-groups/ChangeStatus"), new { publicId = group.PublicId, status = IHasStatus.Active }, Json);
        var yesterday = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(-1);
        await Read<AdsDto>(await staff.PutAsJsonAsync(U($"/api/ads/Update/{ad.PublicId}"), new AdsRequest(group.Id, "QC mới", Img(tenantId), Link: "https://qc.example", ShowTo: yesterday), Json));
        await opac.PostAsync(U($"/api/opac/ads/{ad.PublicId}/click"), null);
        Assert.Equal(3, (await Read<AdsDto>(await staff.GetAsync(U($"/api/ads/GetById/{ad.PublicId}")))).Clicks);
    }

    [Fact]
    public async Task Counter_days_are_zero_filled_in_vietnam_calendar_days_and_need_permission()
    {
        var tenantId = NewTenant();
        var opac = Opac(tenantId);
        await opac.PostAsync(U("/api/opac/counter/hit"), null);
        await opac.PostAsync(U("/api/opac/counter/hit"), null);

        var days = await Read<List<CounterDayPoint>>(await Staff(tenantId, "PORTAL_COUNTER:view").GetAsync(U("/api/counter/days?days=7")));
        Assert.Equal(7, days.Count);
        Assert.Equal(days.Select(d => d.Day).Order(), days.Select(d => d.Day)); // cũ → mới
        Assert.Equal(2, days[^1].Hits);
        Assert.All(days[..^1], d => Assert.Equal(0, d.Hits));
        Assert.Equal(366, (await Read<List<CounterDayPoint>>(await Staff(tenantId).GetAsync(U("/api/counter/days?days=99999")))).Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "PORTAL_NEWS:view").GetAsync(U("/api/counter/days"))).StatusCode);
    }
}
