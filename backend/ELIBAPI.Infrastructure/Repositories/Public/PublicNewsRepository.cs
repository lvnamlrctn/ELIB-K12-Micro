using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicNewsSearchRequest : PublicSearchRequest
{
    public Guid? CategoryId { get; set; }
    public string? Types { get; set; }
}

public class PublicNewsLatestRequest
{
    public Guid DepartmentCode { get; set; }
    public Guid? CategoryCode { get; set; }
    public int Top { get; set; }
}

public interface IPublicNewsRepository : IPublicGenericRepository<News, PublicNewsSearchRequest>
{
    Task<List<News>> GetLatestNewsAsync(PublicNewsLatestRequest request);
    Task<List<News>> GetNewsByIdAsync(Guid newsId, Guid? tenantId = null);
}

public class PublicNewsRepository : PublicBaseRepository<News, PublicNewsSearchRequest>, IPublicNewsRepository
{
    private readonly IMemoryCache _newsCache;

    public PublicNewsRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache)
        => _newsCache = cache;

    protected override IQueryable<News> BuildQuery(PublicNewsSearchRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
            TenantId = _db.Tenants
                .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
                .Select(d => (long?)d.Id)
                .FirstOrDefault();

        long? categoryId = null;
        if (r.CategoryId != Guid.Empty)
            categoryId = _db.Categories
                .Where(c => c.PublicId == r.CategoryId && c.IsDelete != 2)
                .Select(c => (long?)c.Id)
                .FirstOrDefault();

        var q = _db.News.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (TenantId.HasValue)
            q = q.Where(x => x.TenantId == TenantId);
        else
            q = q.Where(x => false);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Title!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (!string.IsNullOrEmpty(r.Types)) q = q.Where(x => x.Types == r.Types);
        if (categoryId.HasValue) q = q.Where(x => x.CategoryId == categoryId);
        return q.OrderByDescending(x => x.Id);
    }

    // tenantId do PublicHostTenantFilter ghi đè theo host (<madonvi>.…) — có thì chỉ trả tin của đơn vị đó hoặc
    // dùng chung; host không khớp đơn vị (localhost/IP) mà client cũng không gửi thì giữ hành vi cũ.
    public async Task<List<News>> GetNewsByIdAsync(Guid newsId, Guid? tenantId = null)
    {
        var q = _db.News.Where(x => x.IsDelete != 2 && x.Status == 2 && x.PublicId == newsId);
        if (tenantId.HasValue && tenantId != Guid.Empty)
        {
            var id = await _db.Tenants
                .Where(d => d.PublicId == tenantId.Value && d.IsDelete != 2)
                .Select(d => (long?)d.Id)
                .FirstOrDefaultAsync();
            q = q.Where(x => x.TenantId == null || x.TenantId == id);
        }
        return await q.OrderByDescending(x => x.Id).ToListAsync();
    }

    public async Task<List<News>> GetLatestNewsAsync(PublicNewsLatestRequest r)
    {
        var key = $"News:Latest:{r.DepartmentCode}:{r.CategoryCode}:{r.Top}";
        if (_newsCache.TryGetValue(key, out List<News>? cached) && cached != null)
            return cached;

        long? TenantId = null;
        if (r.DepartmentCode != Guid.Empty)
            TenantId = await _db.Tenants
                .Where(d => d.PublicId == r.DepartmentCode && d.IsDelete != 2)
                .Select(d => (long?)d.Id)
                .FirstOrDefaultAsync();

        long? categoryId = null;
        if (r.CategoryCode.HasValue)
            categoryId = await _db.Categories
                .Where(c => c.PublicId == r.CategoryCode.Value && c.IsDelete != 2)
                .Select(c => (long?)c.Id)
                .FirstOrDefaultAsync();

        var q = _db.News.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null);
        if (categoryId.HasValue) q = q.Where(x => x.CategoryId == categoryId);

        var result = await q.OrderByDescending(x => x.Id).Take(r.Top).ToListAsync();
        _newsCache.Set(key, result, TimeSpan.FromMinutes(5));
        return result;
    }
}

