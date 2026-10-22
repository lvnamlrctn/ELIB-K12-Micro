using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>
/// Tìm bản sách để xếp giá trực quan / xếp giá lại (ELIB-LRC: StoreMapShelving/Search). <see cref="Status"/>: I (chưa xếp giá, mặc định), R (đã xếp giá),
/// ALL (cả hai). Chỉ I và R xếp được; mất/thanh lý/xuất kho không hiện. <see cref="StoreId"/> lọc theo kho của bản sách.
/// </summary>
public sealed class MapShelvingSearch
{
    public string? Status { get; set; }
    public long? StoreId { get; set; }

    /// <summary>Số đơn nhận (số) hoặc một phần tên đơn nhận.</summary>
    public string? Receipt { get; set; }

    public string? BarcodeFrom { get; set; }
    public string? BarcodeTo { get; set; }
    public string? Keyword { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Một bản sách trong danh sách xếp giá: vị trí hiện tại và vị trí gợi ý theo DDC (khoảng DDC của ngăn chứa DDC bản sách, trong các giá của kho).</summary>
public sealed record MapShelvingRow(
    Guid PublicId, string Barcode, long Mfn, string? Title, string? Ddc, string Status, bool OnLoan, long? StoreId, string? StoreName, long? ReceiptCode,
    Guid? ShelfPublicId, string? ShelfName, bool ShelfDeleted, int? RowIndex, string? RowRange,
    Guid? SuggestedShelfPublicId, string? SuggestedShelfName, Guid? SuggestedRowPublicId, int? SuggestedRowIndex);

public sealed record ShelfRowNode(Guid RowPublicId, int RowIndex, string DdcStart, string DdcEnd, string? Description, int Items);

public sealed record ShelfNode(
    Guid ShelfPublicId, string Code, string Name, double? PositionX, double? PositionY, double? Width, double? Height, string? ColorHex, int Items,
    IReadOnlyList<ShelfRowNode> Rows);

public sealed record FloorNode(Guid FloorPublicId, string Name, int? FloorNumber, string? BuildingName, double CanvasWidth, double CanvasHeight, string? LayoutImageUrl,
    IReadOnlyList<ShelfNode> Shelves);

/// <summary>Xếp các bản sách vào một giá (và ngăn). <see cref="Reason"/> tuỳ chọn, ghi vào lịch sử di chuyển.</summary>
public sealed record PlaceRequest(IReadOnlyList<Guid>? Ids, Guid ShelfPublicId, Guid? RowPublicId = null, string? Reason = null);

public sealed record PlaceResult(int Placed, IReadOnlyList<SkippedItem> Skipped);

/// <summary>
/// Xếp giá trực quan và xếp giá lại (monolith: StoreMapShelvingController, quyền MAP_SHELVING): chọn bản sách (chưa xếp giá, hoặc đã xếp giá để xếp lại), chọn giá
/// trên sơ đồ tầng của kho và ngăn theo DDC. Bản chưa xếp giá sang sẵn sàng phục vụ; bản đã xếp giá chỉ đổi vị trí. Mỗi lần xếp ghi lịch sử di chuyển.
/// </summary>
public sealed class MapShelvingResource(ICrudDbContext db, ItemEvents events, ItemMovements movements, TimeProvider clock, ICrudAuditSink? audit = null)
{
    public const int MaxPlace = 2000;
    public const int MaxPageSize = 200;

    private static string[] StatusFilter(string? status) => (status ?? "I").Trim().ToUpperInvariant() switch
    {
        "ALL" => [ItemStatus.Unshelved, ItemStatus.Available],
        "R" => [ItemStatus.Available],
        "I" => [ItemStatus.Unshelved],
        _ => throw new BusinessRuleException("STATUS_INVALID", "Trạng thái lọc phải là I (chưa xếp giá), R (đã xếp giá) hoặc ALL."),
    };

    public async Task<CrudPage<MapShelvingRow>> SearchAsync(MapShelvingSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var statuses = StatusFilter(search.Status);
        var page = Math.Max(1, search.PageIndex);
        var size = Math.Clamp(search.PageSize, 1, MaxPageSize);

        var query = db.Set<Item>().AsNoTracking().Where(i => statuses.Contains(i.Status));
        if (search.StoreId is { } storeId) query = query.Where(i => i.StoreId == storeId);
        if (!string.IsNullOrWhiteSpace(search.BarcodeFrom))
        {
            var from = Item.Key(search.BarcodeFrom);
            query = query.Where(i => i.BarcodeKey.CompareTo(from) >= 0);
        }
        if (!string.IsNullOrWhiteSpace(search.BarcodeTo))
        {
            var to = Item.Key(search.BarcodeTo);
            query = query.Where(i => i.BarcodeKey.CompareTo(to) <= 0);
        }
        if (!string.IsNullOrWhiteSpace(search.Receipt))
        {
            var text = search.Receipt.Trim().ToLowerInvariant();
            var asCode = long.TryParse(text, out var parsed) ? parsed : -1;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL — không phân biệt hoa/thường trên Postgres
            var receipts = db.Set<ReceiptSnapshot>().Where(r => (r.Name != null && r.Name.ToLower().Contains(text)) || r.Code == asCode).Select(r => r.ReceiptPublicId);
#pragma warning restore CA1862, CA1304, CA1311
            query = query.Where(i => i.ReceiptPublicId != null && receipts.Contains(i.ReceiptPublicId.Value));
        }
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var term = search.Keyword.Trim().ToLowerInvariant();
            var key = Item.Key(term);
#pragma warning disable CA1862, CA1304, CA1311
            var bibs = db.Set<BibSnapshot>().Where(b => b.Title.ToLower().Contains(term)).Select(b => b.Mfn);
#pragma warning restore CA1862, CA1304, CA1311
            query = query.Where(i => i.BarcodeKey.StartsWith(key) || bibs.Contains(i.BibId));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(i => i.BarcodeKey).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CrudPage<MapShelvingRow>(await ToRowsAsync(items, ct), total, page, size);
    }

    private async Task<IReadOnlyList<MapShelvingRow>> ToRowsAsync(List<Item> items, CancellationToken ct)
    {
        var mfns = items.Select(i => i.BibId).Distinct().ToList();
        var bibs = await db.Set<BibSnapshot>().AsNoTracking().Where(b => mfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, ct);
        var storeIds = items.Where(i => i.StoreId != null).Select(i => i.StoreId!.Value).Distinct().ToList();
        var stores = await db.Set<Store>().AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var receiptIds = items.Where(i => i.ReceiptPublicId != null).Select(i => i.ReceiptPublicId!.Value).Distinct().ToList();
        var receiptCodes = await db.Set<ReceiptSnapshot>().AsNoTracking().Where(r => receiptIds.Contains(r.ReceiptPublicId)).ToDictionaryAsync(r => r.ReceiptPublicId, r => r.Code, ct);

        // Giá/ngăn: mọi giá của các kho có mặt (gợi ý) + giá/ngăn hiện tại của từng bản (hiển thị, kể cả giá đã xoá).
        var storePublicIds = stores.Values.Select(s => s.PublicId).ToList();
        var currentShelfIds = items.Where(i => i.ShelfPublicId != null).Select(i => i.ShelfPublicId!.Value).Distinct().ToList();
        var shelves = await db.Set<ShelfReplica>().AsNoTracking()
            .Where(s => (!s.Deleted && s.StorePublicId != null && storePublicIds.Contains(s.StorePublicId.Value)) || currentShelfIds.Contains(s.ShelfPublicId))
            .ToListAsync(ct);
        var shelfIds = shelves.Select(s => s.ShelfPublicId).ToList();
        var rows = await db.Set<ShelfRowReplica>().AsNoTracking().Where(r => shelfIds.Contains(r.ShelfPublicId)).ToListAsync(ct);
        var shelfById = shelves.ToDictionary(s => s.ShelfPublicId);
        var rowById = rows.ToDictionary(r => r.RowPublicId);
        // Thứ tự gợi ý như LRC: giá có Id nhỏ trước, rồi số ngăn.
        var candidates = rows.Join(shelves.Where(s => !s.Deleted), r => r.ShelfPublicId, s => s.ShelfPublicId, (r, s) => (Row: r, Shelf: s))
            .OrderBy(x => x.Shelf.Id).ThenBy(x => x.Row.RowIndex).ToList();

        return items.Select(i =>
        {
            bibs.TryGetValue(i.BibId, out var bib);
            var store = i.StoreId is { } sid ? stores.GetValueOrDefault(sid) : null;
            shelfById.TryGetValue(i.ShelfPublicId ?? Guid.Empty, out var shelf);
            rowById.TryGetValue(i.ShelfRowPublicId ?? Guid.Empty, out var row);
            var suggestion = store is null || bib?.Ddc is null ? default
                : candidates.FirstOrDefault(c => c.Shelf.StorePublicId == store.PublicId && ShelfRowReplica.Contains(bib.Ddc, c.Row.DdcStart, c.Row.DdcEnd));
            return new MapShelvingRow(i.PublicId, i.Barcode, i.BibId, bib?.Title, bib?.Ddc, i.Status, i.OnLoan, i.StoreId, store?.Name,
                i.ReceiptPublicId is { } rid && receiptCodes.TryGetValue(rid, out var code) ? code : null,
                i.ShelfPublicId, shelf?.Name, shelf?.Deleted ?? (i.ShelfPublicId != null), row?.RowIndex, row is null ? null : $"{row.DdcStart} – {row.DdcEnd}",
                suggestion.Shelf?.ShelfPublicId, suggestion.Shelf?.Name, suggestion.Row?.RowPublicId, suggestion.Row?.RowIndex);
        }).ToList();
    }

    /// <summary>Sơ đồ giá của một kho: tầng → giá → ngăn, kèm số bản đang xếp ở từng giá/ngăn.</summary>
    public async Task<IReadOnlyList<FloorNode>> ShelvesAsync(long storeId, CancellationToken ct)
    {
        var store = await db.Set<Store>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId, ct) ?? throw new NotFoundException("Kho", storeId);
        var shelves = await db.Set<ShelfReplica>().AsNoTracking().Where(s => !s.Deleted && s.StorePublicId == store.PublicId).OrderBy(s => s.Id).ToListAsync(ct);
        if (shelves.Count == 0) return [];
        var shelfIds = shelves.Select(s => s.ShelfPublicId).ToList();
        var floorIds = shelves.Select(s => s.FloorPublicId).Distinct().ToList();
        var floors = await db.Set<FloorReplica>().AsNoTracking().Where(f => !f.Deleted && floorIds.Contains(f.FloorPublicId)).ToListAsync(ct);
        var rows = await db.Set<ShelfRowReplica>().AsNoTracking().Where(r => shelfIds.Contains(r.ShelfPublicId)).OrderBy(r => r.RowIndex).ToListAsync(ct);
        // Chỉ bản đang sẵn sàng trên giá — mất/thanh lý/xuất kho vẫn giữ mã giá nhưng không chiếm chỗ.
        var onShelf = await db.Set<Item>().AsNoTracking().Where(i => i.Status == ItemStatus.Available && i.ShelfPublicId != null && shelfIds.Contains(i.ShelfPublicId.Value))
            .Select(i => new { Shelf = i.ShelfPublicId!.Value, Row = i.ShelfRowPublicId }).ToListAsync(ct);
        int Count(Guid shelf) => onShelf.Count(x => x.Shelf == shelf);
        int RowCount(Guid row) => onShelf.Count(x => x.Row == row);

        ShelfNode ToNode(ShelfReplica s) => new(
            s.ShelfPublicId, s.Code, s.Name, s.PositionX, s.PositionY, s.Width, s.Height, s.ColorHex, Count(s.ShelfPublicId),
            rows.Where(r => r.ShelfPublicId == s.ShelfPublicId)
                .Select(r => new ShelfRowNode(r.RowPublicId, r.RowIndex, r.DdcStart, r.DdcEnd, r.Description, RowCount(r.RowPublicId))).ToList());

        var nodes = floors.OrderBy(f => f.BuildingName).ThenBy(f => f.FloorNumber).ThenBy(f => f.Name).Select(f => new FloorNode(
            f.FloorPublicId, f.Name, f.FloorNumber, f.BuildingName, f.CanvasWidth, f.CanvasHeight, f.LayoutImageUrl,
            shelves.Where(s => s.FloorPublicId == f.FloorPublicId).Select(ToNode).ToList())).ToList();

        // Giá có tầng chưa được đồng bộ (chưa nhận FloorChanged): vẫn hiện, ở một "tầng" tạm — không để giá biến mất khỏi sơ đồ.
        var known = floors.Select(f => f.FloorPublicId).ToHashSet();
        foreach (var group in shelves.Where(s => !known.Contains(s.FloorPublicId)).GroupBy(s => s.FloorPublicId))
            nodes.Add(new FloorNode(group.Key, "Tầng chưa đồng bộ", null, null, 1000, 700, null, group.Select(ToNode).ToList()));
        return nodes;
    }

    public async Task<PlaceResult> PlaceAsync(PlaceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ids = (request.Ids ?? []).Distinct().ToList();
        if (ids.Count is 0 or > MaxPlace) throw new BusinessRuleException("PLACE_EMPTY", $"Chọn từ 1 đến {MaxPlace} bản sách cần xếp.");
        var shelf = await db.Set<ShelfReplica>().AsNoTracking().FirstOrDefaultAsync(s => s.ShelfPublicId == request.ShelfPublicId && !s.Deleted, ct)
            ?? throw new BusinessRuleException("SHELF_NOT_FOUND", "Giá sách đã chọn không còn (hoặc chưa đồng bộ từ sơ đồ tầng).");
        ShelfRowReplica? row = null;
        if (request.RowPublicId is { } rowId)
            row = await db.Set<ShelfRowReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.RowPublicId == rowId && r.ShelfPublicId == shelf.ShelfPublicId, ct)
                ?? throw new BusinessRuleException("ROW_NOT_IN_SHELF", "Ngăn đã chọn không thuộc giá này.");
        var shelfStoreId = shelf.StorePublicId is { } sp
            ? (await db.Set<Store>().AsNoTracking().Where(s => s.PublicId == sp).Select(s => (long?)s.Id).FirstOrDefaultAsync(ct))
            : null;

        var skipped = new List<SkippedItem>();
        var placed = 0;
        var now = clock.GetUtcNow();
        foreach (var item in await db.Set<Item>().Where(i => ids.Contains(i.PublicId)).OrderBy(i => i.BarcodeKey).ToListAsync(ct))
        {
            if (shelf.StorePublicId is not null && item.StoreId != shelfStoreId)
            {
                skipped.Add(new SkippedItem(item.Barcode, "Giá thuộc kho khác với kho của bản sách."));
                continue;
            }
            if (item.Status == ItemStatus.Available && item.ShelfPublicId == shelf.ShelfPublicId && item.ShelfRowPublicId == row?.RowPublicId)
            {
                skipped.Add(new SkippedItem(item.Barcode, "Đã ở đúng vị trí này."));
                continue;
            }
            var (from, fromStatus) = (item.Location, item.Status);
            try
            {
                item.Place(shelf.ShelfPublicId, row?.RowPublicId, now);
            }
            catch (BusinessRuleException ex)
            {
                skipped.Add(new SkippedItem(item.Barcode, ex.Message));
                continue;
            }
            movements.Record(item, fromStatus == ItemStatus.Unshelved ? MovementKind.Place : MovementKind.Reshelve, from, fromStatus, request.Reason);
            await events.PublishAsync(item, deleted: false, ct);
            placed++;
        }
        if (placed > 0 && audit is not null)
            await audit.RecordAsync(new CrudAuditEntry(nameof(Item), "Bản sách", Guid.Empty, CrudChange.Updated,
                $"Xếp {placed} bản sách vào giá {shelf.Name}" + (row is null ? "" : $", ngăn {row.RowIndex}") + (string.IsNullOrWhiteSpace(request.Reason) ? "" : $" — {request.Reason.Trim()}")), ct);
        await db.SaveChangesAsync(ct);
        return new PlaceResult(placed, skipped);
    }
}
