using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public interface IPublicMenuRepository : IPublicGenericRepository<Menu, PublicMenuSearchRequest>
{
    Task<List<Menu>> BuildMenusync(Guid departmentCode);
}

public class PublicMenuSearchRequest : PublicSearchRequest
{
    public long? MenuType { get; set; }
    public long? ParentId { get; set; }
}

public class PublicCmsMenuRequest
{
    public Guid DepartmentCode { get; set; }
}

public class PublicMenuRepository : PublicBaseRepository<Menu, PublicMenuSearchRequest>, IPublicMenuRepository
{
    private readonly IMemoryCache _menuCache;

    public PublicMenuRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache)
        => _menuCache = cache;

    protected override IQueryable<Menu> BuildQuery(PublicMenuSearchRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            TenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }
        var q = _db.Menus.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.MenuType.HasValue) q = q.Where(x => x.MenuType == r.MenuType);
        if (r.ParentId.HasValue) q = q.Where(x => x.ParentId == r.ParentId);
        if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    public async Task<List<Menu>> BuildMenusync(Guid departmentCode)
    {
        var key = $"Menu:Flat:{departmentCode}";
        if (_menuCache.TryGetValue(key, out List<Menu>? cached) && cached != null)
            return cached;

        var q = _db.Menus.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (departmentCode != Guid.Empty)
        {
            var TenantId = await _db.Tenants
                .Where(d => d.PublicId == departmentCode && d.IsDelete != 2)
                .Select(d => (long?)d.Id)
                .FirstOrDefaultAsync();
            if (TenantId.HasValue)
                q = q.Where(x => x.TenantId == TenantId || x.TenantId == null);
        }

        var result = await q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id).ToListAsync();
        _menuCache.Set(key, result, TimeSpan.FromMinutes(5));
        return result;
    }
}

