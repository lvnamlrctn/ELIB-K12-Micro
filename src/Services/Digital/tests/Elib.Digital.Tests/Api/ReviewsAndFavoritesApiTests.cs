using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Api;
using Elib.Digital.Application;
using Elib.Digital.Domain;

namespace Elib.Digital.Tests.Api;

public sealed class ReviewsAndFavoritesApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private async Task<(long TenantId, HttpClient Staff, EbookItemDto Item)> SetupAsync()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var collection = await Read<EbookCollectionDto>(await staff.PostAsJsonAsync(U("/api/collections/Add"), new EbookCollectionRequest("Văn học"), Json));
        var item = await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("Số đỏ", CollectionId: collection.Id), Json));
        await staff.PutAsJsonAsync(U("/api/items/ChangeStatus"), new ChangeStatusRequest(item.PublicId, IHasStatus.Active), Json);
        return (tenantId, staff, item);
    }

    [Fact]
    public async Task Anonymous_review_is_pending_until_staff_approves_it()
    {
        var (tenantId, staff, item) = await SetupAsync();
        var opac = Opac(tenantId);

        await opac.PostAsJsonAsync(U($"/api/opac/items/{item.PublicId}/reviews"), new { rating = 5, displayName = "Bạn đọc A", content = "Hay lắm" }, Json);
        var published = await Read<List<OpacReviewSummary>>(await opac.GetAsync(U($"/api/opac/items/{item.PublicId}/reviews")));
        Assert.Empty(published);

        var pending = await Read<CrudPage<EbookReviewDto>>(await staff.PostAsJsonAsync(U("/api/reviews/Search"), new EbookReviewSearch(), Json));
        var review = Assert.Single(pending.Items);
        Assert.Equal(item.PublicId, review.EbookItemPublicId);
        Assert.Equal(item.Title, review.ItemTitle);

        // ChangeStatus chung của CRUD không còn duyệt được (chỉ cần quyền edit) — phải qua Approve.
        Assert.Equal("REVIEW_USE_APPROVE", (await Error(await staff.PutAsJsonAsync(U("/api/reviews/ChangeStatus"), new ChangeStatusRequest(review.PublicId, IHasStatus.Active), Json))).Code);
        Assert.Empty(await Read<List<OpacReviewSummary>>(await opac.GetAsync(U($"/api/opac/items/{item.PublicId}/reviews"))));
        var editor = Staff(tenantId, "DIGITAL_REVIEWS:view", "DIGITAL_REVIEWS:edit");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await editor.PostAsync(U($"/api/reviews/{review.PublicId}/Approve"), null)).StatusCode);
        await staff.PostAsync(U($"/api/reviews/{review.PublicId}/Approve"), null);
        published = await Read<List<OpacReviewSummary>>(await opac.GetAsync(U($"/api/opac/items/{item.PublicId}/reviews")));
        Assert.Equal("Bạn đọc A", Assert.Single(published).DisplayName);
    }

    [Fact]
    public async Task Approve_publishes_the_review_and_tells_the_author_once()
    {
        var (tenantId, staff, item) = await SetupAsync();
        var opac = Opac(tenantId);
        await opac.PostAsJsonAsync(U($"/api/opac/items/{item.PublicId}/reviews"), new { rating = 4, displayName = "Bạn đọc B", content = "Ổn", email = "b@example.com" }, Json);
        var review = Assert.Single((await Read<CrudPage<EbookReviewDto>>(await staff.PostAsJsonAsync(U("/api/reviews/Search"), new EbookReviewSearch(), Json))).Items);

        var approved = await Read<EbookReviewDto>(await staff.PostAsync(U($"/api/reviews/{review.PublicId}/Approve"), null));
        Assert.Equal(IHasStatus.Active, approved.Status);
        Assert.NotNull(approved.ReviewedAt);
        Assert.Single(await Read<List<OpacReviewSummary>>(await opac.GetAsync(U($"/api/opac/items/{item.PublicId}/reviews"))));

        await staff.PostAsync(U($"/api/reviews/{review.PublicId}/Approve"), null); // gọi lại: không báo lần hai
        var notes = Factory.PublishedOf<NotificationRequested>().Where(n => n.TemplateCode == EbookTemplates.ReviewApproved && n.Recipient.Email == "b@example.com").ToList();
        Assert.Single(notes);
    }

    [Fact]
    public async Task Reject_needs_a_reason_hides_the_review_and_records_why()
    {
        var (tenantId, staff, item) = await SetupAsync();
        var opac = Opac(tenantId);
        await opac.PostAsJsonAsync(U($"/api/opac/items/{item.PublicId}/reviews"), new { rating = 1, displayName = "Spam", content = "mua hàng tại ...", email = "spam@example.com" }, Json);
        var review = Assert.Single((await Read<CrudPage<EbookReviewDto>>(await staff.PostAsJsonAsync(U("/api/reviews/Search"), new EbookReviewSearch(), Json))).Items);

        var noReason = await staff.PostAsJsonAsync(U($"/api/reviews/{review.PublicId}/Reject"), new RejectReviewRequest("  "), Json);
        Assert.Equal("REVIEW_REJECT_REASON_REQUIRED", (await Error(noReason)).Code);

        var rejected = await Read<EbookReviewDto>(await staff.PostAsJsonAsync(U($"/api/reviews/{review.PublicId}/Reject"), new RejectReviewRequest("Quảng cáo"), Json));
        Assert.Equal("Quảng cáo", rejected.RejectReason);
        Assert.NotNull(rejected.RejectedAt);
        Assert.Empty(await Read<List<OpacReviewSummary>>(await opac.GetAsync(U($"/api/opac/items/{item.PublicId}/reviews"))));
        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == EbookTemplates.ReviewRejected && n.Data["reason"] == "Quảng cáo");

        // Duyệt lại sau khi từ chối: xoá dấu từ chối.
        var again = await Read<EbookReviewDto>(await staff.PostAsync(U($"/api/reviews/{review.PublicId}/Approve"), null));
        Assert.Null(again.RejectedAt);
        Assert.Null(again.RejectReason);
    }

    [Fact]
    public async Task Honeypot_and_pending_caps_stop_spam()
    {
        var (tenantId, staff, item) = await SetupAsync();
        var opac = Opac(tenantId);
        var url = U($"/api/opac/items/{item.PublicId}/reviews");

        await opac.PostAsJsonAsync(url, new { rating = 5, displayName = "Bot", content = "x", website = "http://spam" }, Json);
        Assert.Empty((await Read<CrudPage<EbookReviewDto>>(await staff.PostAsJsonAsync(U("/api/reviews/Search"), new EbookReviewSearch(), Json))).Items);

        for (var i = 0; i < 3; i++) Assert.True((await opac.PostAsJsonAsync(url, new { rating = 5, displayName = "Một người", email = "same@example.com" }, Json)).IsSuccessStatusCode);
        var fourth = await opac.PostAsJsonAsync(url, new { rating = 5, displayName = "Một người", email = "same@example.com" }, Json);
        Assert.Equal("REVIEW_TOO_MANY_PENDING", (await Error(fourth)).Code);
    }

    [Fact]
    public async Task Rating_outside_one_to_five_is_rejected()
    {
        var (tenantId, _, item) = await SetupAsync();
        var opac = Opac(tenantId);
        var response = await opac.PostAsJsonAsync(U($"/api/opac/items/{item.PublicId}/reviews"), new { rating = 7, displayName = "X" }, Json);
        Assert.Equal("RATING_INVALID", (await Error(response)).Code);
    }

    [Fact]
    public async Task Favorites_are_scoped_to_the_reader_and_adding_twice_is_idempotent()
    {
        var (tenantId, _, item) = await SetupAsync();
        var readerA = ReaderClient(tenantId, "FAV-A", Guid.CreateVersion7());
        var readerB = ReaderClient(tenantId, "FAV-B", Guid.CreateVersion7());

        await readerA.PostAsync(U($"/api/reader/favorites/{item.PublicId}"), null);
        await readerA.PostAsync(U($"/api/reader/favorites/{item.PublicId}"), null); // idempotent, không tạo trùng

        var listA = await Read<List<FavoriteSummary>>(await readerA.GetAsync(U("/api/reader/favorites")));
        Assert.Equal(item.PublicId, Assert.Single(listA).EbookItemPublicId);

        var listB = await Read<List<FavoriteSummary>>(await readerB.GetAsync(U("/api/reader/favorites")));
        Assert.Empty(listB);

        await readerA.DeleteAsync(U($"/api/reader/favorites/{item.PublicId}"));
        Assert.Empty(await Read<List<FavoriteSummary>>(await readerA.GetAsync(U("/api/reader/favorites"))));
    }
}
