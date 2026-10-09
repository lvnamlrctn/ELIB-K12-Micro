using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class BarcodeRepository : BaseRepository<Barcode, BarcodeSearchRequest, BarcodeRequest>
{
    public BarcodeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    // Lịch sử thay đổi chi tiết (Đợt 17) — không có PII, không cần che field nào.
    protected override string[] AuditedFields =>
        [nameof(Barcode.BarcodeValue), nameof(Barcode.BarcodeNumber), nameof(Barcode.Store), nameof(Barcode.BibId), nameof(Barcode.Status)];

    protected override IQueryable<Barcode> BuildQuery(BarcodeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.BarcodeValue!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(BarcodeRequest r, Barcode e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Barcode e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Barcode e, int status, long userId) { }
}
