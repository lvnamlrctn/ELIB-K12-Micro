using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ELIBAPI.Infrastructure.Repositories;

public class PublicPhotoSearchRequest : PublicSearchRequest
{
    public long? PhotoAlbumId { get; set; }
    public string? Types { get; set; }
}

public class PublicPhotoRepository : PublicBaseRepository<Photo, PublicPhotoSearchRequest>
{
    public PublicPhotoRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

    protected override IQueryable<Photo> BuildQuery(PublicPhotoSearchRequest r)
    {
        long? TenantId = null;
        if (r.TenantId != Guid.Empty)
        {
            TenantId = _db.Tenants
               .Where(d => d.PublicId == r.TenantId && d.IsDelete != 2)
               .Select(d => (long?)d.Id)
               .FirstOrDefault();
        }
        var q = _db.Photos.Where(x => x.IsDelete != 2 && x.Status == 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (!string.IsNullOrEmpty(r.Types)) q = q.Where(x => x.Types == r.Types);
        if (r.PhotoAlbumId.HasValue) q = q.Where(x => x.PhotoAlbumId == r.PhotoAlbumId);
        if (TenantId.HasValue) q = q.Where(x => x.TenantId == TenantId || x.TenantId == null);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }
}

