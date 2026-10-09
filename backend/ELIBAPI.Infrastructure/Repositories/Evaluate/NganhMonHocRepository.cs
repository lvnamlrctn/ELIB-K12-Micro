using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class NganhMonHocRepository : BaseRepository<NganhMonHoc, NganhMonHocSearchRequest, NganhMonHocRequest>
{
    public NganhMonHocRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<NganhMonHoc> BuildQuery(NganhMonHocSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.MajorId.HasValue)  q = q.Where(x => x.MajorId  == r.MajorId);
        if (r.MonHocId.HasValue) q = q.Where(x => x.MonHocId == r.MonHocId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(NganhMonHocRequest r, NganhMonHoc e, long userId, bool isNew)
    {
        e.MajorId = r.MajorId; e.MonHocId = r.MonHocId;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(NganhMonHoc e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(NganhMonHoc e, int status, long userId) { }
}
