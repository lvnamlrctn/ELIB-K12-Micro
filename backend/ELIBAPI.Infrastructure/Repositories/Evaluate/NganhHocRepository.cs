using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class NganhHocRepository : BaseRepository<NganhHoc, NganhHocSearchRequest, NganhHocRequest>
{
    public NganhHocRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<NganhHoc> BuildQuery(NganhHocSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.MajorsName!.Contains(r.Keyword) || x.MajorsCode!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ProgramId.HasValue)              q = q.Where(x => x.ProgramId == r.ProgramId);
        if (r.ParentId.HasValue)               q = q.Where(x => x.ParentId  == r.ParentId);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status    == r.Status);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(NganhHocRequest r, NganhHoc e, long userId, bool isNew)
    {
        e.MajorsName = r.MajorsName; e.ParentId = r.ParentId; e.ProgramId = r.ProgramId;
        e.AmountStudent = r.AmountStudent; e.SortOrder = r.SortOrder; e.PortalId = r.PortalId;
        e.Language = r.Language; e.MajorsCode = r.MajorsCode; e.Status = r.Status;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(NganhHoc e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(NganhHoc e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
