using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Xem <see cref="IStoreMoveService"/>. Port ELIB-LRC 10-04 (tách từ <c>CatalogueMoveController</c>), giữ lọc đơn vị
/// của K12. Bổ sung bước "Hoàn thành điều chuyển" — trước đây phiếu chỉ ghi nhận danh sách, <c>Barcode.Store</c> không bao
/// giờ đổi sang kho nhận.</summary>
public class StoreMoveService(ELIBAPIDbContext db) : IStoreMoveService
{
    public const int MaxPageSize = 200;
    /// <summary>ab_move.Status: phiếu đã hoàn thành (ĐKCB đã chuyển sang kho nhận) — khoá, không sửa/xoá.</summary>
    public const int Completed = 2;
    private const string MoveNotFound = "Không tìm thấy phiếu điều chuyển";
    private const string LockedMessage = "Phiếu điều chuyển đã hoàn thành, không sửa được";

    public async Task<string?> ValidateAsync(AbMoveRequest r, Guid? publicId, long? tenantId)
    {
        if (r.StoreDeliver_Id.HasValue && r.StoreDeliver_Id == r.StoreReceipt_Id) return "Kho nhận phải khác kho nguồn";
        // Kho của đơn vị khác: user thường chỉ được chọn kho của đơn vị mình hoặc kho dùng chung.
        if (tenantId.HasValue)
            foreach (var storeId in new[] { r.StoreDeliver_Id, r.StoreReceipt_Id }.Where(s => s.HasValue))
                if (!await db.Stores.AnyAsync(s => s.Id == storeId && s.IsDelete != 2 && (s.TenantId == tenantId || s.TenantId == null)))
                    return "Kho không thuộc đơn vị của bạn";
        if (publicId is not Guid id) return null;
        if (await LockedErrorAsync(movePublicId: id) is string locked) return locked;
        var move = await db.AbMoves.Where(x => x.PublicId == id && x.IsDelete != 2).Select(x => new { x.Id, x.StoreDeliver_Id }).FirstOrDefaultAsync();
        if (move == null || move.StoreDeliver_Id == r.StoreDeliver_Id) return null;
        // Các dòng đã chọn thuộc kho nguồn cũ — đổi kho nguồn sẽ làm phiếu chứa tài liệu không ở kho nguồn mới.
        return await db.AbMoveDetails.AnyAsync(x => x.Move_Id == move.Id && x.IsDelete != 2)
            ? "Phiếu đã có tài liệu, không đổi được kho nguồn — xoá các dòng trước"
            : null;
    }

    public async Task SyncCodeAsync(long moveId)
    {
        var move = await db.AbMoves.FirstOrDefaultAsync(x => x.Id == moveId);
        if (move == null || move.Code == move.Id) return;
        move.Code = move.Id;
        await db.SaveChangesAsync();
    }

    private Task<AbMove?> FindAsync(long moveId, long? tenantId) =>
        db.AbMoves.FirstOrDefaultAsync(x => x.Id == moveId && x.IsDelete != 2 && (!tenantId.HasValue || x.TenantId == tenantId));

