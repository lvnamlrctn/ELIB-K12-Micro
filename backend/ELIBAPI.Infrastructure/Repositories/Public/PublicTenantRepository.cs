using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicTenantSearchRequest : PublicSearchRequest { }

public interface IPublicTenantRepository : IPublicGenericRepository<Tenant, PublicTenantSearchRequest>
{
    Task<Tenant?> GetByHostAsync(string host);
}

public class PublicTenantRepository : PublicBaseRepository<Tenant, PublicTenantSearchRequest>, IPublicTenantRepository
{
    private readonly IMemoryCache _cache;

    public PublicTenantRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache)
    {
        _cache = cache;
    }

    public async Task<Tenant?> GetByHostAsync(string host)
    {
        var key = $"Tenant:ByHost:{host.ToLowerInvariant()}";
        if (_cache.TryGetValue(key, out Tenant? cached) && cached != null)
            return cached;

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(x => x.IsDelete != 2 && x.Host == host);

        if (tenant == null && host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            tenant = await _db.Tenants
                .FirstOrDefaultAsync(x => x.IsDelete != 2
                    && (x.Host == subdomain || x.Code == subdomain));
        }

        if (tenant != null)
            _cache.Set(key, tenant, TimeSpan.FromMinutes(5));

        return tenant;
    }

    protected override IQueryable<Tenant> BuildQuery(PublicTenantSearchRequest r)
    {
        var q = _db.Tenants.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        return q.OrderBy(x => x.Id);
    }
}
