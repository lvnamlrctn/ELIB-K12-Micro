using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class SupportonlineRepository : BaseRepository<Supportonline, SupportonlineSearchRequest, SupportonlineRequest>
{
    public SupportonlineRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Supportonline> BuildQuery(SupportonlineSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(SupportonlineRequest r, Supportonline e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Yahoo = r.Yahoo; e.Skype = r.Skype; e.Facebook = r.Facebook;
        e.Email = r.Email; e.Phone = r.Phone; e.Mobile = r.Mobile;
        e.PortalId = r.PortalId; e.Language = r.Language; e.Wellcome = r.Wellcome;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Supportonline e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Supportonline e, int status, long userId) { }
}
