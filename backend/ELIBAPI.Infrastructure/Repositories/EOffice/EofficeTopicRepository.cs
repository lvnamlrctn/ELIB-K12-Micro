using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class EofficeTopicRepository : BaseRepository<EofficeTopic, EofficeTopicSearchRequest, EofficeTopicRequest>
{
    public EofficeTopicRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<EofficeTopic> BuildQuery(EofficeTopicSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ParentId.HasValue)               q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status   == r.Status);
        return q.OrderBy(x => x.Order).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EofficeTopicRequest r, EofficeTopic e, long userId, bool isNew)
    {
        e.Name = r.Name; e.ParentId = r.ParentId; e.Level = r.Level; e.Status = r.Status;
        e.Order = r.Order; e.PortalId = r.PortalId; e.Language = r.Language;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EofficeTopic e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EofficeTopic e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