    private static string? Low(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLower();

    public async Task<ServiceResult<(List<MoveCandidate> Items, int Total)>> SearchDocumentsAsync(MoveDocumentQuery r, long? tenantId)
    {
        var move = await FindAsync(r.MoveId, tenantId);
        // Phiếu không tồn tại trước đây báo sai "chưa có Kho nguồn".
        if (move == null) return ServiceResult<(List<MoveCandidate>, int)>.NotFound(MoveNotFound);
        if (!move.StoreDeliver_Id.HasValue) return ServiceResult<(List<MoveCandidate>, int)>.BadRequest("Phiếu điều chuyển chưa có Kho nguồn");

        // Khóa cứng Kho nguồn theo phiếu (lấy từ server) — không nhận StoreId từ client. ĐKCB cùng đơn vị với phiếu.
        var query = db.Barcodes.Where(x => x.IsDelete != 2 && x.Store == move.StoreDeliver_Id
                                           && (move.TenantId == null || x.TenantId == move.TenantId));
        var already = db.AbMoveDetails.Where(x => x.Move_Id == r.MoveId && x.IsDelete != 2).Select(x => x.BarcodeId);
        query = query.Where(x => !already.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(r.BarcodeFrom)) query = query.Where(x => string.Compare(x.BarcodeValue, r.BarcodeFrom.Trim()) >= 0);
        if (!string.IsNullOrWhiteSpace(r.BarcodeTo)) query = query.Where(x => string.Compare(x.BarcodeValue, r.BarcodeTo.Trim()) <= 0);
        if (r.MfnFrom.HasValue || r.MfnTo.HasValue)
        {
            var byMfn = db.Bibs.Where(b => b.IsDelete != 2 && (!r.MfnFrom.HasValue || b.Mfn >= r.MfnFrom) && (!r.MfnTo.HasValue || b.Mfn <= r.MfnTo))
                .Select(b => b.Bibid);
            query = query.Where(x => x.BibId.HasValue && byMfn.Contains(x.BibId.Value));
        }
        // Không phân biệt hoa thường (trước đây phân biệt).
        var (title, author, publisher, year) = (Low(r.Title), Low(r.Author), Low(r.Publisher), Low(r.PublishYear));
        if (title != null || author != null || publisher != null || year != null)
        {
            var byXml = db.BibXmls.Where(x =>
                (title == null || (x.Title != null && x.Title.ToLower().Contains(title))) &&
                (author == null || (x.Author != null && x.Author.ToLower().Contains(author))) &&
                (publisher == null || (x.Publisher != null && x.Publisher.ToLower().Contains(publisher))) &&
                (year == null || (x.PublishDate != null && x.PublishDate.Contains(year)))).Select(x => x.BibId);
            query = query.Where(x => x.BibId.HasValue && byXml.Contains(x.BibId.Value));
        }

        var total = await query.CountAsync();
        var size = Math.Clamp(r.PageSize ?? 10, 1, MaxPageSize);
        var page = Math.Max(1, r.PageIndex ?? 1);
        var raw = await query.OrderBy(x => x.BarcodeValue).Skip((page - 1) * size).Take(size)
            .Select(x => new { x.Id, x.BarcodeValue, x.BibId }).ToListAsync();

        var bibIds = raw.Where(x => x.BibId.HasValue).Select(x => x.BibId!.Value).Distinct().ToList();
        var mfns = await db.Bibs.Where(b => bibIds.Contains(b.Bibid)).ToDictionaryAsync(b => b.Bibid, b => b.Mfn);
        var xmls = await db.BibXmls.Where(x => bibIds.Contains(x.BibId))
            .Select(x => new { x.BibId, x.Title, x.Author, x.Publisher, x.PublishDate }).ToDictionaryAsync(x => x.BibId);

        return ServiceResult<(List<MoveCandidate>, int)>.Ok((raw.Select(x =>
        {
            var xml = x.BibId is long b ? xmls.GetValueOrDefault(b) : null;
            return new MoveCandidate(x.Id, x.BarcodeValue, x.BibId is long m ? mfns.GetValueOrDefault(m) : null,
                xml?.Title, xml?.Author, xml?.Publisher, xml?.PublishDate);
        }).ToList(), total));
    }

    public async Task<ServiceResult<List<MoveLine>>> LinesAsync(long moveId, long? tenantId)
    {
        if (await FindAsync(moveId, tenantId) == null) return ServiceResult<List<MoveLine>>.NotFound(MoveNotFound);
        var details = await db.AbMoveDetails.Where(x => x.Move_Id == moveId && x.IsDelete != 2).OrderBy(x => x.Id).ToListAsync();
        var barcodeIds = details.Where(x => x.BarcodeId.HasValue).Select(x => x.BarcodeId!.Value).Distinct().ToList();
        var barcodes = await db.Barcodes.Where(b => barcodeIds.Contains(b.Id)).Select(b => new { b.Id, b.BarcodeValue, b.BibId }).ToDictionaryAsync(b => b.Id);
        var bibIds = barcodes.Values.Where(b => b.BibId.HasValue).Select(b => b.BibId!.Value).Distinct().ToList();
        var xmls = await db.BibXmls.Where(x => bibIds.Contains(x.BibId)).Select(x => new { x.BibId, x.Title, x.Author }).ToDictionaryAsync(x => x.BibId);
        return ServiceResult<List<MoveLine>>.Ok(details.Select(d =>
        {
            var bc = d.BarcodeId is long id ? barcodes.GetValueOrDefault(id) : null;
            var xml = bc?.BibId is long bib ? xmls.GetValueOrDefault(bib) : null;
            return new MoveLine(d.Id, d.PublicId, bc?.BarcodeValue, xml?.Title, xml?.Author);
        }).ToList());
    }

    public async Task<ServiceResult<MoveAddResult>> AddDetailsAsync(long moveId, IReadOnlyCollection<long> barcodeIds, long? userId, long? tenantId)
    {
        if (barcodeIds == null || barcodeIds.Count == 0) return ServiceResult<MoveAddResult>.BadRequest("Danh sách tài liệu không được rỗng");
        var move = await FindAsync(moveId, tenantId);
        if (move == null) return ServiceResult<MoveAddResult>.NotFound(MoveNotFound);
        if (move.Status == Completed) return ServiceResult<MoveAddResult>.BadRequest(LockedMessage);
        if (!move.StoreDeliver_Id.HasValue) return ServiceResult<MoveAddResult>.BadRequest("Phiếu điều chuyển chưa có Kho nguồn");

        var requested = barcodeIds.Distinct().ToList();
        var existing = await db.AbMoveDetails.Where(x => x.Move_Id == moveId && x.IsDelete != 2 && x.BarcodeId.HasValue)
            .Select(x => x.BarcodeId!.Value).ToListAsync();
        // Trước đây nhận mọi Id client gửi (kể cả ĐKCB không tồn tại, đã xoá, thuộc kho khác kho nguồn hoặc đơn vị khác).
        var valid = await db.Barcodes.Where(b => requested.Contains(b.Id) && b.IsDelete != 2 && b.Store == move.StoreDeliver_Id
                                                 && (move.TenantId == null || b.TenantId == move.TenantId))
            .Select(b => b.Id).ToListAsync();
        var toAdd = valid.Except(existing).ToList();

        var now = LibraryClock.Now;
        foreach (var id in toAdd)
            db.AbMoveDetails.Add(new AbMoveDetail
            {
                Move_Id = moveId, BarcodeId = id, Store_Id = move.StoreDeliver_Id, PublicId = Guid.NewGuid(),
                TenantId = move.TenantId, CreatedRowBy = userId, CreatedRowDate = now
            });
        await db.SaveChangesAsync();
        return ServiceResult<MoveAddResult>.Ok(new MoveAddResult(toAdd.Count, requested.Count - toAdd.Count));
    }

    public async Task<string?> LockedErrorAsync(Guid? movePublicId = null, Guid? linePublicId = null, long? moveId = null)
    {
        if (linePublicId is Guid line)
            moveId = await db.AbMoveDetails.Where(x => x.PublicId == line).Select(x => x.Move_Id).FirstOrDefaultAsync();
        var q = db.AbMoves.Where(x => x.IsDelete != 2);
        q = movePublicId is Guid m ? q.Where(x => x.PublicId == m) : moveId is long id ? q.Where(x => x.Id == id) : q.Where(_ => false);
        return await q.AnyAsync(x => x.Status == Completed) ? LockedMessage : null;
    }

    /// <summary>Trạng thái ĐKCB không chuyển kho được: đang mượn, đang ra kho, đã mất, đã thanh lý.</summary>
    private static readonly Dictionary<string, string> Unmovable = new()
    {
        ["B"] = "đang được mượn", ["X"] = "đang ra khỏi kho (xuất kho / xử lý kỹ thuật)", ["L"] = "đã mất", ["S"] = "đã thanh lý",
    };

    public async Task<ServiceResult<MoveCompleteResult>> CompleteAsync(long moveId, long? userId, long? tenantId)
    {
        var move = await FindAsync(moveId, tenantId);
        if (move == null) return ServiceResult<MoveCompleteResult>.NotFound(MoveNotFound);
        if (move.Status == Completed) return ServiceResult<MoveCompleteResult>.BadRequest("Phiếu điều chuyển đã hoàn thành trước đó");
        if (!move.StoreDeliver_Id.HasValue || !move.StoreReceipt_Id.HasValue)
            return ServiceResult<MoveCompleteResult>.BadRequest("Phiếu điều chuyển phải có cả kho nguồn và kho nhận");

        var ids = await db.AbMoveDetails.Where(x => x.Move_Id == moveId && x.IsDelete != 2 && x.BarcodeId.HasValue)
            .Select(x => x.BarcodeId!.Value).Distinct().ToListAsync();
        if (ids.Count == 0) return ServiceResult<MoveCompleteResult>.BadRequest("Phiếu chưa có tài liệu nào");

        var copies = await db.Barcodes.Where(b => ids.Contains(b.Id)).ToListAsync();
        var values = copies.Where(b => b.BarcodeValue != null).Select(b => b.BarcodeValue!).Distinct().ToList();
        // Phiếu mượn mở theo (mã, đơn vị) — mã ĐKCB chỉ duy nhất trong 1 đơn vị.
        var onLoan = (await PrintLoans.Open(db).Where(o => o.Barcode != null && values.Contains(o.Barcode))
                .Select(o => new { o.Barcode, o.TenantId }).ToListAsync())
            .Select(o => (o.Barcode!, o.TenantId ?? 0)).ToHashSet();

        var now = LibraryClock.Now;
        var skipped = new List<MoveSkippedCopy>();
        var moved = 0;
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        foreach (var b in copies.OrderBy(b => b.BarcodeValue))
        {
            string? reason =
                b.IsDelete == 2 ? "đã bị xoá"
                : b.Store != move.StoreDeliver_Id ? "không còn ở kho nguồn"
                : move.TenantId != null && b.TenantId != move.TenantId ? "thuộc đơn vị khác"
                : b.Status != null && Unmovable.TryGetValue(b.Status, out var why) ? why
                : b.BarcodeValue != null && onLoan.Contains((b.BarcodeValue, b.TenantId ?? 0)) ? Unmovable["B"]
                : null;
            if (reason != null) { skipped.Add(new MoveSkippedCopy(b.BarcodeValue, reason)); continue; }

            b.Store = move.StoreReceipt_Id;
            // Vị trí giá/ngăn thuộc kho cũ — giữ lại sẽ chỉ sai chỗ trên sơ đồ OPAC; xếp giá lại ở kho mới.
            b.MapShelfRowId = null; b.MapObjectId = null;
            b.UpdateRowBy = userId; b.UpdatedRowDate = now;
            moved++;
        }
        move.Status = Completed;
        move.ReceiptDate ??= now;
        move.UserIdReceipt ??= userId is long u && u <= int.MaxValue ? (int)u : null;
        move.UpdateRowBy = userId; move.UpdatedRowDate = now;
        await db.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return ServiceResult<MoveCompleteResult>.Ok(new MoveCompleteResult(moved, skipped));
    }
}
