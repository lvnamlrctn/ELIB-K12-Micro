using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Lọc sách mất / thanh lý: Keyword = đầu số ĐKCB hoặc một phần nhan đề; From/To theo ngày mất / ngày thanh lý.</summary>
public sealed class StoreRecordSearch : CrudSearch
{
    public long? StoreId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>Dòng sách mất: mã lúc báo mất, bản sách hiện tại (null khi đã xoá), trạng thái hiện tại của bản sách.</summary>
public sealed record LostRow(
    long Id, Guid PublicId, string Barcode, Guid? ItemPublicId, long? Mfn, string? Title, string? Author, long? StoreId, string? StoreName,
    DateOnly LostOn, string? Reason, string? CardNo, string? ItemStatus, DateTimeOffset CreatedAt);

public sealed record MarkLostRequest(IReadOnlyList<string>? Barcodes = null, DateOnly? LostOn = null, string? Reason = null);

public sealed record LiquidationRow(
    long Id, Guid PublicId, string Barcode, Guid? ItemPublicId, long? Mfn, string? Title, string? Author, string? Publisher, long? StoreId,
    string? StoreName, DateOnly LiquidatedOn, string? Reason, string? ItemStatus, DateTimeOffset CreatedAt);

public sealed record LiquidateRequest(IReadOnlyList<string>? Barcodes = null, DateOnly? LiquidatedOn = null, string? Reason = null);

/// <summary>
/// Xử lý sách mất (monolith: StoreLostBookController + LostBookService, quyền LOST_BOOKS): báo mất theo lô ĐKCB, khôi phục khi tìm
/// thấy, danh sách + xuất Excel. Bản đang mượn không báo mất ở đây — xử lý qua phiếu phạt ở circulation (lượt mượn đóng vì mất
/// cũng ghi bản ghi báo mất, xem <see cref="ItemResource.ApplyLoanAsync"/>).
/// </summary>
public sealed class LostBooks(ICrudDbContext db, ItemEvents events, HoldingsExcel excel, TimeProvider clock, ICrudAuditSink? audit = null)
{
    private const string EntityName = "Bản sách";

    public async Task<CrudPage<LostRow>> SearchAsync(StoreRecordSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = Rows(search);
        var (page, size) = (Math.Max(1, search.PageIndex), Math.Clamp(search.PageSize, 1, 500));
        return new CrudPage<LostRow>(await query.Skip((page - 1) * size).Take(size).ToListAsync(ct), await query.CountAsync(ct), page, size);
    }

    public async Task<ExcelFile> ExportAsync(StoreRecordSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var rows = await Rows(search).Take(HoldingsExcel.MaxRows).ToListAsync(ct);
        return await excel.BuildAsync("sach-mat", ct, new ExcelSheet("Sách mất", "DANH SÁCH TÀI LIỆU MẤT", HoldingsExcel.Period(search.From, search.To),
            ["Đăng ký cá biệt", "Nhan đề", "Tác giả", "Kho", "Ngày mất", "Lý do", "Thẻ bạn đọc", "Trạng thái hiện tại"],
            rows.Select(r => (IReadOnlyList<object?>)[r.Barcode, r.Title, r.Author, r.StoreName, r.LostOn, r.Reason, r.CardNo,
                r.ItemStatus is null ? "(đã xoá)" : ItemStatus.Names.GetValueOrDefault(r.ItemStatus, r.ItemStatus)])));
    }

    /// <summary>Báo mất theo lô: bản đang mượn / đã mất / đã thanh lý bị bỏ qua kèm lý do; các bản còn lại lưu một lần.</summary>
    public async Task<ItemBatchResult> MarkLostAsync(MarkLostRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (items, notFound) = await ItemBarcodes.LoadAsync(db, request.Barcodes, ct);
        var lostOn = request.LostOn ?? VietnamDate.Of(clock.GetUtcNow());
        var skipped = new List<SkippedItem>();
        var done = 0;
        foreach (var item in items)
        {
            var record = LostRecord.For(item, lostOn, request.Reason); // lý do sai → lỗi cả request, không phải lỗi từng bản
            try
            {
                item.MarkLost();
            }
            catch (BusinessRuleException ex)
            {
                skipped.Add(new SkippedItem(item.Barcode, ex.Message));
                continue;
            }
            db.Set<LostRecord>().Add(record);
            await events.PublishAsync(item, deleted: false, ct);
            await AuditAsync(item, $"ĐKCB {item.Barcode}: báo mất" + (record.Reason is { } r ? $" ({r})" : ""), ct);
            done++;
        }
        await db.SaveChangesAsync(ct);
        return new ItemBatchResult(done, notFound, skipped);
    }

