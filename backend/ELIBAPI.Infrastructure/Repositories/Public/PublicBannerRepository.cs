using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using ELIBAPI.Core.Interfaces;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicBannerSearchRequest : PublicSearchRequest { }

public interface IPublicBannerRepository : IPublicGenericRepository<PublicBannerResponse, PublicBannerSearchRequest> { }

public class PublicBannerRepository : PublicBaseRepository<PublicBannerResponse, PublicBannerSearchRequest>, IPublicBannerRepository
{
    // Đợt 22 — nhận ICacheInvalidator để controller admin (BannerController) báo cache OPAC hết hiệu lực
    // ngay sau ghi; base(db, cache) KHÔNG tự lấy dịch vụ qua DI cho tham số không truyền, phải forward tay.
    public PublicBannerRepository(ELIBAPIDbContext db, IMemoryCache cache, ICacheInvalidator invalidator) : base(db, cache, invalidator: invalidator) { }

    protected override IQueryable<PublicBannerResponse> BuildQuery(PublicBannerSearchRequest r)
    {
        long? tenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            tenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }

        var q = _db.Banners.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (tenantId.HasValue) q = q.Where(x => x.TenantId == tenantId || x.TenantId == null);

        return q.OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.Id)
                .Select(x => new PublicBannerResponse
                {
                    Url       = x.Url,
                    Link      = x.Link,
                    Name      = x.Name,
                    SortOrder = x.SortOrder,
                    Status    = x.Status,
                });
    }
}
