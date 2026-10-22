using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Lý do xuất kho (monolith: DExportReasonController, quyền EXPORT_REASON).</summary>
public sealed class ExportReasonResource(ICrudDbContext db) : NamedCatalogResource<ExportReasonResource, ExportReason>(db)
{
    public static readonly IReadOnlyList<string> Defaults = ["Photo tài liệu", "Triển lãm, trưng bày", "Cho đơn vị khác mượn", "Sửa chữa, đóng gáy"];

    protected override string EntityName => "Lý do xuất kho";

    /// <summary>Đơn vị mới: lý do xuất kho mặc định (bỏ qua lý do đã có cùng tên).</summary>
    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var name in Defaults.Where(n => !existing.Contains(n)))
        {
            Set.Add(NamedCatalogItem.New<ExportReason>(name));
            added++;
        }
        await Db.SaveChangesAsync(ct);
        return added;
    }

    protected override async Task EnsureDeletableAsync(ExportReason entity, CancellationToken ct)
    {
        if (await Db.Set<StoreOut>().AnyAsync(x => x.ReasonId == entity.Id, ct))
            throw new ConflictException("EXPORT_REASON_IN_USE", $"Lý do '{entity.Name}' đã dùng cho lượt xuất kho, không xoá được.");
    }
}

/// <summary>Đơn vị nhận sách (monolith: DBookOutUnitController, quyền BOOK_OUT_UNIT).</summary>
public sealed class BookOutUnitResource(ICrudDbContext db) : NamedCatalogResource<BookOutUnitResource, BookOutUnit>(db)
{
    protected override string EntityName => "Đơn vị nhận sách";

    protected override async Task EnsureDeletableAsync(BookOutUnit entity, CancellationToken ct)
    {
        if (await Db.Set<StoreOut>().AnyAsync(x => x.UnitId == entity.Id, ct))
            throw new ConflictException("BOOK_OUT_UNIT_IN_USE", $"Đơn vị '{entity.Name}' đã dùng cho lượt xuất kho, không xoá được.");
    }
}

/// <summary>Địa điểm triển lãm (monolith: DExhibitionLocationController, quyền EXHIBITION_LOCATION).</summary>
public sealed class ExhibitionLocationResource(ICrudDbContext db) : NamedCatalogResource<ExhibitionLocationResource, ExhibitionLocation>(db)
{
    protected override string EntityName => "Địa điểm triển lãm";

    protected override async Task EnsureDeletableAsync(ExhibitionLocation entity, CancellationToken ct)
    {
        if (await Db.Set<StoreOut>().AnyAsync(x => x.ExhibitionLocationId == entity.Id, ct))
            throw new ConflictException("EXHIBITION_LOCATION_IN_USE", $"Địa điểm '{entity.Name}' đã dùng cho lượt xuất kho, không xoá được.");
    }
}

/// <summary>
/// Lịch sử ra vào kho. Keyword: tên người giao/nhận; <see cref="Barcode"/>: một phần số ĐKCB; <see cref="Open"/>: true = đang ở ngoài,
/// false = đã nhập lại. Khoảng ngày theo ngày xuất / ngày nhập (giờ VN).
/// </summary>
public sealed class StoreOutSearch : CrudSearch
{
    public string? Barcode { get; set; }
    public bool? Open { get; set; }
    public long? ReasonId { get; set; }
    public long? UnitId { get; set; }
    public long? ExhibitionLocationId { get; set; }
    public bool? ReturnAtLibrary { get; set; }
    public DateOnly? OutFrom { get; set; }
    public DateOnly? OutTo { get; set; }
    public DateOnly? InFrom { get; set; }
    public DateOnly? InTo { get; set; }
}

public sealed record StoreOutRow(
    long Id, Guid PublicId, string Barcode, Guid? ItemPublicId, long? Mfn, string? Title, long? StoreId, string? StoreName,
    long? ReasonId, string? ReasonName, long? UnitId, string? UnitName, long? ExhibitionLocationId, string? ExhibitionLocationName,
    string? DelivererName, string? ReceiverName, bool ReturnAtLibrary, DateTimeOffset? OutAt, DateTimeOffset? InAt);

public sealed record ScanOutRequest(
    string? Barcode, long? ReasonId = null, long? UnitId = null, long? ExhibitionLocationId = null, string? DelivererName = null,
    string? ReceiverName = null, bool ReturnAtLibrary = false);

