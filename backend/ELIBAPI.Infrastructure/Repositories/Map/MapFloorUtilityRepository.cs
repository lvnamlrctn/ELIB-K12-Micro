using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class MapFloorUtilityRepository : BaseRepository<MapFloorUtility, MapFloorUtilitySearchRequest, MapFloorUtilityRequest>
{
    public MapFloorUtilityRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<MapFloorUtility> BuildQuery(MapFloorUtilitySearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (r.FloorId.HasValue) q = q.Where(x => x.FloorId == r.FloorId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MapFloorUtilityRequest r, MapFloorUtility e, long userId, bool isNew)
    {
        PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MapFloorUtility e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MapFloorUtility e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
