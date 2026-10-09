using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class KiemKeRepository : BaseRepository<KiemKe, KiemKeSearchRequest, KiemKeRequest>
{
    public KiemKeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<KiemKe> BuildQuery(KiemKeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Dkcb!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.ID);
    }

    protected override void MapRequestToEntity(KiemKeRequest r, KiemKe e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(KiemKe e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(KiemKe e, int status, long userId) { }
}
