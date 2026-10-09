using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicEbookTopicSearchRequest : PublicSearchRequest { }

/// <summary>Danh mục "chủ đề" (Ebook.Topic) công khai -- đổ dropdown chọn hồ sơ quan tâm của bạn
/// đọc (dùng bởi chatbot gợi ý tài liệu theo hồ sơ). Quản trị qua trang admin có sẵn.</summary>
public class PublicEbookTopicRepository : PublicBaseRepository<EbookTopic, PublicEbookTopicSearchRequest>
{
    public PublicEbookTopicRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    protected override IQueryable<EbookTopic> BuildQuery(PublicEbookTopicSearchRequest r)
    {
        long? tenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            tenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }
        var q = _db.EbookTopics.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))
            q = q.Where(x => x.Name != null && x.Name.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (tenantId.HasValue) q = q.Where(x => x.TenantId == tenantId || x.TenantId == null);
        return q.OrderBy(x => x.Order).ThenBy(x => x.Id);
    }
}
