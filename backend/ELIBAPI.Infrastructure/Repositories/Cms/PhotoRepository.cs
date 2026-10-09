using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class PhotoRepository : BaseRepository<Photo, PhotoSearchRequest, PhotoRequest>
{
    public PhotoRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Photo> BuildQuery(PhotoSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (!string.IsNullOrEmpty(r.Types)) q = q.Where(x => x.Types == r.Types);
        if (r.PhotoAlbumId.HasValue) q = q.Where(x => x.PhotoAlbumId == r.PhotoAlbumId);
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PhotoRequest r, Photo e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Brief = r.Brief; e.Image = r.Image; e.Link = r.Link;
        e.Postion = r.Postion; e.PhotoAlbumId = r.PhotoAlbumId; e.Width = r.Width;
        e.Height = r.Height; e.Status = r.Status; e.PortalId = r.PortalId;
        e.SortOrder = r.SortOrder; e.Language = r.Language; e.Types = r.Types;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Photo e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Photo e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
