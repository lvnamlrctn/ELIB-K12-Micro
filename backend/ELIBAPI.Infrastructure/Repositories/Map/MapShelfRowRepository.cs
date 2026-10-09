using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class MapShelfRowRepository : BaseRepository<MapShelfRow, MapShelfRowSearchRequest, MapShelfRowRequest>
{
    public MapShelfRowRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<MapShelfRow> BuildQuery(MapShelfRowSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.ShelfDetailId.HasValue) q = q.Where(x => x.ShelfDetailId == r.ShelfDetailId);
        return q.OrderBy(x => x.RowIndex);
    }

    protected override void MapRequestToEntity(MapShelfRowRequest r, MapShelfRow e, long userId, bool isNew)
    {
        PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MapShelfRow e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MapShelfRow e, int status, long userId) { }
}
