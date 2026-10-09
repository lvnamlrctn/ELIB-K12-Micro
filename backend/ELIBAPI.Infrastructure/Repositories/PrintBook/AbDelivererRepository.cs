using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class AbDelivererRepository : BaseRepository<AbDeliverer, AbDelivererSearchRequest, AbDelivererRequest>
{
    public AbDelivererRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<AbDeliverer> BuildQuery(AbDelivererSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.DelivererName!.Contains(r.Keyword));
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        if (r.CodeFrom.HasValue) q = q.Where(x => x.Code >= r.CodeFrom);
        if (r.CodeTo.HasValue) q = q.Where(x => x.Code <= r.CodeTo);
        if (!string.IsNullOrEmpty(r.DelivererName)) q = q.Where(x => x.DelivererName!.Contains(r.DelivererName));
        if (!string.IsNullOrEmpty(r.ReceiptName)) q = q.Where(x => x.ReceiptName!.Contains(r.ReceiptName));
        if (r.CreatedBy.HasValue) q = q.Where(x => x.CreatedRowBy == r.CreatedBy);
        if (r.DelivererDateFrom.HasValue) q = q.Where(x => x.DelivererDate >= r.DelivererDateFrom);
        if (r.DelivererDateTo.HasValue) q = q.Where(x => x.DelivererDate <= r.DelivererDateTo);
        if (r.Sign.HasValue) q = q.Where(x => (x.Sign ?? 1) == r.Sign);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(AbDelivererRequest r, AbDeliverer e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        if (isNew && e.Sign == null) e.Sign = 1;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(AbDeliverer e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(AbDeliverer e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
