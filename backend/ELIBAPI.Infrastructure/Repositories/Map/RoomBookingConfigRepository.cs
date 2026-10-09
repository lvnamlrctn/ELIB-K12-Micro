using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

// CRUD thuần (Add/Update/Delete/Search) đi qua BaseRepository — TenantId tự gán/lọc qua JWT
// (ApplyTenantOnAdd/ApplyTenantFilter/CheckTenantOwnership) như mọi entity có cột TenantId khác.
public class RoomBookingConfigRepository : BaseRepository<RoomBookingConfig, RoomBookingConfigSearchRequest, RoomBookingConfigRequest>
{
    public RoomBookingConfigRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<RoomBookingConfig> BuildQuery(RoomBookingConfigSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.MapObjectId.HasValue) q = q.Where(x => x.MapObjectId == r.MapObjectId);
        return q.OrderByDescending(x => x.Id);
    }

    public override async Task<PagedResult<RoomBookingConfig>> SearchAsync(RoomBookingConfigSearchRequest r)
    {
        var paged = await base.SearchAsync(r);
        await FillRoomNamesAsync(paged.Items);
        return paged;
    }

    public override async Task<List<RoomBookingConfig>> SearchAllAsync(RoomBookingConfigSearchRequest r)
    {
        var list = await base.SearchAllAsync(r);
        await FillRoomNamesAsync(list);
        return list;
    }

    private async Task FillRoomNamesAsync(List<RoomBookingConfig> list)
    {
        var objectIds = list.Select(x => x.MapObjectId).Distinct().ToList();
        var roomMap   = await _context.MapObjects.Where(o => objectIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name);
        foreach (var x in list)
            x.RoomName = roomMap.TryGetValue(x.MapObjectId, out var rn) ? rn : null;
    }

    protected override void MapRequestToEntity(RoomBookingConfigRequest r, RoomBookingConfig e, long userId, bool isNew)
    {
        PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; e.Status ??= 2; }
    }

    protected override void SoftDelete(RoomBookingConfig e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(RoomBookingConfig e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
