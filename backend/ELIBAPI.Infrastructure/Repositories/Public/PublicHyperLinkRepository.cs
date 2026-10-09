using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using ELIBAPI.Core.Interfaces;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicHyperLinkSearchRequest : PublicSearchRequest
{
    public Guid? LinkGroupId { get; set; }
}

public interface IPublicHyperLinkRepository : IPublicGenericRepository<PublicHyperLinkResponse, PublicHyperLinkSearchRequest>
{
}

public class PublicHyperLinkRepository : PublicBaseRepository<PublicHyperLinkResponse, PublicHyperLinkSearchRequest>, IPublicHyperLinkRepository
{
    public PublicHyperLinkRepository(ELIBAPIDbContext db, IMemoryCache cache, ICacheInvalidator invalidator) : base(db, cache, invalidator: invalidator) { }

    protected override IQueryable<PublicHyperLinkResponse> BuildQuery(PublicHyperLinkSearchRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            TenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }

        long? linkGroupId = null;
        if (r.LinkGroupId.HasValue && r.LinkGroupId != Guid.Empty)
        {
            linkGroupId = _db.LinkGroups
                .Where(x => x.PublicId == r.LinkGroupId && x.IsDelete != 2)
                .Select(x => (long?)x.Id)
                .FirstOrDefault();
        }

        var q = _db.Links.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null);
        if (linkGroupId.HasValue && linkGroupId > 0) q = q.Where(x => x.LinkGroupId == linkGroupId);

        return q.OrderByDescending(x => x.Id)
                .Select(x => new PublicHyperLinkResponse
                {
                    PublicId = x.PublicId,
                    Name = x.Name,
                    LinkUrl = x.LinkUrl,
                    Description = x.Description,
                    Images = x.Images,
                    Status = x.Status,
                    LinkGroupPublicId = _db.LinkGroups.Where(g => g.Id == x.LinkGroupId).Select(g => (Guid?)g.PublicId).FirstOrDefault()
                });
    }
}

