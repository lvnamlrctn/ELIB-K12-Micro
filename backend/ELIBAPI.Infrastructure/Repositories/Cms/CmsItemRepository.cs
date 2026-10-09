using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class CmsItemRepository : BaseRepository<CmsItem, CmsItemSearchRequest, CmsItemRequest>
{
    public CmsItemRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<CmsItem> BuildQuery(CmsItemSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ItemTypeId.HasValue) q = q.Where(x => x.ItemTypeId == r.ItemTypeId);
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(CmsItemRequest r, CmsItem e, long userId, bool isNew)
    {
        e.ItemTypeId = r.ItemTypeId; e.Name = r.Name; e.Content = r.Content;
        e.Link = r.Link; e.Status = r.Status; e.PortalId = r.PortalId; e.Language = r.Language;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(CmsItem e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(CmsItem e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
