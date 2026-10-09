using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicZ3950ConfigSearchRequest : PublicSearchRequest
{
    public long? GroupId { get; set; }
}

public class PublicZ3950ConfigRepository : PublicBaseRepository<Z3950Config, PublicZ3950ConfigSearchRequest>
{
    public PublicZ3950ConfigRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    protected override IQueryable<Z3950Config> BuildQuery(PublicZ3950ConfigSearchRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            TenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }

        var q = _db.Z3950Configs.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.GroupId.HasValue) q = q.Where(x => x.GroupId == r.GroupId);
       // if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null);

        return q.OrderByDescending(x => x.Id);
    }
}

