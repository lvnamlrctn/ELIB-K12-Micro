using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class AbReceiptRepository : BaseRepository<AbReceipt, AbReceiptSearchRequest, AbReceiptRequest>
{
    public AbReceiptRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<AbReceipt> BuildQuery(AbReceiptSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Receipt_Name!.Contains(r.Keyword));
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        if (r.CodeFrom.HasValue) q = q.Where(x => x.Code >= r.CodeFrom);
        if (r.CodeTo.HasValue) q = q.Where(x => x.Code <= r.CodeTo);
        if (!string.IsNullOrEmpty(r.ReceiptName)) q = q.Where(x => x.Receipt_Name!.Contains(r.ReceiptName));
        if (r.CreatedBy.HasValue) q = q.Where(x => x.CreatedRowBy == r.CreatedBy);
        if (r.SupplierId.HasValue) q = q.Where(x => x.Supplier_Id == r.SupplierId);
        if (r.ReceiptDateFrom.HasValue) q = q.Where(x => x.Receipt_Date >= r.ReceiptDateFrom);
        if (r.ReceiptDateTo.HasValue) q = q.Where(x => x.Receipt_Date <= r.ReceiptDateTo);
        if (r.CreatedDateFrom.HasValue) q = q.Where(x => x.CreatedDate >= r.CreatedDateFrom);
        if (r.CreatedDateTo.HasValue) q = q.Where(x => x.CreatedDate <= r.CreatedDateTo);
        if (r.SourceId.HasValue) q = q.Where(x => x.Source_Id == r.SourceId);
        if (r.FundId.HasValue) q = q.Where(x => x.FundId == r.FundId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(AbReceiptRequest r, AbReceipt e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(AbReceipt e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(AbReceipt e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
