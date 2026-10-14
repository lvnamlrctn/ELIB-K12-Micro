using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

public sealed record InventoryRequest(string Name, DateOnly? StartedOn = null, long? StoreId = null, string? Note = null);

public sealed record InventoryDto(
    long Id, Guid PublicId, string Name, DateOnly StartedOn, long? StoreId, string? StoreName, string? Note, DateTimeOffset? ClosedAt,
    int ScanCount, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Quét một mã trong đợt. <see cref="StoreId"/>: kho đang kiểm (trống = kho của đợt, đợt không chọn kho thì không đối chiếu).</summary>
public sealed record ScanRequest(Guid InventoryId, string? Barcode, long? StoreId = null);

public sealed record InventoryScanRow(
    long Id, Guid PublicId, string Barcode, Guid? ItemPublicId, long? Mfn, string? Title, long? StoreId, string? StoreName, long? ScanStoreId,
    bool Registered, bool WrongStore, bool OnLoan, bool Abnormal, string? ItemStatus, DateTimeOffset CreatedAt);

public sealed record ScanResult(InventoryScanRow Scan, bool AlreadyScanned);

/// <summary>Danh sách mã đã quét. Filter: wrong-store | on-loan | unregistered | abnormal (trống = tất cả).</summary>
public sealed class ScanSearch : CrudSearch
{
    public Guid InventoryId { get; set; }
    public string? Filter { get; set; }
}

/// <summary>Tổng hợp đợt (monolith: Summary). <see cref="Missing"/>: bản thiếu (nghi mất), xem <see cref="InventoryResource"/>.</summary>
public sealed record InventorySummary(int Scanned, int WrongStore, int Unregistered, int OnLoan, int Abnormal, int Missing);

public sealed record MissingRow(Guid ItemPublicId, string Barcode, long Mfn, string? Title, string? Author, long? StoreId, string? StoreName, string Status);

public sealed record InventoryImportResult(int Scanned, int AlreadyScanned, int Unregistered, IReadOnlyList<CrudImportError> Errors);

/// <summary>
/// Kiểm kê kho (monolith: StoreInventoryController + InventoryImportService, quyền INVENTORY; bảng KiemKe cũ gộp vào đây).
/// Quét tay hoặc nhập Excel danh sách mã; mã không có trong CSDL vẫn ghi nhận "chưa đăng ký". Đối chiếu sai kho, còn lượt mượn mở,
/// trạng thái khác thường (Mất/Thanh lý/Xuất kho mà vẫn trên giá). "Nghi mất": bản thuộc kho được kiểm chưa quét — trừ bản đang mượn,
/// đã mất, đã thanh lý, đang xuất kho; đợt không chọn kho thì như monolith, chỉ tính các kho có bản đã quét (đợt chưa quét gì → rỗng).
/// </summary>
public sealed class InventoryResource(ICrudDbContext db, HoldingsExcel excel, TimeProvider clock)
    : CrudResource<InventoryResource, Inventory, CrudSearch, InventoryRequest, InventoryDto>(db)
{
    public const string WrongStore = "wrong-store";
    public const string OnLoan = "on-loan";
    public const string Unregistered = "unregistered";
    public const string Abnormal = "abnormal";

    protected override string EntityName => "Đợt kiểm kê";

    protected override string? Describe(Inventory entity) => entity.Name;

    protected override Expression<Func<Inventory, InventoryDto>> Projection => x => new InventoryDto(
        x.Id, x.PublicId, x.Name, x.StartedOn, x.StoreId, Db.Set<Store>().Where(s => s.Id == x.StoreId).Select(s => s.Name).FirstOrDefault(), x.Note,
        x.ClosedAt, Db.Set<InventoryScan>().Count(s => s.InventoryId == x.Id), x.CreatedAt, x.UpdatedAt);

    protected override Inventory Create(InventoryRequest request) =>
        Inventory.Create(request.Name, request.StartedOn ?? VietnamDate.Of(clock.GetUtcNow()), request.StoreId, request.Note);

    protected override async Task UpdateAsync(Inventory entity, InventoryRequest request, CancellationToken ct) =>
        entity.Update(request.Name, request.StartedOn ?? entity.StartedOn, request.StoreId, request.Note,
            hasScans: await Db.Set<InventoryScan>().AnyAsync(s => s.InventoryId == entity.Id, ct));

    protected override void Update(Inventory entity, InventoryRequest request) => throw new NotSupportedException("Sửa qua UpdateAsync.");

    protected override IQueryable<Inventory> Filter(IQueryable<Inventory> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Inventory> Order(IQueryable<Inventory> query) => query.OrderByDescending(x => x.StartedOn).ThenByDescending(x => x.Id);

    protected override async Task ValidateAsync(Inventory entity, CancellationToken ct)
    {
        if (entity.StoreId is { } id && !await Db.Set<Store>().AnyAsync(s => s.Id == id, ct))
            throw new BusinessRuleException("STORE_NOT_FOUND", "Kho đã chọn không còn.");
    }

    public async Task<InventoryDto> CloseAsync(Guid publicId, CancellationToken ct)
    {
        var inventory = await LoadAsync(publicId, ct);
        inventory.Close(clock.GetUtcNow());
        await AuditAsync(inventory, CrudChange.StatusChanged, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(inventory);
    }

    public async Task<InventoryDto> ReopenAsync(Guid publicId, CancellationToken ct)
    {
        var inventory = await LoadAsync(publicId, ct);
        inventory.Reopen();
        await AuditAsync(inventory, CrudChange.StatusChanged, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(inventory);
    }

    /// <summary>Quét một mã (monolith: ScanBarcode). Mã đã quét trong đợt → trả lại dòng cũ, không ghi thêm.</summary>
    public async Task<ScanResult> ScanAsync(ScanRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inventory = await LoadAsync(request.InventoryId, ct);
        var barcode = Item.NormalizeBarcode(request.Barcode);
        var (added, already, _) = await ScanManyAsync(inventory, [(barcode, request.StoreId)], ct);
        await Db.SaveChangesAsync(ct);
        var id = added.Count > 0 ? added[0].Id : already[0];
        return new ScanResult(await ScanRows(Db.Set<InventoryScan>().Where(s => s.Id == id)).FirstAsync(ct), added.Count == 0);
    }

    public static byte[] ScanTemplate() => CrudExcel.Template("Kiem ke", ImportColumns);

    private static readonly IReadOnlyList<CrudImportColumn> ImportColumns =
    [
        new("barcode", "Số ĐKCB", true, "Mỗi dòng một mã đã quét.", "DKCB", "Barcode", "Mã vạch", "Ma vach"),
        new("store", "Mã kho", false, "Kho đang kiểm (trống = kho của đợt).", "Kho", "Store"),
    ];

    /// <summary>
    /// Nhập danh sách mã đã quét từ Excel (monolith: Import, đường đồng bộ). Cùng logic quét tay; mã kho sai → lỗi dòng, cả file không
    /// ghi. Mã đã quét trước đó được bỏ qua.
    /// </summary>
    public async Task<InventoryImportResult> ImportAsync(Guid inventoryId, Stream file, CancellationToken ct)
    {
        var inventory = await LoadAsync(inventoryId, ct);
        inventory.EnsureOpen();
        var rows = CrudExcel.Read(file, ImportColumns);
        var stores = await Db.Set<Store>().AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Id, StringComparer.OrdinalIgnoreCase, ct);
        var errors = new List<CrudImportError>();
        var scans = new List<(string, long?)>();
        foreach (var row in rows)
        {
            try
            {
                var barcode = Item.NormalizeBarcode(row.Required("barcode", "Số ĐKCB"));
                long? store = row.Get("store") is { } code
                    ? stores.TryGetValue(code.Trim(), out var id) ? id : throw new BusinessRuleException("STORE_NOT_FOUND", $"Không có kho mã '{code}'.")
                    : null;
                scans.Add((barcode, store));
            }
            catch (BusinessRuleException ex)
            {
                if (errors.Count < MaxImportErrors)
                    errors.Add(new CrudImportError(row.Number, ex.Message));
            }
        }
        if (errors.Count > 0) return new InventoryImportResult(0, 0, 0, errors);
        var (added, already, unregistered) = await ScanManyAsync(inventory, scans, ct);
        await Db.SaveChangesAsync(ct);
        return new InventoryImportResult(added.Count, already.Count, unregistered, []);
    }

    public async Task<CrudPage<InventoryScanRow>> ScansAsync(ScanSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var inventory = await LoadAsync(search.InventoryId, ct);
        var scans = Db.Set<InventoryScan>().AsNoTracking().Where(s => s.InventoryId == inventory.Id);
        scans = search.Filter?.Trim().ToLowerInvariant() switch
        {
            null or "" => scans,
            WrongStore => scans.Where(s => s.WrongStore),
            OnLoan => scans.Where(s => s.OnLoan),
            Unregistered => scans.Where(s => !s.Registered),
            Abnormal => scans.Where(s => s.ItemStatus == ItemStatus.Lost || s.ItemStatus == ItemStatus.Liquidated || s.ItemStatus == ItemStatus.OutOfStore),
            _ => throw new BusinessRuleException("FILTER_INVALID", $"Bộ lọc '{search.Filter}' không hợp lệ."),
        };
        if (search.Term is { } term)
        {
            var key = Item.Key(term);
            scans = scans.Where(s => s.BarcodeKey.StartsWith(key));
        }
        var (page, size) = (Math.Max(1, search.PageIndex), Math.Clamp(search.PageSize, 1, 500));
        var total = await scans.CountAsync(ct);
        return new CrudPage<InventoryScanRow>(await ScanRows(scans).Skip((page - 1) * size).Take(size).ToListAsync(ct), total, page, size);
    }

    public async Task<InventorySummary> SummaryAsync(Guid publicId, CancellationToken ct)
    {
        var inventory = await LoadAsync(publicId, ct);
        var scans = Db.Set<InventoryScan>().Where(s => s.InventoryId == inventory.Id);
        return new InventorySummary(
            await scans.CountAsync(ct),
            await scans.CountAsync(s => s.WrongStore, ct),
            await scans.CountAsync(s => !s.Registered, ct),
            await scans.CountAsync(s => s.OnLoan, ct),
            await scans.CountAsync(s => s.ItemStatus == ItemStatus.Lost || s.ItemStatus == ItemStatus.Liquidated || s.ItemStatus == ItemStatus.OutOfStore, ct),
            await Missing(inventory).CountAsync(ct));
    }

    /// <summary>Bản thiếu (nghi mất) theo kho, ĐKCB — để in danh sách hoặc báo mất hàng loạt (lost-books/MarkLost).</summary>
    public async Task<CrudPage<MissingRow>> MissingAsync(Guid publicId, int pageIndex, int pageSize, CancellationToken ct)
    {
        var inventory = await LoadAsync(publicId, ct);
        var query = MissingRows(inventory);
        var (page, size) = (Math.Max(1, pageIndex), Math.Clamp(pageSize, 1, ItemBarcodes.MaxBatch));
        return new CrudPage<MissingRow>(await query.Skip((page - 1) * size).Take(size).ToListAsync(ct), await Missing(inventory).CountAsync(ct), page, size);
    }

    /// <summary>Báo cáo kiểm kê (monolith: Report) — 3 sheet: tổng quan, chi tiết đã quét, chưa quét (nghi mất).</summary>
    public async Task<ExcelFile> ReportAsync(Guid publicId, CancellationToken ct)
    {
        var inventory = await LoadAsync(publicId, ct);
        var summary = await SummaryAsync(publicId, ct);
        var scanned = await ScanRows(Db.Set<InventoryScan>().AsNoTracking().Where(s => s.InventoryId == inventory.Id)).Take(HoldingsExcel.MaxRows).ToListAsync(ct);
        var missing = await MissingRows(inventory).Take(HoldingsExcel.MaxRows).ToListAsync(ct);
        var subtitle = $"Đợt: {inventory.Name}";
        return await excel.BuildAsync($"kiem-ke-{inventory.Id}", ct,
            new ExcelSheet("Tổng quan", "BÁO CÁO KIỂM KÊ - TỔNG QUAN", subtitle, ["Chỉ số", "Số lượng"],
            [
                ["Đã quét", summary.Scanned],
                ["Sai vị trí kho", summary.WrongStore],
                ["Chưa đăng ký (mã không có trong CSDL)", summary.Unregistered],
                ["Có trên giá nhưng còn lượt mượn mở", summary.OnLoan],
                ["Trạng thái khác thường (mất / thanh lý / xuất kho)", summary.Abnormal],
                ["Chưa quét (nghi mất)", summary.Missing],
            ]),
            new ExcelSheet("Chi tiết đã quét", "BÁO CÁO KIỂM KÊ - CHI TIẾT ĐÃ QUÉT", subtitle,
                ["Số ĐKCB", "Nhan đề", "Kho", "Vị trí kho", "Tình trạng mượn", "Trạng thái bản sách"],
                scanned.Select(x => (IReadOnlyList<object?>)[x.Barcode, x.Registered ? x.Title : "(chưa đăng ký)", x.StoreName,
                    x.WrongStore ? "Sai kho" : "Đúng kho", x.OnLoan ? "Còn lượt mượn mở" : "Không mượn",
                    x.ItemStatus is null ? null : ItemStatus.Names.GetValueOrDefault(x.ItemStatus, x.ItemStatus)])),
            new ExcelSheet("Chưa quét (nghi mất)", "BÁO CÁO KIỂM KÊ - CHƯA QUÉT (NGHI MẤT)", subtitle, ["Số ĐKCB", "Nhan đề", "Tác giả", "Kho"],
                missing.Select(x => (IReadOnlyList<object?>)[x.Barcode, x.Title, x.Author, x.StoreName])));
    }

    /// <summary>Ghi các mã chưa quét trong đợt (chưa lưu). Trả dòng mới, id dòng đã có, số mã chưa đăng ký mới ghi.</summary>
    private async Task<(List<InventoryScan> Added, List<long> Already, int Unregistered)> ScanManyAsync(
        Inventory inventory, IReadOnlyList<(string Barcode, long? StoreId)> codes, CancellationToken ct)
    {
        inventory.EnsureOpen();
        var keys = codes.Select(c => Item.Key(c.Barcode)).Distinct(StringComparer.Ordinal).ToList();
        var items = new Dictionary<string, Item>(StringComparer.Ordinal);
        var existing = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var chunk in keys.Chunk(500))
        {
            foreach (var item in await Db.Set<Item>().AsNoTracking().Where(i => chunk.Contains(i.BarcodeKey)).ToListAsync(ct)) items[item.BarcodeKey] = item;
            foreach (var scan in await Db.Set<InventoryScan>().AsNoTracking().Where(s => s.InventoryId == inventory.Id && chunk.Contains(s.BarcodeKey))
                         .Select(s => new { s.BarcodeKey, s.Id }).ToListAsync(ct))
                existing[scan.BarcodeKey] = scan.Id;
        }
        if (codes.Select(c => c.StoreId).Where(s => s is not null).Distinct().ToList() is { Count: > 0 } chosen)
        {
            var ids = chosen.Select(s => s!.Value).ToList();
            if (await Db.Set<Store>().CountAsync(s => ids.Contains(s.Id), ct) != ids.Count)
                throw new BusinessRuleException("STORE_NOT_FOUND", "Kho đã chọn không còn.");
        }

        var added = new List<InventoryScan>();
        var already = new List<long>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (barcode, storeId) in codes)
        {
            var key = Item.Key(barcode);
            if (existing.TryGetValue(key, out var id)) { already.Add(id); continue; }
            if (!seen.Add(key)) continue; // trùng trong cùng file
            var scan = InventoryScan.Of(inventory.Id, barcode, items.GetValueOrDefault(key), storeId ?? inventory.StoreId);
            Db.Set<InventoryScan>().Add(scan);
            added.Add(scan);
        }
        return (added, already, added.Count(s => !s.Registered));
    }

    private IQueryable<Item> Missing(Inventory inventory)
    {
        var scans = Db.Set<InventoryScan>().Where(s => s.InventoryId == inventory.Id);
        var items = Db.Set<Item>().Where(i => !scans.Any(s => s.ItemId == i.Id) && !i.OnLoan
            && i.Status != ItemStatus.Lost && i.Status != ItemStatus.Liquidated && i.Status != ItemStatus.OutOfStore);
        if (inventory.StoreId is { } store) return items.Where(i => i.StoreId == store);
        var stores = scans.Where(s => s.StoreId != null).Select(s => s.StoreId);
        return items.Where(i => i.StoreId != null && stores.Contains(i.StoreId));
    }

    private IQueryable<MissingRow> MissingRows(Inventory inventory) =>
        from i in Missing(inventory).AsNoTracking()
        join b in Db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
        from b in bj.DefaultIfEmpty()
        join st in Db.Set<Store>() on i.StoreId equals (long?)st.Id into sj
        from st in sj.DefaultIfEmpty()
        orderby st.Code, i.BarcodeKey
        select new MissingRow(i.PublicId, i.Barcode, i.BibId, b == null ? null : b.Title, b == null ? null : b.Author, i.StoreId,
            st == null ? null : st.Name, i.Status);

    private IQueryable<InventoryScanRow> ScanRows(IQueryable<InventoryScan> scans) =>
        from s in scans
        join i in Db.Set<Item>() on s.ItemId equals (long?)i.Id into ij
        from i in ij.DefaultIfEmpty()
        join b in Db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
        from b in bj.DefaultIfEmpty()
        join st in Db.Set<Store>() on s.StoreId equals (long?)st.Id into sj
        from st in sj.DefaultIfEmpty()
        orderby s.Id descending
        select new InventoryScanRow(
            s.Id, s.PublicId, s.Barcode, i == null ? null : i.PublicId, i == null ? null : i.BibId, b == null ? null : b.Title, s.StoreId,
            st == null ? null : st.Name, s.ScanStoreId, s.Registered, s.WrongStore, s.OnLoan,
            s.ItemStatus == ItemStatus.Lost || s.ItemStatus == ItemStatus.Liquidated || s.ItemStatus == ItemStatus.OutOfStore, s.ItemStatus,
            s.CreatedAt);
}
