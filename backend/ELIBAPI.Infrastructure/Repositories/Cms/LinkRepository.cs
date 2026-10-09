using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class LinkRepository : BaseRepository<Link, LinkSearchRequest, LinkRequest>
{
    public LinkRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Link> BuildQuery(LinkSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.LinkGroupId.HasValue && r.LinkGroupId > 0) q = q.Where(x => x.LinkGroupId == r.LinkGroupId);
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(LinkRequest r, Link e, long userId, bool isNew)
    {
        e.Name = r.Name; e.LinkUrl = r.LinkUrl; e.Description = r.Description; e.Images = r.Images;
        e.Status = r.Status; e.LinkGroupId = r.LinkGroupId; e.PortalId = r.PortalId; e.Language = r.Language;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Link e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Link e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
