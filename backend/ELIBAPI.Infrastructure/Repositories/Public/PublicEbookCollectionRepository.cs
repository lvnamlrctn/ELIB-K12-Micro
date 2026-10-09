using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicEbookCollectionSearchRequest : PublicSearchRequest { }

public interface IPublicEbookCollectionRepository : IPublicGenericRepository<EbookCollection, PublicEbookCollectionSearchRequest>
{
    Task<Dictionary<long, int>> GetCollectionItemCountsAsync(List<long> collectionIds);
}

public class PublicEbookCollectionRepository : PublicBaseRepository<EbookCollection, PublicEbookCollectionSearchRequest>, IPublicEbookCollectionRepository
{
    public PublicEbookCollectionRepository(ELIBAPIDbContext db, IMemoryCache cache, ELIBAPI.Core.Interfaces.ICacheInvalidator invalidator) : base(db, cache, invalidator: invalidator) { }

    protected override IQueryable<EbookCollection> BuildQuery(PublicEbookCollectionSearchRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            TenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }
        var q = _db.EbookCollections.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null || x.Share > 0);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    public async Task<Dictionary<long, int>> GetCollectionItemCountsAsync(List<long> collectionIds)
    {
        return await _db.EbookItems
            .Where(x => x.CollectionId.HasValue && collectionIds.Contains(x.CollectionId.Value) && x.IsDelete != 2)
            .GroupBy(x => x.CollectionId)
            .Select(g => new { CollectionId = g.Key ?? 0, Count = g.Count() })
            .ToDictionaryAsync(x => x.CollectionId, x => x.Count);
    }
}

