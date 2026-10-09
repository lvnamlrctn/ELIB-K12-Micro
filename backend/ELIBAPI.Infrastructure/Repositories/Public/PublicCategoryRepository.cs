using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicCategorySearchRequest : PublicSearchRequest
{
    public long? ParentId { get; set; }
    public long? Level { get; set; }
}

public class PublicCategoryRepository : PublicBaseRepository<Category, PublicCategorySearchRequest>
{
    public PublicCategoryRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    protected override IQueryable<Category> BuildQuery(PublicCategorySearchRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            TenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }
        var q = _db.Categories.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ParentId.HasValue) q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Level.HasValue) q = q.Where(x => x.Level == r.Level);
        if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null);
        return q.OrderBy(x => x.Order).ThenByDescending(x => x.Id);
    }
}

