using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ThanhlyRepository : BaseRepository<Thanhly, ThanhlySearchRequest, ThanhlyRequest>
{
    public ThanhlyRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Thanhly> BuildQuery(ThanhlySearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.LiquidateFrom.HasValue) q = q.Where(x => x.Sumited >= r.LiquidateFrom);
        // Ngày "đến" không kèm giờ tính trọn ngày cuối (port ELIB-LRC 10-04).
        if (r.LiquidateTo is DateTime to) { var end = to.TimeOfDay == TimeSpan.Zero ? to.Date.AddDays(1) : to.AddTicks(1); q = q.Where(x => x.Sumited < end); }

        if (!string.IsNullOrEmpty(r.Barcode) || r.StoreId.HasValue || r.BibTypeId.HasValue
            || !string.IsNullOrEmpty(r.Title) || !string.IsNullOrEmpty(r.Author))
        {
            var matchedBarcodeIds =
                from bc in _context.Barcodes
                join b  in _context.Bibs    on bc.BibId equals b.Bibid  into bj from b  in bj.DefaultIfEmpty()
                join bx in _context.BibXmls on bc.BibId equals bx.BibId into xj from bx in xj.DefaultIfEmpty()
                where bc.IsDelete != 2
                   && (string.IsNullOrEmpty(r.Barcode)  || bc.BarcodeValue == r.Barcode)
                   && (!r.StoreId.HasValue               || bc.Store == r.StoreId)
                   && (!r.BibTypeId.HasValue             || (b != null && b.Bib_type_id == r.BibTypeId))
                   && (string.IsNullOrEmpty(r.Title)     || (bx != null && bx.Title!.Contains(r.Title)))
                   && (string.IsNullOrEmpty(r.Author)    || (bx != null && bx.Author!.Contains(r.Author)))
                select bc.Id;
            q = q.Where(x => x.BarcodeId.HasValue && matchedBarcodeIds.Contains(x.BarcodeId.Value));
        }
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ThanhlyRequest r, Thanhly e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Thanhly e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Thanhly e, int status, long userId) { }
}
