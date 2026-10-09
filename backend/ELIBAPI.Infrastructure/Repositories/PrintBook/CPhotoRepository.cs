using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class CPhotoRepository : BaseRepository<CPhoto, CPhotoSearchRequest, CPhotoRequest>
{
    public CPhotoRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<CPhoto> BuildQuery(CPhotoSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Barcode!.Contains(r.Keyword));
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);

        // Lọc theo bạn đọc (số thẻ / họ / tên) — subquery lấy Id, không JOIN.
        if (!string.IsNullOrEmpty(r.CardNo) || !string.IsNullOrEmpty(r.LastName) || !string.IsNullOrEmpty(r.FirstName))
        {
            var readers = _context.Readers.Where(x => x.IsDelete != 2);
            if (!string.IsNullOrEmpty(r.CardNo))    readers = readers.Where(x => x.Cardno!.Contains(r.CardNo));
            if (!string.IsNullOrEmpty(r.LastName))  readers = readers.Where(x => x.LastName!.Contains(r.LastName));
            if (!string.IsNullOrEmpty(r.FirstName)) readers = readers.Where(x => x.FirstName!.Contains(r.FirstName));
            var readerIds = readers.Select(x => x.Id);
            q = q.Where(x => x.Reader_Id.HasValue && readerIds.Contains(x.Reader_Id.Value));
        }

        // Lọc theo nhan đề: BibXml.Title -> BibId -> Barcode.BarcodeValue
        if (!string.IsNullOrEmpty(r.BibTitle))
        {
            var bibIds = _context.BibXmls.Where(x => x.Title!.Contains(r.BibTitle)).Select(x => x.BibId);
            var matched = _context.Barcodes
                .Where(x => x.IsDelete != 2 && x.BibId.HasValue && bibIds.Contains(x.BibId.Value));
            // Tương quan theo cả đơn vị — mã ĐKCB chỉ duy nhất trong 1 đơn vị (Đợt 20).
            q = q.Where(x => x.Barcode != null
                && matched.Any(bc => bc.BarcodeValue == x.Barcode && (bc.TenantId ?? 0) == (x.TenantId ?? 0)));
        }

        if (r.PhotoDateFrom.HasValue) q = q.Where(x => x.PhotoDate >= r.PhotoDateFrom.Value.Date);
        if (r.PhotoDateTo.HasValue)   q = q.Where(x => x.PhotoDate <  r.PhotoDateTo.Value.Date.AddDays(1));
        if (r.IsPaid.HasValue)
        {
            if (r.IsPaid == 2) q = q.Where(x => x.IsPaid == 2);
            else               q = q.Where(x => x.IsPaid != 2);
        }

        return q.OrderByDescending(x => x.PhotoDate).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(CPhotoRequest r, CPhoto e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(CPhoto e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(CPhoto e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