/// <summary>
/// Sách ra vào kho (monolith: BookOutStoreController, quyền BOOK_OUT_STORE): quét ĐKCB xuất kho (bản sang "X", không cho mượn), quét
/// lại để nhập kho. Bản "X" của dữ liệu cũ không có lượt xuất vẫn nhập được (ghi một lượt chỉ có ngày nhập). Xoá lượt còn mở thì bản
/// sách về kho.
/// </summary>
public sealed class StoreOuts(ICrudDbContext db, ItemEvents events, HoldingsExcel excel, TimeProvider clock, CrudAuditContext? auditContext = null, ICrudAuditSink? audit = null)
{
    public async Task<CrudPage<StoreOutRow>> SearchAsync(StoreOutSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = Rows(search);
        var (page, size) = (Math.Max(1, search.PageIndex), Math.Clamp(search.PageSize, 1, 500));
        return new CrudPage<StoreOutRow>(await query.Skip((page - 1) * size).Take(size).ToListAsync(ct), await query.CountAsync(ct), page, size);
    }

    public async Task<ExcelFile> ExportAsync(StoreOutSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var rows = await Rows(search).Take(HoldingsExcel.MaxRows).ToListAsync(ct);
        return await excel.BuildAsync("sach-ra-vao-kho", ct, new ExcelSheet("Sách ra vào kho", "DANH SÁCH SÁCH RA VÀO KHO",
            HoldingsExcel.Period(search.OutFrom, search.OutTo),
            ["Số ĐKCB", "Nhan đề", "Kho", "Lý do", "Đơn vị nhận", "Địa điểm triển lãm", "Người giao", "Người nhận", "Ngày xuất", "Ngày nhập"],
            rows.Select(r => (IReadOnlyList<object?>)[r.Barcode, r.Title, r.StoreName, r.ReasonName, r.UnitName, r.ExhibitionLocationName,
                r.DelivererName, r.ReceiverName, r.OutAt, r.InAt])));
    }

    public async Task<StoreOutRow> ScanOutAsync(ScanOutRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemBarcodes.RequireAsync(db, request.Barcode, ct);
        await EnsureExistsAsync<ExportReason>(request.ReasonId, "Lý do xuất kho", ct);
        await EnsureExistsAsync<BookOutUnit>(request.UnitId, "Đơn vị nhận sách", ct);
        await EnsureExistsAsync<ExhibitionLocation>(request.ExhibitionLocationId, "Địa điểm triển lãm", ct);
        var tx = StoreOut.Open(item, clock.GetUtcNow(), request.ReasonId, request.UnitId, request.ExhibitionLocationId, request.DelivererName,
            request.ReceiverName, request.ReturnAtLibrary);
        item.TakeOut();
        db.Set<StoreOut>().Add(tx);
        await events.PublishAsync(item, deleted: false, ct);
        await AuditAsync(item, $"ĐKCB {item.Barcode}: xuất kho", ct);
        await db.SaveChangesAsync(ct);
        return await Rows(new StoreOutSearch(), tx.Id).FirstAsync(ct);
    }

    public async Task<StoreOutRow> ScanInAsync(BarcodeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await ItemBarcodes.RequireAsync(db, request.Barcode, ct);
        var tx = await db.Set<StoreOut>().Where(x => x.ItemId == item.Id && x.InAt == null).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (tx is null)
        {
            if (item.Status != ItemStatus.OutOfStore)
                throw new BusinessRuleException("STORE_OUT_NOT_OPEN", $"ĐKCB {item.Barcode} không có lượt xuất kho đang mở.");
            db.Set<StoreOut>().Add(tx = StoreOut.ReturnOnly(item));
        }
        tx.Close(clock.GetUtcNow());
        if (item.Status == ItemStatus.OutOfStore)
        {
            item.BringBack();
            await events.PublishAsync(item, deleted: false, ct);
        }
        await AuditAsync(item, $"ĐKCB {item.Barcode}: nhập kho", ct);
        await db.SaveChangesAsync(ct);
        return await Rows(new StoreOutSearch(), tx.Id).FirstAsync(ct);
    }

