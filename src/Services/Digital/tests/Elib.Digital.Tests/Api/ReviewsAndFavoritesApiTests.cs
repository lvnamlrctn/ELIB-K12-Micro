using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
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

        await staff.PutAsJsonAsync(U("/api/reviews/ChangeStatus"), new ChangeStatusRequest(review.PublicId, IHasStatus.Active), Json);
        published = await Read<List<OpacReviewSummary>>(await opac.GetAsync(U($"/api/opac/items/{item.PublicId}/reviews")));
        Assert.Equal("Bạn đọc A", Assert.Single(published).DisplayName);
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
