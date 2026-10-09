using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class MonHocRepository : BaseRepository<MonHoc, MonHocSearchRequest, MonHocRequest>
{
    public MonHocRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<MonHoc> BuildQuery(MonHocSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.TenMon!.Contains(r.Keyword) || x.MaMon!.Contains(r.Keyword));
        if (r.DegreeId.HasValue)              q = q.Where(x => x.DegreeId    == r.DegreeId);
        if (r.KnowledgeId.HasValue)           q = q.Where(x => x.KnowledgeId == r.KnowledgeId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MonHocRequest r, MonHoc e, long userId, bool isNew)
    {
        e.MaMon = r.MaMon; e.TenMon = r.TenMon; e.SoTinChi = r.SoTinChi;
        e.DegreeId = r.DegreeId; e.KnowledgeId = r.KnowledgeId; e.OptionId = r.OptionId;
        e.NguoiBienSoan = r.NguoiBienSoan; e.Active = r.Active; e.Attachment = r.Attachment; e.Note = r.Note;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MonHoc e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MonHoc e, int status, long userId)
    { e.Active = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