    /// <summary>Xoá một lượt (nhập nhầm). Lượt còn mở: bản sách về kho nếu không còn lượt mở nào khác.</summary>
    public async Task DeleteAsync(Guid publicId, CancellationToken ct)
    {
        var reason = ChangeReason.Require(null, auditContext?.Capture().Reason, "xoá lượt ra vào kho");
        var tx = await db.Set<StoreOut>().FirstOrDefaultAsync(x => x.PublicId == publicId, ct) ?? throw new NotFoundException("Lượt ra vào kho", publicId);
        var item = await db.Set<Item>().FirstOrDefaultAsync(x => x.Id == tx.ItemId, ct);
        db.Set<StoreOut>().Remove(tx);
        if (tx.IsOpen && item is { Status: ItemStatus.OutOfStore }
            && !await db.Set<StoreOut>().AnyAsync(x => x.ItemId == item.Id && x.InAt == null && x.Id != tx.Id, ct))
        {
            item.BringBack();
            await events.PublishAsync(item, deleted: false, ct);
        }
        if (item is not null) await AuditAsync(item, $"ĐKCB {item.Barcode}: xoá lượt ra vào kho ({reason})", ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Dòng lịch sử theo bộ lọc; <paramref name="id"/> = đúng một lượt (lọc trước projection — EF không lọc được sau record).</summary>
    private IQueryable<StoreOutRow> Rows(StoreOutSearch s, long? id = null)
    {
        HoldingsExcel.EnsureRange(s.OutFrom, s.OutTo);
        HoldingsExcel.EnsureRange(s.InFrom, s.InTo);
        var txs = db.Set<StoreOut>().AsNoTracking();
        if (id is { } only) txs = txs.Where(x => x.Id == only);
        if (s.Open is { } open) txs = open ? txs.Where(x => x.InAt == null) : txs.Where(x => x.InAt != null);
        if (s.ReasonId is { } reason) txs = txs.Where(x => x.ReasonId == reason);
        if (s.UnitId is { } unit) txs = txs.Where(x => x.UnitId == unit);
        if (s.ExhibitionLocationId is { } location) txs = txs.Where(x => x.ExhibitionLocationId == location);
        if (s.ReturnAtLibrary is { } atLibrary) txs = txs.Where(x => x.ReturnAtLibrary == atLibrary);
        if (s.OutFrom is { } of) { var start = VietnamDate.Start(of); txs = txs.Where(x => x.OutAt >= start); }
        if (s.OutTo is { } ot) { var end = VietnamDate.Start(ot.AddDays(1)); txs = txs.Where(x => x.OutAt < end); }
        if (s.InFrom is { } inf) { var start = VietnamDate.Start(inf); txs = txs.Where(x => x.InAt >= start); }
        if (s.InTo is { } it) { var end = VietnamDate.Start(it.AddDays(1)); txs = txs.Where(x => x.InAt < end); }
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToUpper()/ToLower() sang upper()/lower() của SQL
        if (!string.IsNullOrWhiteSpace(s.Barcode))
        {
            var key = Item.Key(s.Barcode);
            txs = txs.Where(x => x.Barcode.ToUpper().Contains(key));
        }
        if (s.Term is { } term)
            txs = txs.Where(x => (x.DelivererName != null && x.DelivererName.ToLower().Contains(term))
                || (x.ReceiverName != null && x.ReceiverName.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        var query =
            from x in txs
            join i in db.Set<Item>() on x.ItemId equals i.Id into ij
            from i in ij.DefaultIfEmpty()
            join b in db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
            from b in bj.DefaultIfEmpty()
            join st in db.Set<Store>() on x.StoreId equals (long?)st.Id into sj
            from st in sj.DefaultIfEmpty()
            join r in db.Set<ExportReason>() on x.ReasonId equals (long?)r.Id into rj
            from r in rj.DefaultIfEmpty()
            join u in db.Set<BookOutUnit>() on x.UnitId equals (long?)u.Id into uj
            from u in uj.DefaultIfEmpty()
            join l in db.Set<ExhibitionLocation>() on x.ExhibitionLocationId equals (long?)l.Id into lj
            from l in lj.DefaultIfEmpty()
            orderby x.Id descending
            select new StoreOutRow(
                x.Id, x.PublicId, x.Barcode, i == null ? null : i.PublicId, i == null ? null : i.BibId, b == null ? null : b.Title, x.StoreId,
                st == null ? null : st.Name, x.ReasonId, r == null ? null : r.Name, x.UnitId, u == null ? null : u.Name, x.ExhibitionLocationId,
                l == null ? null : l.Name, x.DelivererName, x.ReceiverName, x.ReturnAtLibrary, x.OutAt, x.InAt);
        return query;
    }

    private async Task EnsureExistsAsync<T>(long? id, string label, CancellationToken ct) where T : Entity
    {
        if (id is { } value && !await db.Set<T>().AnyAsync(x => x.Id == value, ct))
            throw new BusinessRuleException("REFERENCE_NOT_FOUND", $"{label} đã chọn không còn.");
    }

    private Task AuditAsync(Item item, string label, CancellationToken ct) => audit is null
        ? Task.CompletedTask
        : audit.RecordAsync(new CrudAuditEntry(nameof(Item), "Bản sách", item.PublicId, CrudChange.Updated, label), ct);
}
