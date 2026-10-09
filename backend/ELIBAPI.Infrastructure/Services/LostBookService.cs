using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Xem <see cref="ILostBookService"/>. Port ELIB-LRC 10-04 (tách từ <c>StoreLostBookController</c>), giữ lọc đơn vị
/// của K12: bản ghi mất / ĐKCB so khớp theo (mã, đơn vị) vì mã ĐKCB chỉ duy nhất trong 1 đơn vị.</summary>
public class LostBookService(ELIBAPIDbContext db) : ILostBookService
{
    public const string LostStatus = "L", InStoreStatus = "R", LiquidatedStatus = "S";
    public const int MaxPageSize = 200;
    private const string NotFound = "Đăng ký cá biệt không tồn tại";

    public async Task<PagedResult<LostBookRow>> SearchAsync(LostBookSearchRequest r, bool paged, long? scopeTenantId, bool includeShared, bool all)
    {
        var recorded = db.LostBooks.Where(x => x.IsDelete != 2
            && (all || x.TenantId == scopeTenantId || (includeShared && x.TenantId == null)));
        // ĐKCB đang "L" không có bản ghi báo mất cùng đơn vị (dữ liệu cũ, mất qua phiếu phạt).
        var unrecorded = db.Barcodes.Where(b => b.Status == LostStatus && b.IsDelete != 2 && b.BarcodeValue != null
            && (all || b.TenantId == scopeTenantId || (includeShared && b.TenantId == null))
            && !db.LostBooks.Any(l => l.Barcode == b.BarcodeValue && (l.TenantId ?? 0) == (b.TenantId ?? 0) && l.IsDelete != 2));

        var keyword = string.IsNullOrWhiteSpace(r.Keyword) ? null : r.Keyword.Trim().ToLower();
        if (keyword != null)
        {
            recorded = recorded.Where(x => x.Barcode != null && x.Barcode.ToLower().Contains(keyword));
            unrecorded = unrecorded.Where(b => b.BarcodeValue!.ToLower().Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(r.Barcode))
        {
            var exact = r.Barcode.Trim().ToLower();
            recorded = recorded.Where(x => x.Barcode != null && x.Barcode.Trim().ToLower() == exact);
            unrecorded = unrecorded.Where(b => b.BarcodeValue!.Trim().ToLower() == exact);
        }
        if (r.StoreId.HasValue)
        {
            recorded = recorded.Where(x => x.Store == r.StoreId);
            unrecorded = unrecorded.Where(b => b.Store == r.StoreId);
        }
        if (r.BibTypeId.HasValue || r.MfnFrom.HasValue || r.MfnTo.HasValue)
        {
            var bibIds = db.Bibs.Where(b => (!r.BibTypeId.HasValue || b.Bib_type_id == r.BibTypeId)
                && (!r.MfnFrom.HasValue || b.Mfn >= r.MfnFrom) && (!r.MfnTo.HasValue || b.Mfn <= r.MfnTo)).Select(b => (long?)b.Bibid);
            var copies = db.Barcodes.Where(b => b.IsDelete != 2 && bibIds.Contains(b.BibId));
            recorded = recorded.Where(x => copies.Any(b => b.BarcodeValue == x.Barcode && (b.TenantId ?? 0) == (x.TenantId ?? 0)));
            unrecorded = unrecorded.Where(b => bibIds.Contains(b.BibId));
        }
        var dated = r.CreatedDateFrom.HasValue || r.CreatedDateTo.HasValue;
        if (r.CreatedDateFrom is DateTime from) recorded = recorded.Where(x => x.Submited >= from);
        if (r.CreatedDateTo is DateTime to)
        {
            // Ngày "đến" không kèm giờ (bộ chọn ngày) trước đây so ≤ 00:00 nên mất trọn ngày cuối.
            if (to.TimeOfDay == TimeSpan.Zero) { var end = to.Date.AddDays(1); recorded = recorded.Where(x => x.Submited < end); }
            else recorded = recorded.Where(x => x.Submited <= to);
        }

        var rows = recorded.Select(x => new
        {
            x.Id, PublicId = (Guid?)x.PublicId, x.Barcode, x.Store, x.Submited, x.Reason, Recorded = true, x.TenantId
        });
        if (!dated)
            rows = rows.Concat(unrecorded.Select(b => new
            {
                Id = 0L, PublicId = (Guid?)null, Barcode = b.BarcodeValue, Store = (long?)b.Store, Submited = (DateTime?)null,
                Reason = (string?)null, Recorded = false, b.TenantId
            }));

        var total = await rows.CountAsync();
        var ordered = rows.OrderByDescending(x => x.Recorded).ThenByDescending(x => x.Submited).ThenBy(x => x.Barcode);
        var size = paged ? Math.Clamp(r.PageSize <= 0 ? 10 : r.PageSize, 1, MaxPageSize) : 0;
        var page = Math.Max(1, r.PageIndex);
        var list = paged ? await ordered.Skip((page - 1) * size).Take(size).ToListAsync() : await ordered.ToListAsync();

        // Nhan đề theo (mã, đơn vị); mã trùng (dữ liệu cũ) ưu tiên bản chưa xoá — trước đây ToDictionary trùng khoá → 500.
        var codesInPage = list.Where(x => x.Barcode != null).Select(x => x.Barcode!).Distinct().ToList();
        var bibByCode = (await db.Barcodes.Where(b => b.BarcodeValue != null && codesInPage.Contains(b.BarcodeValue) && b.BibId != null)
                .Select(b => new { b.BarcodeValue, b.TenantId, b.BibId, b.IsDelete }).ToListAsync())
            .GroupBy(b => (b.BarcodeValue!, b.TenantId ?? 0)).ToDictionary(g => g.Key, g => g.OrderBy(b => b.IsDelete == 2).First().BibId!.Value);
        var bibIds2 = bibByCode.Values.Distinct().ToList();
        var titles = new Dictionary<long, string?>();
        foreach (var chunk in bibIds2.Chunk(5000))
            foreach (var x in await db.BibXmls.Where(x => chunk.Contains(x.BibId)).Select(x => new { x.BibId, x.Title }).ToListAsync())
                titles.TryAdd(x.BibId, x.Title);
        var storeIds = list.Where(x => x.Store.HasValue).Select(x => x.Store!.Value).Distinct().ToList();
        var stores = await db.Stores.Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
        var tenantNames = await TenantScopeHelper.GetTenantNamesAsync(db, list.Where(x => x.TenantId.HasValue).Select(x => x.TenantId!.Value));

        return new PagedResult<LostBookRow>
        {
            TotalCount = total, PageIndex = paged ? page : 1, PageSize = paged ? size : total,
            Items = list.Select(x => new LostBookRow(x.Id, x.PublicId, x.Barcode,
                x.Barcode != null && bibByCode.TryGetValue((x.Barcode, x.TenantId ?? 0), out var bib) ? titles.GetValueOrDefault(bib) : null,
                x.Store, x.Store is long s ? stores.GetValueOrDefault(s) : null, x.Submited, x.Reason, x.Recorded,
                x.TenantId, x.TenantId is long t ? tenantNames.GetValueOrDefault(t) : null)).ToList()
        };
    }

    public async Task<ServiceResult<LostBookLookup>> LookupAsync(string? code, long? tenantId)
    {
        var (bc, error) = await FindAsync(code, tenantId);
        if (bc == null) return ServiceResult<LostBookLookup>.NotFound(error!);
        long? mfn = null; string? isbd = null;
        if (bc.BibId.HasValue)
        {
            mfn = await db.Bibs.Where(x => x.Bibid == bc.BibId).Select(x => x.Mfn).FirstOrDefaultAsync();
            isbd = await db.BibXmls.Where(x => x.BibId == bc.BibId).Select(x => x.Isbd).FirstOrDefaultAsync();
        }
        var storeName = bc.Store.HasValue ? await db.Stores.Where(x => x.Id == bc.Store).Select(x => x.Name).FirstOrDefaultAsync() : null;
        var statusName = !string.IsNullOrEmpty(bc.Status)
            ? await db.BarcodeStatuses.Where(x => x.Id == bc.Status).Select(x => x.CommentStatus).FirstOrDefaultAsync() : null;
        return ServiceResult<LostBookLookup>.Ok(new LostBookLookup(bc.BarcodeValue, mfn, bc.Store, storeName, bc.Status, statusName, isbd));
    }

    public async Task<ServiceResult<LostBook>> MarkLostAsync(string? code, DateTime? lossDate, string? reason, long? userId, long? tenantId)
    {
        var (barcode, error) = await FindAsync(code, tenantId);
        if (barcode == null) return ServiceResult<LostBook>.NotFound(error!);
        if (await db.LostBooks.AnyAsync(x => x.Barcode == barcode.BarcodeValue && x.TenantId == barcode.TenantId && x.IsDelete != 2))
            return ServiceResult<LostBook>.BadRequest("Tài liệu đã được đánh dấu thất lạc");
        if (barcode.Status == LiquidatedStatus) return ServiceResult<LostBook>.BadRequest("Tài liệu đã thanh lý");
        // Bản bạn đọc đang mượn mà làm mất: xử lý qua phiếu phạt (lý do "Mất tài liệu") để đóng phiếu mượn và thu tiền —
        // báo mất ở đây trước đây để phiếu mượn mở mãi trong khi ĐKCB đã "L".
        if (await PrintLoans.Open(db).AnyAsync(o => o.Barcode == barcode.BarcodeValue && (o.TenantId ?? 0) == (barcode.TenantId ?? 0)))
            return ServiceResult<LostBook>.BadRequest("Tài liệu đang được bạn đọc mượn — xử lý mất qua phiếu phạt ở màn hình Mượn/Trả");

        var now = LibraryClock.Now;
        var lost = new LostBook
        {
            Barcode = barcode.BarcodeValue, Submited = lossDate ?? now, Store = barcode.Store, Reason = reason, CreatedBy = userId,
            TenantId = barcode.TenantId, // bản ghi mất thuộc đơn vị của bản sách
            PublicId = Guid.NewGuid(), CreatedRowBy = userId, CreatedRowDate = now
        };
        db.LostBooks.Add(lost);
        barcode.Status = LostStatus; barcode.UpdateRowBy = userId; barcode.UpdatedRowDate = now;
        await db.SaveChangesAsync();
        return ServiceResult<LostBook>.Ok(lost);
    }

    public async Task<ServiceResult<bool>> RestoreAsync(string? code, long? userId, long? tenantId)
    {
        var (barcode, error) = await FindAsync(code, tenantId);
        if (barcode == null) return ServiceResult<bool>.NotFound(error!);
        // Chỉ bản ghi mất cùng đơn vị với bản sách.
        var rows = await db.LostBooks.Where(x => x.Barcode == barcode.BarcodeValue && x.TenantId == barcode.TenantId).ToListAsync();
        // Trước đây chỉ khôi phục được khi có bản ghi LostBook — ĐKCB "L" của dữ liệu cũ / phiếu phạt không khôi phục được.
        if (rows.Count == 0 && barcode.Status != LostStatus) return ServiceResult<bool>.NotFound("Tài liệu không ở trạng thái mất");

        db.LostBooks.RemoveRange(rows);
        // Chỉ trả về kho khi ĐKCB đang "Mất" — trước đây luôn ghi "R", đè lên trạng thái khác (đã thanh lý, đang ra kho…).
        // Dữ liệu cũ có mã nhập trùng 2 dòng (cùng đơn vị, cùng "L") — khôi phục cả các dòng trùng để không sót.
        var copies = await db.Barcodes.Where(b => b.BarcodeValue == barcode.BarcodeValue && b.TenantId == barcode.TenantId
                                                  && b.IsDelete != 2 && b.Status == LostStatus).ToListAsync();
        foreach (var copy in copies)
        {
            copy.Status = InStoreStatus; copy.UpdateRowBy = userId; copy.UpdatedRowDate = LibraryClock.Now;
        }
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>ĐKCB trong đơn vị (không phân biệt hoa thường). Tài khoản hệ thống mà mã có ở &gt;1 đơn vị → lỗi.</summary>
    private async Task<(Barcode? Barcode, string? Error)> FindAsync(string? code, long? tenantId)
    {
        code = (code ?? "").Trim();
        if (code == "") return (null, NotFound);
        var (bc, ambiguous) = await BarcodeTenantLookup.ByValueAsync(db.Barcodes, code, tenantId, normalize: true);
        if (ambiguous) return (null, BarcodeTenantLookup.AmbiguousMessage);
        return bc == null ? (null, NotFound) : (bc, null);
    }
}
