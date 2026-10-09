using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class BibDataOrderRepository : BaseRepository<BibDataOrder, BibDataOrderSearchRequest, BibDataOrderRequest>
{
    public BibDataOrderRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<BibDataOrder> BuildQuery(BibDataOrderSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Title!.Contains(r.Keyword));
        if (r.BibId.HasValue) q = q.Where(x => x.BibId == r.BibId);
        return q.OrderByDescending(x => x.BibDataId);
    }

    protected override void MapRequestToEntity(BibDataOrderRequest r, BibDataOrder e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(BibDataOrder e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(BibDataOrder e, int status, long userId) { }
}
