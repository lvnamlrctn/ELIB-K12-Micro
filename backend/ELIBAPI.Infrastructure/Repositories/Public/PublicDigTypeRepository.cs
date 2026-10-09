using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicDigTypeSearchRequest : PublicSearchRequest { }

/// <summary>Danh mục "loại tài liệu số" (DigType) công khai -- đổ dropdown lọc ở trang tìm kiếm OPAC.
/// Quản trị loại này qua trang admin có sẵn /admin/ebook-dig-types.</summary>
public class PublicDigTypeRepository : PublicBaseRepository<DigType, PublicDigTypeSearchRequest>
{
    public PublicDigTypeRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    protected override IQueryable<DigType> BuildQuery(PublicDigTypeSearchRequest r)
    {
        long? tenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            tenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }
        var q = _db.DigTypes.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))
            q = q.Where(x => (x.DescriptionVn != null && x.DescriptionVn.Contains(r.Keyword))
                           || (x.DescriptionEn != null && x.DescriptionEn.Contains(r.Keyword)));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (tenantId.HasValue) q = q.Where(x => x.TenantId == tenantId || x.TenantId == null);
        return q.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);
    }
}
