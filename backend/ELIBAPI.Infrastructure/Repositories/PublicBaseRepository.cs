using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public abstract class PublicBaseRepository<TEntity, TSearch>
    : IPublicGenericRepository<TEntity, TSearch>
    where TEntity : class
    where TSearch  : PublicSearchRequest
{
    protected readonly ELIBAPIDbContext _db;
    private   readonly IMemoryCache     _cache;
    private   readonly int              _cacheDurationMinutes;
    private   readonly ICacheInvalidator? _invalidator;

    /// <summary>Phân vùng cache (Đợt 22) — mặc định theo tên TEntity; controller ghi (Add/Update/Delete/
    /// ChangeStatus) gọi <c>ICacheInvalidator.Invalidate(nameof(TEntity-tương-ứng))</c> để tăng version,
    /// làm mọi khóa cache cũ hết hiệu lực ngay thay vì đợi hết TTL 5 phút.</summary>
    protected virtual string CacheScope => typeof(TEntity).Name;

    protected PublicBaseRepository(ELIBAPIDbContext db, IMemoryCache cache, int cacheDurationMinutes = 5, ICacheInvalidator? invalidator = null)
    {
        _db                   = db;
        _cache                = cache;
        _cacheDurationMinutes = cacheDurationMinutes;
        _invalidator          = invalidator;
    }

    private string Version => _invalidator?.GetVersion(CacheScope) ?? "0";

    protected abstract IQueryable<TEntity> BuildQuery(TSearch request);

    // Đợt 22.7 — vá lỗ hổng đọc chéo đơn vị phát hiện khi khảo sát Đợt 22.1: TEntity có cột TenantId
    // (đa số repo Public — Category/Banner/News/...) thì lọc == tenantId hoặc == null (đúng quy ước
    // "bản ghi dùng chung" đã áp dụng ở BuildQuery của từng repo, vd PublicCategoryRepository.BuildQuery).
    // Entity không có cột TenantId (vd Tenant — bản thân nó chính là đơn vị) thì tham số này không có
    // tác dụng, giữ nguyên hành vi cũ.
    private static readonly bool HasTenantColumn = typeof(TEntity).GetProperty("TenantId") != null;

    public async Task<TEntity?> GetByPublicIdAsync(Guid publicId, Guid tenantId = default)
    {
        long? resolvedTenantId = null;
        if (HasTenantColumn && tenantId != Guid.Empty)
        {
            resolvedTenantId = await _db.Tenants
                .Where(t => t.PublicId == tenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();
        }

        var key = $"{CacheScope}:v{Version}:Detail:{publicId}:{resolvedTenantId}";
        if (_cache.TryGetValue(key, out TEntity? cached) && cached != null)
            return cached;

        var q = _db.Set<TEntity>().Where(x => EF.Property<Guid>(x, "PublicId") == publicId);
        if (resolvedTenantId.HasValue)
            q = q.Where(x => EF.Property<long?>(x, "TenantId") == resolvedTenantId || EF.Property<long?>(x, "TenantId") == null);

        var result = await q.FirstOrDefaultAsync();

        if (result != null)
            _cache.Set(key, result, TimeSpan.FromMinutes(_cacheDurationMinutes));

        return result;
    }

    public async Task<PagedResult<TEntity>> SearchAsync(TSearch request)
    {
        var key = BuildCacheKey("Search", request);
        if (_cache.TryGetValue(key, out PagedResult<TEntity>? cached) && cached != null)
            return cached;

        var q     = BuildQuery(request);
        var total = await q.CountAsync();
        var items = await q
            .Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var result = new PagedResult<TEntity>
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize
        };

        _cache.Set(key, result, TimeSpan.FromMinutes(_cacheDurationMinutes));
        return result;
    }

    public async Task<List<TEntity>> SearchAllAsync(TSearch request)
    {
        var key = BuildCacheKey("SearchAll", request);
        if (_cache.TryGetValue(key, out List<TEntity>? cached) && cached != null)
            return cached;

        var result = await BuildQuery(request).ToListAsync();
        _cache.Set(key, result, TimeSpan.FromMinutes(_cacheDurationMinutes));
        return result;
    }

    private string BuildCacheKey(string method, TSearch request)
    {
        var json  = JsonSerializer.Serialize(request);
        var hash  = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(json)))[..6].ToLower();
        return $"{CacheScope}:v{Version}:{method}:{hash}";
    }
}
