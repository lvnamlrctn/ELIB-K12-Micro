using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class EvaluateCourseRepository : BaseRepository<EvaluateCourse, EvaluateCourseSearchRequest, EvaluateCourseRequest>
{
    public EvaluateCourseRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<EvaluateCourse> BuildQuery(EvaluateCourseSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword) || x.Code!.Contains(r.Keyword));
        if (r.DegreeId.HasValue)              q = q.Where(x => x.DegreeId == r.DegreeId);
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status   == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EvaluateCourseRequest r, EvaluateCourse e, long userId, bool isNew)
    {
        e.Code = r.Code; e.Name = r.Name; e.Credit = r.Credit; e.Status = r.Status;
        e.DegreeId = r.DegreeId; e.OptionCourseId = r.OptionCourseId; e.KnowledgeId = r.KnowledgeId; e.FileUrl = r.FileUrl;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EvaluateCourse e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EvaluateCourse e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
