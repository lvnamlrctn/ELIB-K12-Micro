using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ReaderDeleteRepository : BaseRepository<ReaderDelete, ReaderDeleteSearchRequest, ReaderDeleteRequest>
{
    public ReaderDeleteRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ReaderDelete> BuildQuery(ReaderDeleteSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))      q = q.Where(x => x.Cardno!.Contains(r.Keyword) || x.FirstName!.Contains(r.Keyword) || x.LastName!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.FirstName))    q = q.Where(x => x.FirstName!.Contains(r.FirstName));
        if (!string.IsNullOrEmpty(r.LastName))     q = q.Where(x => x.LastName!.Contains(r.LastName));
        if (!string.IsNullOrEmpty(r.Cardno))       q = q.Where(x => x.Cardno!.Contains(r.Cardno));
        if (!string.IsNullOrEmpty(r.PortalId))     q = q.Where(x => x.PortalId     == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language))     q = q.Where(x => x.Language     == r.Language);
        if (r.OrgId.HasValue)                      q = q.Where(x => x.OrgId        == r.OrgId);
        if (r.ReaderTypeId.HasValue)               q = q.Where(x => x.ReaderTypeId == r.ReaderTypeId);
        if (r.ClassId.HasValue)                    q = q.Where(x => x.ClassId      == r.ClassId);
        if (r.CourseId.HasValue)                   q = q.Where(x => x.CourseId     == r.CourseId);
        if (r.Status.HasValue && r.Status > 0)     q = q.Where(x => x.Status       == r.Status);
        if (r.IssueDateFrom.HasValue)              q = q.Where(x => x.IssueDate    >= r.IssueDateFrom);
        if (r.IssueDateTo.HasValue)                q = q.Where(x => x.IssueDate    <= r.IssueDateTo);
        if (r.ExpireDateFrom.HasValue)             q = q.Where(x => x.ExpireDate   >= r.ExpireDateFrom);
        if (r.ExpireDateTo.HasValue)               q = q.Where(x => x.ExpireDate   <= r.ExpireDateTo);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ReaderDeleteRequest r, ReaderDelete e, long userId, bool isNew)
    {
        e.FirstName = r.FirstName; e.LastName = r.LastName; e.Cardno = r.Cardno;
        e.Email = r.Email; e.Phone = r.Phone; e.Address = r.Address; e.OrgId = r.OrgId;
        e.ReaderTypeId = r.ReaderTypeId; e.ClassId = r.ClassId; e.CourseId = r.CourseId;
        e.DegreeId = r.DegreeId; e.EthenicId = r.EthenicId; e.ProfId = r.ProfId;
        e.Blane = r.Blane; e.ExpireDate = r.ExpireDate; e.IssueDate = r.IssueDate;
        e.BirthDate = r.BirthDate; e.Password = r.Password;
        e.PortalId = r.PortalId; e.Language = r.Language; e.Photo = r.Photo;
        e.Status = r.Status; e.Sex = r.Sex;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(ReaderDelete e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ReaderDelete e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
