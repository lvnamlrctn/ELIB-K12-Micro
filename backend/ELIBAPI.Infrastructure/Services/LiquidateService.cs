using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Xem <see cref="ILiquidateService"/>. Port ELIB-LRC 10-04, giữ lọc đơn vị của K12.</summary>
public class LiquidateService(ELIBAPIDbContext db) : ILiquidateService
{
    public const string LiquidatedStatus = "S", LostStatus = "L", InStoreStatus = "R";
    public const int MaxPageSize = 200;
    private const string NotFound = "Mã vạch không tồn tại";

    private static readonly Dictionary<string, string> Blocked = new()
    {
        ["B"] = "Tài liệu đang được bạn đọc mượn",
        ["X"] = "Tài liệu đang ra khỏi kho (xuất kho / xử lý kỹ thuật) — nhập kho trước khi thanh lý",
    };

    public async Task<PagedResult<LiquidateRow>> SearchAsync(ThanhlySearchRequest r, bool paged, long? scopeTenantId, bool includeShared, bool all)
    {
        var q = db.Thanhlys.Where(x => x.IsDelete != 2 && (all || x.TenantId == scopeTenantId || (includeShared && x.TenantId == null)));
        if (r.LiquidateFrom is DateTime from) q = q.Where(x => x.Sumited >= from);
        if (r.LiquidateTo is DateTime to)
            q = to.TimeOfDay == TimeSpan.Zero ? q.Where(x => x.Sumited < to.Date.AddDays(1)) : q.Where(x => x.Sumited <= to);

        // Lọc không phân biệt hoa thường (trước đây phân biệt).
        static string? Low(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLower();
        var (barcode, title, author) = (Low(r.Barcode), Low(r.Title), Low(r.Author));
        if (barcode != null || r.StoreId.HasValue || r.BibTypeId.HasValue || title != null || author != null)
        {
            var matched =
                from bc in db.Barcodes
                join b in db.Bibs on bc.BibId equals b.Bibid into bj from b in bj.DefaultIfEmpty()
                join bx in db.BibXmls on bc.BibId equals bx.BibId into xj from bx in xj.DefaultIfEmpty()
                where (barcode == null || bc.BarcodeValue!.Trim().ToLower() == barcode)
                   && (!r.StoreId.HasValue || bc.Store == r.StoreId)
                   && (!r.BibTypeId.HasValue || (b != null && b.Bib_type_id == r.BibTypeId))
                   && (title == null || (bx != null && bx.Title != null && bx.Title.ToLower().Contains(title)))
                   && (author == null || (bx != null && bx.Author != null && bx.Author.ToLower().Contains(author)))
                select bc.Id;
            q = q.Where(x => x.BarcodeId.HasValue && matched.Contains(x.BarcodeId.Value));
        }

        var total = await q.CountAsync();
        var ordered = q.OrderByDescending(x => x.Sumited).ThenByDescending(x => x.Id);
        var size = paged ? Math.Clamp(r.PageSize <= 0 ? 10 : r.PageSize, 1, MaxPageSize) : 0;
        var page = Math.Max(1, r.PageIndex);
        var list = paged ? await ordered.Skip((page - 1) * size).Take(size).ToListAsync() : await ordered.ToListAsync();

        var barcodeIds = list.Where(x => x.BarcodeId.HasValue).Select(x => x.BarcodeId!.Value).Distinct().ToList();
        var copies = await db.Barcodes.Where(b => barcodeIds.Contains(b.Id)).Select(b => new { b.Id, b.BarcodeValue, b.BibId, b.Store }).ToDictionaryAsync(b => b.Id);
        var bibIds = copies.Values.Where(b => b.BibId.HasValue).Select(b => b.BibId!.Value).Distinct().ToList();
        var bibs = await db.Bibs.Where(b => bibIds.Contains(b.Bibid)).ToDictionaryAsync(b => b.Bibid, b => b.Mfn);
        var xmls = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).Select(x => new { x.BibId, x.Title, x.Author, x.Publisher }).ToDictionaryAsync(x => x.BibId);
        var storeIds = copies.Values.Where(b => b.Store.HasValue).Select(b => (long)b.Store!.Value).Distinct().ToList();
        var stores = await db.Stores.Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(db, list.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value));

