using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class LostBookRepository : BaseRepository<LostBook, LostBookSearchRequest, LostBookRequest>
{
    public LostBookRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<LostBook> BuildQuery(LostBookSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Barcode!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.Barcode)) q = q.Where(x => x.Barcode == r.Barcode);
        if (r.StoreId.HasValue) q = q.Where(x => x.Store == r.StoreId);
        if (r.CreatedDateFrom.HasValue) q = q.Where(x => x.Submited >= r.CreatedDateFrom);
        // Ngày "đến" không kèm giờ (bộ chọn ngày) tính trọn ngày cuối (port ELIB-LRC 10-04).
        if (r.CreatedDateTo is DateTime to) { var end = to.TimeOfDay == TimeSpan.Zero ? to.Date.AddDays(1) : to.AddTicks(1); q = q.Where(x => x.Submited < end); }

        if (r.BibTypeId.HasValue || r.MfnFrom.HasValue || r.MfnTo.HasValue)
        {
            var matchedBarcodes =
                from bc in _context.Barcodes
                join b in _context.Bibs on bc.BibId equals b.Bibid
                where bc.IsDelete != 2
                   && (!r.BibTypeId.HasValue || b.Bib_type_id == r.BibTypeId)
                   && (!r.MfnFrom.HasValue || b.Mfn >= r.MfnFrom)
                   && (!r.MfnTo.HasValue || b.Mfn <= r.MfnTo)
                select bc;
            // Tương quan theo cả đơn vị — mã ĐKCB chỉ duy nhất trong 1 đơn vị (Đợt 20).
            q = q.Where(x => matchedBarcodes.Any(bc => bc.BarcodeValue == x.Barcode && (bc.TenantId ?? 0) == (x.TenantId ?? 0)));
        }
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(LostBookRequest r, LostBook e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(LostBook e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(LostBook e, int status, long userId) { }
}
