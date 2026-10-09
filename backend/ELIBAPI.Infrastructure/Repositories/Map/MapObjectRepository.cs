using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class MapObjectRepository : BaseRepository<MapObject, MapObjectSearchRequest, MapObjectRequest>
{
    public MapObjectRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<MapObject> BuildQuery(MapObjectSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (r.FloorId.HasValue) q = q.Where(x => x.FloorId == r.FloorId);
        if (!string.IsNullOrEmpty(r.ObjectType)) q = q.Where(x => x.ObjectType == r.ObjectType);
        if (r.Category.HasValue) q = q.Where(x => x.Category == r.Category);
        if (r.StoreId.HasValue) q = q.Where(x => x.StoreId == r.StoreId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MapObjectRequest r, MapObject e, long userId, bool isNew)
    {
        PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MapObject e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MapObject e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