        return new PagedResult<LiquidateRow>
        {
            TotalCount = total, PageIndex = paged ? page : 1, PageSize = paged ? size : total,
            Items = list.Select(x =>
            {
                var bc = x.BarcodeId is long id ? copies.GetValueOrDefault(id) : null;
                var xml = bc?.BibId is long bib ? xmls.GetValueOrDefault(bib) : null;
                return new LiquidateRow(x.Id, x.PublicId, x.BarcodeId, bc?.BarcodeValue, bc?.BibId is long m ? bibs.GetValueOrDefault(m) : null,
                    xml?.Title, xml?.Author, xml?.Publisher, bc?.Store, bc?.Store is int s ? stores.GetValueOrDefault(s) : null, x.Sumited, x.Note,
                    x.TenantId, x.TenantId is long t ? tenantNames.GetValueOrDefault(t) : null);
            }).ToList()
        };
    }

    public async Task<ServiceResult<Thanhly>> LiquidateAsync(string? code, string? reason, DateTime? date, long? userId, long? tenantId)
    {
        var (barcode, error) = await FindAsync(code, tenantId);
        if (barcode == null) return ServiceResult<Thanhly>.NotFound(error!);
        if (barcode.Status == LiquidatedStatus || await db.Thanhlys.AnyAsync(x => x.BarcodeId == barcode.Id && x.IsDelete != 2))
            return ServiceResult<Thanhly>.BadRequest("Tài liệu đã được thanh lý");
        // Trước đây thanh lý được cả bản bạn đọc đang mượn / đang ra kho.
        if (barcode.Status != null && Blocked.TryGetValue(barcode.Status, out var why)) return ServiceResult<Thanhly>.BadRequest(why);
        if (await PrintLoans.Open(db).AnyAsync(o => o.Barcode == barcode.BarcodeValue && (o.TenantId ?? 0) == (barcode.TenantId ?? 0)))
            return ServiceResult<Thanhly>.BadRequest(Blocked["B"]);

        var now = LibraryClock.Now;
        var entity = new Thanhly
        {
            BarcodeId = barcode.Id, Sumited = date ?? now, UserId = userId is long u && u <= int.MaxValue ? (int)u : null, Note = reason,
            TenantId = barcode.TenantId ?? tenantId, // bản ghi thanh lý thuộc đơn vị của bản sách
            PublicId = Guid.NewGuid(), CreatedRowBy = userId, CreatedRowDate = now
        };
        db.Thanhlys.Add(entity);
        barcode.Status = LiquidatedStatus; barcode.UpdateRowBy = userId; barcode.UpdatedRowDate = now;
        await db.SaveChangesAsync();
        return ServiceResult<Thanhly>.Ok(entity);
    }

    public async Task<ServiceResult<bool>> CancelAsync(string? code, long? userId, long? tenantId)
    {
        var (barcode, error) = await FindAsync(code, tenantId);
        if (barcode == null) return ServiceResult<bool>.NotFound(error!);
        var record = await db.Thanhlys.FirstOrDefaultAsync(x => x.BarcodeId == barcode.Id && x.IsDelete != 2);
        if (record == null) return ServiceResult<bool>.NotFound("Không tìm thấy bản ghi thanh lý");

        var now = LibraryClock.Now;
        record.IsDelete = 2; record.UpdateRowBy = userId; record.UpdatedRowDate = now;
        // Trước đây luôn ghi "R": bản đã báo mất rồi thanh lý, huỷ thanh lý lại thành "sẵn sàng cho mượn".
        if (barcode.Status == LiquidatedStatus)
        {
            var stillLost = await db.LostBooks.AnyAsync(l => l.Barcode == barcode.BarcodeValue && l.TenantId == barcode.TenantId && l.IsDelete != 2);
            barcode.Status = stillLost ? LostStatus : InStoreStatus; barcode.UpdateRowBy = userId; barcode.UpdatedRowDate = now;
        }
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    private async Task<(Barcode? Barcode, string? Error)> FindAsync(string? code, long? tenantId)
    {
        code = (code ?? "").Trim();
        if (code == "") return (null, NotFound);
        var (bc, ambiguous) = await BarcodeTenantLookup.ByValueAsync(db.Barcodes, code, tenantId, normalize: true);
        if (ambiguous) return (null, BarcodeTenantLookup.AmbiguousMessage);
        return bc == null ? (null, NotFound) : (bc, null);
    }
}
