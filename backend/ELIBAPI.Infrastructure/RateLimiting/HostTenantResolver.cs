using System.Net;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.RateLimiting;

/// <summary>Đợt 22 — tra đơn vị theo Host (<c>&lt;madonvi&gt;.thuvientn.vn</c>), tách ra dùng chung từ
/// <see cref="ELIBAPI.API.Filters.PublicHostTenantFilter"/> (Đợt 18) để <see cref="TenantContextMiddleware"/>
/// (rate limiting) dùng lại đúng 1 cơ chế + 1 cache, không tra trùng 2 lần cho cùng 1 request. Cache 5 phút,
/// cả kết quả tìm thấy lẫn không khớp (host chung/nội bộ không tốn DB mỗi request).</summary>
public class HostTenantResolver(IPublicTenantRepository tenantRepo, IMemoryCache cache)
{
    public async Task<(Guid PublicId, long Id)?> ResolveAsync(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host == "localhost" || IPAddress.TryParse(host, out _))
            return null;

        var key = $"PublicHostTenant:{host.ToLowerInvariant()}";
        if (cache.TryGetValue(key, out (Guid, long)? cached))
            return cached;

        var t = await tenantRepo.GetByHostAsync(host);
        (Guid, long)? result = t != null ? (t.PublicId, t.Id) : null;
        cache.Set(key, result, TimeSpan.FromMinutes(5));
        return result;
    }
}
