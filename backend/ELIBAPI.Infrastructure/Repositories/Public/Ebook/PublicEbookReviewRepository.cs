using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public interface IPublicEbookReviewRepository : IPublicGenericRepository<EbookReview, PublicEbookReviewSearchRequest>
{
    Task<EbookReview> AddReviewAsync(PublicEbookReviewRequest request);
}

public class PublicEbookReviewRepository : PublicBaseRepository<EbookReview, PublicEbookReviewSearchRequest>, IPublicEbookReviewRepository
{
    public PublicEbookReviewRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    protected override IQueryable<EbookReview> BuildQuery(PublicEbookReviewSearchRequest r)
    {
        var q = _db.EbookReviews.Where(x => x.Status == 2 && x.IsDelete != 2);
        if (r.ItemId.HasValue)
        {
            var itemId = _db.EbookItems.Where(x => x.PublicId == r.ItemId.Value).Select(x => x.Id).FirstOrDefault();
            q = q.Where(x => x.ItemId == itemId);
        }
        if (r.Rating.HasValue) q = q.Where(x => x.Rating == r.Rating);
        return q.OrderByDescending(x => x.CreatedRowDate);
    }

    public async Task<EbookReview> AddReviewAsync(PublicEbookReviewRequest request)
    {
        long? itemId = null;
        if (request.ItemId.HasValue)
            itemId = await _db.EbookItems.Where(x => x.PublicId == request.ItemId.Value).Select(x => x.Id).FirstOrDefaultAsync();

        var entity = new EbookReview
        {
            ItemId = itemId,
            Rating = request.Rating,
            DisplayName = request.DisplayName,
            Content = request.Content,
            Email = request.Email,
            Status = 1,
            IsDelete = 0,
            CreatedRowDate = DateTime.Now,
            PublicId = Guid.NewGuid()
        };
        _db.EbookReviews.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }
}