    /// <summary>Tìm thấy lại bản đã báo mất: xoá bản ghi báo mất, bản sách về sẵn sàng phục vụ.</summary>
    public async Task RestoreAsync(BarcodeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemBarcodes.RequireAsync(db, request.Barcode, ct);
        item.RestoreFound();
        foreach (var record in await db.Set<LostRecord>().Where(x => x.ItemId == item.Id).ToListAsync(ct)) db.Set<LostRecord>().Remove(record);
        await events.PublishAsync(item, deleted: false, ct);
        await AuditAsync(item, $"ĐKCB {item.Barcode}: khôi phục mất (tìm thấy)", ct);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<LostRow> Rows(StoreRecordSearch s)
    {
        HoldingsExcel.EnsureRange(s.From, s.To);
        var records = db.Set<LostRecord>().AsNoTracking();
        if (s.StoreId is { } store) records = records.Where(x => x.StoreId == store);
        if (s.From is { } from) records = records.Where(x => x.LostOn >= from);
        if (s.To is { } to) records = records.Where(x => x.LostOn <= to);
        var query =
            from r in records
            join i in db.Set<Item>() on r.ItemId equals i.Id into ij
            from i in ij.DefaultIfEmpty()
            join b in db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
            from b in bj.DefaultIfEmpty()
            join st in db.Set<Store>() on r.StoreId equals (long?)st.Id into sj
            from st in sj.DefaultIfEmpty()
            select new { r, i, b, st };
        if (s.Term is { } term)
        {
            var key = Item.Key(term);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToUpper()/ToLower() sang upper()/lower() của SQL
            query = query.Where(x => x.r.Barcode.ToUpper().StartsWith(key) || (x.b != null && x.b.Title.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query.OrderByDescending(x => x.r.LostOn).ThenByDescending(x => x.r.Id).Select(x => new LostRow(
            x.r.Id, x.r.PublicId, x.r.Barcode, x.i == null ? null : x.i.PublicId, x.i == null ? null : x.i.BibId, x.b == null ? null : x.b.Title,
            x.b == null ? null : x.b.Author, x.r.StoreId, x.st == null ? null : x.st.Name, x.r.LostOn, x.r.Reason, x.r.CardNo,
            x.i == null ? null : x.i.Status, x.r.CreatedAt));
    }

    private Task AuditAsync(Item item, string label, CancellationToken ct) => audit is null
        ? Task.CompletedTask
        : audit.RecordAsync(new CrudAuditEntry(nameof(Item), EntityName, item.PublicId, CrudChange.Updated, label), ct);
}

/// <summary>
/// Thanh lý (monolith: StoreLiquidateController + LiquidateService, quyền LIQUIDATES): thanh lý theo lô ĐKCB (monolith từng mã một),
/// huỷ thanh lý, danh sách + xuất Excel. Chặn bản đang mượn, đang xuất kho; bản đã báo mất thanh lý được (huỷ thì về lại "Mất").
/// </summary>
public sealed class Liquidations(ICrudDbContext db, ItemEvents events, HoldingsExcel excel, TimeProvider clock, ICrudAuditSink? audit = null)
{
    private const string EntityName = "Bản sách";

    public async Task<CrudPage<LiquidationRow>> SearchAsync(StoreRecordSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = Rows(search);
        var (page, size) = (Math.Max(1, search.PageIndex), Math.Clamp(search.PageSize, 1, 500));
        return new CrudPage<LiquidationRow>(await query.Skip((page - 1) * size).Take(size).ToListAsync(ct), await query.CountAsync(ct), page, size);
    }

    public async Task<ExcelFile> ExportAsync(StoreRecordSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var rows = await Rows(search).Take(HoldingsExcel.MaxRows).ToListAsync(ct);
        return await excel.BuildAsync("thanh-ly", ct, new ExcelSheet("Thanh lý", "DANH SÁCH TÀI LIỆU THANH LÝ", HoldingsExcel.Period(search.From, search.To),
            ["Đăng ký cá biệt", "Nhan đề", "Tác giả", "Nhà xuất bản", "Kho", "Ngày thanh lý", "Lý do thanh lý"],
            rows.Select(r => (IReadOnlyList<object?>)[r.Barcode, r.Title, r.Author, r.Publisher, r.StoreName, r.LiquidatedOn, r.Reason])));
    }

    public async Task<ItemBatchResult> LiquidateAsync(LiquidateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (items, notFound) = await ItemBarcodes.LoadAsync(db, request.Barcodes, ct);
        var on = request.LiquidatedOn ?? VietnamDate.Of(clock.GetUtcNow());
        var skipped = new List<SkippedItem>();
        var done = 0;
        foreach (var item in items)
        {
            var record = Liquidation.For(item, on, request.Reason);
            try
            {
                item.Liquidate();
            }
            catch (BusinessRuleException ex)
            {
                skipped.Add(new SkippedItem(item.Barcode, ex.Message));
                continue;
            }
            db.Set<Liquidation>().Add(record);
            await events.PublishAsync(item, deleted: false, ct);
            await AuditAsync(item, $"ĐKCB {item.Barcode}: thanh lý" + (record.Reason is { } r ? $" ({r})" : ""), ct);
            done++;
        }
        await db.SaveChangesAsync(ct);
        return new ItemBatchResult(done, notFound, skipped);
    }

    /// <summary>Huỷ thanh lý: xoá bản ghi thanh lý; bản sách về "Mất" nếu còn bản ghi báo mất, ngược lại về sẵn sàng.</summary>
    public async Task CancelAsync(BarcodeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemBarcodes.RequireAsync(db, request.Barcode, ct);
        var records = await db.Set<Liquidation>().Where(x => x.ItemId == item.Id).ToListAsync(ct);
        if (records.Count == 0 && item.Status != ItemStatus.Liquidated)
            throw new NotFoundException("bản ghi thanh lý của ĐKCB", item.Barcode);
        item.CancelLiquidation(stillLost: await db.Set<LostRecord>().AnyAsync(x => x.ItemId == item.Id, ct));
        foreach (var record in records) db.Set<Liquidation>().Remove(record);
        await events.PublishAsync(item, deleted: false, ct);
        await AuditAsync(item, $"ĐKCB {item.Barcode}: huỷ thanh lý", ct);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<LiquidationRow> Rows(StoreRecordSearch s)
    {
        HoldingsExcel.EnsureRange(s.From, s.To);
        var records = db.Set<Liquidation>().AsNoTracking();
        if (s.StoreId is { } store) records = records.Where(x => x.StoreId == store);
        if (s.From is { } from) records = records.Where(x => x.LiquidatedOn >= from);
        if (s.To is { } to) records = records.Where(x => x.LiquidatedOn <= to);
        var query =
            from r in records
            join i in db.Set<Item>() on r.ItemId equals i.Id into ij
            from i in ij.DefaultIfEmpty()
            join b in db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
            from b in bj.DefaultIfEmpty()
            join st in db.Set<Store>() on r.StoreId equals (long?)st.Id into sj
            from st in sj.DefaultIfEmpty()
            select new { r, i, b, st };
        if (s.Term is { } term)
        {
            var key = Item.Key(term);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToUpper()/ToLower() sang upper()/lower() của SQL
            query = query.Where(x => x.r.Barcode.ToUpper().StartsWith(key) || (x.b != null && x.b.Title.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query.OrderByDescending(x => x.r.LiquidatedOn).ThenByDescending(x => x.r.Id).Select(x => new LiquidationRow(
            x.r.Id, x.r.PublicId, x.r.Barcode, x.i == null ? null : x.i.PublicId, x.i == null ? null : x.i.BibId, x.b == null ? null : x.b.Title,
            x.b == null ? null : x.b.Author, x.b == null ? null : x.b.Publisher, x.r.StoreId, x.st == null ? null : x.st.Name, x.r.LiquidatedOn,
            x.r.Reason, x.i == null ? null : x.i.Status, x.r.CreatedAt));
    }

    private Task AuditAsync(Item item, string label, CancellationToken ct) => audit is null
        ? Task.CompletedTask
        : audit.RecordAsync(new CrudAuditEntry(nameof(Item), EntityName, item.PublicId, CrudChange.Updated, label), ct);
}
