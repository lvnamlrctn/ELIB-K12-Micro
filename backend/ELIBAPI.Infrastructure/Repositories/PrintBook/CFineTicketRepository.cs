using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class CFineTicketRepository : BaseRepository<CFineTicket, CFineTicketSearchRequest, CFineTicketRequest>
{
    public CFineTicketRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<CFineTicket> BuildQuery(CFineTicketSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.ReaderId.HasValue) q = q.Where(x => x.ReaderId == r.ReaderId);
        if (!string.IsNullOrEmpty(r.Code)) q = q.Where(x => x.Code!.Contains(r.Code));
        if (r.StatusFilter.HasValue) q = q.Where(x => x.Status == r.StatusFilter);
        if (r.FineMethodId.HasValue) q = q.Where(x => x.FineMethodId == r.FineMethodId);
        if (r.CreatedRowBy.HasValue) q = q.Where(x => x.CreatedRowBy == r.CreatedRowBy);
        if (r.DebtStatus == 1) q = q.Where(x => ((x.TotalAmount ?? 0) - (x.DiscountAmount ?? 0) - (x.PaidAmount ?? 0)) > 0);
        else if (r.DebtStatus == 2) q = q.Where(x => ((x.TotalAmount ?? 0) - (x.DiscountAmount ?? 0) - (x.PaidAmount ?? 0)) <= 0);
        if (r.FineDateFrom.HasValue) q = q.Where(x => x.FineDate >= r.FineDateFrom);
        if (r.FineDateTo.HasValue) q = q.Where(x => x.FineDate <= r.FineDateTo);
        if (!string.IsNullOrEmpty(r.CardNo))
        {
            var readerIds = _context.Readers.Where(x => x.Cardno!.Contains(r.CardNo)).Select(x => x.Id);
            q = q.Where(x => x.ReaderId.HasValue && readerIds.Contains(x.ReaderId.Value));
        }
        if (!string.IsNullOrEmpty(r.ReaderName))
        {
            var readerIds = _context.Readers
                .Where(x => (x.FirstName + " " + x.LastName).Contains(r.ReaderName))
                .Select(x => x.Id);
            q = q.Where(x => x.ReaderId.HasValue && readerIds.Contains(x.ReaderId.Value));
        }
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(CFineTicketRequest r, CFineTicket e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(CFineTicket e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(CFineTicket e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
