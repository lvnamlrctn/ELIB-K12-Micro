using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class InventoryRepository : BaseRepository<Inventory, InventorySearchRequest, InventoryRequest>
{
    public InventoryRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Inventory> BuildQuery(InventorySearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.InventoryName!.Contains(r.Keyword));
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        if (r.InventoryDateFrom is DateTime from) q = q.Where(x => x.Submited >= from.Date);
        if (r.InventoryDateTo is DateTime to) { var end = to.Date.AddDays(1); q = q.Where(x => x.Submited < end); }
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(InventoryRequest r, Inventory e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Inventory e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Inventory e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
