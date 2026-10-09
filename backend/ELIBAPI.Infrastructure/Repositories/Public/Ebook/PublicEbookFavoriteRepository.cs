using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public interface IPublicEbookFavoriteRepository : IPublicGenericRepository<EbookFavorite, PublicEbookFavoriteSearchRequest>
{
    Task<EbookFavorite> AddFavoriteAsync(PublicEbookFavoriteRequest request);
    Task<bool> RemoveFavoriteAsync(PublicEbookFavoriteRequest request);
    Task<PagedResult<PublicEbookFavoriteResponse>> SearchWithDetailsAsync(PublicEbookFavoriteSearchRequest request);
    Task<List<PublicEbookFavoriteResponse>> SearchAllWithDetailsAsync(PublicEbookFavoriteSearchRequest request);
}

public class PublicEbookFavoriteRepository : PublicBaseRepository<EbookFavorite, PublicEbookFavoriteSearchRequest>, IPublicEbookFavoriteRepository
{
    public PublicEbookFavoriteRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    protected override IQueryable<EbookFavorite> BuildQuery(PublicEbookFavoriteSearchRequest r)
    {
        var q = _db.EbookFavorites.Where(x => x.IsDelete != 2);

        if (r.TenantId != Guid.Empty)
        {
            var tenantId = _db.Tenants
                .Where(t => t.PublicId == r.TenantId && t.IsDelete != 2)
                .Select(t => t.Id).FirstOrDefault();
            if (tenantId > 0) q = q.Where(x => x.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(r.ReaderId))
        {
            var readerId = ResolveReaderId(r.ReaderId, null);
            q = q.Where(x => x.ReaderId == readerId);
        }

        if (r.ItemId.HasValue)
        {
            var itemId = _db.EbookItems
                .Where(x => x.PublicId == r.ItemId.Value)
                .Select(x => x.Id).FirstOrDefault();
            q = q.Where(x => x.ItemId == itemId);
        }

        return q.OrderByDescending(x => x.CreatedRowDate);
    }

    public async Task<EbookFavorite> AddFavoriteAsync(PublicEbookFavoriteRequest request)
    {
        long? itemId = null;
        if (request.ItemId.HasValue)
            itemId = await _db.EbookItems
                .Where(x => x.PublicId == request.ItemId.Value)
                .Select(x => x.Id).FirstOrDefaultAsync();

        var readerId = await ResolveReaderIdAsync(request.ReaderId, request.Cardnumber);

        long? tenantId = null;
        if (request.TenantId.HasValue)
            tenantId = await _db.Tenants
                .Where(t => t.PublicId == request.TenantId.Value && t.IsDelete != 2)
                .Select(t => t.Id).FirstOrDefaultAsync();

        var existing = await _db.EbookFavorites
            .FirstOrDefaultAsync(x => x.ReaderId == readerId && x.ItemId == itemId && x.IsDelete != 2);

        if (existing != null)
            return existing;

        var entity = new EbookFavorite
        {
            ReaderId       = readerId,
            Cardnumber     = request.Cardnumber?.Trim(),
            ItemId         = itemId,
            TenantId       = tenantId,
            IsDelete       = 0,
            CreatedRowDate = DateTime.Now,
            PublicId       = Guid.NewGuid()
        };
        _db.EbookFavorites.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> RemoveFavoriteAsync(PublicEbookFavoriteRequest request)
    {
        long? itemId = null;
        if (request.ItemId.HasValue)
            itemId = await _db.EbookItems
                .Where(x => x.PublicId == request.ItemId.Value)
                .Select(x => x.Id).FirstOrDefaultAsync();

        var readerId = await ResolveReaderIdAsync(request.ReaderId, request.Cardnumber);

        var entity = await _db.EbookFavorites
            .FirstOrDefaultAsync(x => x.ReaderId == readerId && x.ItemId == itemId && x.IsDelete != 2);

        if (entity == null) return false;

        entity.IsDelete       = 2;
        entity.UpdatedRowDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return true;
    }

    private IQueryable<PublicEbookFavoriteResponse> ProjectWithDetails(IQueryable<EbookFavorite> q)
        => from fav in q
           join item in _db.EbookItems on fav.ItemId equals item.Id into items
           from item in items.DefaultIfEmpty()
           join xml in _db.Set<EbookItemXml>() on item.Id equals xml.Id into xmls
           from xml in xmls.DefaultIfEmpty()
           select new PublicEbookFavoriteResponse
           {
               Id             = fav.Id,
               ItemId         = item != null ? item.PublicId : (Guid?)null,
               ReaderId       = fav.ReaderId,
               ItemTitle      = xml != null ? xml.Title : null,
               Author         = xml != null ? xml.Author : null,
               PublishDate    = xml != null ? xml.PublishDate : null,
               Images         = item != null ? item.Images : null,
               CreatedRowDate = fav.CreatedRowDate
           };

    private IQueryable<PublicEbookFavoriteResponse> ApplyKeywordFilter(IQueryable<PublicEbookFavoriteResponse> q, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return q;
        var kw = keyword.Trim();
        return q.Where(x =>
            (x.ItemTitle != null && x.ItemTitle.Contains(kw))
            || (x.Author != null && x.Author.Contains(kw)));
    }

    public async Task<PagedResult<PublicEbookFavoriteResponse>> SearchWithDetailsAsync(PublicEbookFavoriteSearchRequest request)
    {
        var projected = ApplyKeywordFilter(ProjectWithDetails(BuildQuery(request)), request.Keyword);

        var total = await projected.CountAsync();
        var items2 = await projected
            .Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<PublicEbookFavoriteResponse>
        {
            Items      = items2,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize
        };
    }

    public async Task<List<PublicEbookFavoriteResponse>> SearchAllWithDetailsAsync(PublicEbookFavoriteSearchRequest request)
    {
        var projected = ApplyKeywordFilter(ProjectWithDetails(BuildQuery(request)), request.Keyword);
        return await projected.ToListAsync();
    }

    private long? ResolveReaderId(string? readerIdStr, string? cardnumber)
    {
        if (!string.IsNullOrWhiteSpace(readerIdStr))
        {
            if (Guid.TryParse(readerIdStr, out var guid))
                return _db.Readers.Where(x => x.PublicId == guid).Select(x => x.Id).FirstOrDefault();

            return _db.Readers.Where(x => x.Cardno!.ToLower() == readerIdStr.Trim().ToLower()).Select(x => x.Id).FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(cardnumber))
            return _db.Readers.Where(x => x.Cardno!.ToLower() == cardnumber.Trim().ToLower()).Select(x => x.Id).FirstOrDefault();

        return null;
    }

    private async Task<long?> ResolveReaderIdAsync(string? readerIdStr, string? cardnumber)
    {
        if (!string.IsNullOrWhiteSpace(readerIdStr))
        {
            if (Guid.TryParse(readerIdStr, out var guid))
                return await _db.Readers.Where(x => x.PublicId == guid).Select(x => x.Id).FirstOrDefaultAsync();

            return await _db.Readers.Where(x => x.Cardno!.ToLower() == readerIdStr.Trim().ToLower()).Select(x => x.Id).FirstOrDefaultAsync();
        }

        if (!string.IsNullOrWhiteSpace(cardnumber))
            return await _db.Readers.Where(x => x.Cardno!.ToLower() == cardnumber.Trim().ToLower()).Select(x => x.Id).FirstOrDefaultAsync();

        return null;
    }
}
