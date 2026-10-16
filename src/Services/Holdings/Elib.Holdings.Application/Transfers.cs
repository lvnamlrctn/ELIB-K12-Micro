using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Dòng bản sách của phiếu điều chuyển / giao nhận, kèm trạng thái hiện tại.</summary>
public sealed record TransferLine(
    Guid PublicId, Guid ItemPublicId, string Barcode, long Mfn, string? Title, string? Author, long? StoreId, string? StoreName, string Status,
    bool OnLoan, string? SkipReason);

/// <summary>Thêm bản sách vào phiếu: chọn trong danh sách (id) và/hoặc quét/dán số ĐKCB.</summary>
public sealed record AddLinesRequest(IReadOnlyList<Guid>? ItemIds = null, IReadOnlyList<string>? Barcodes = null);

/// <summary>Chọn bản sách cho phiếu: bộ lọc tìm kiếm tài liệu (từ khoá, khoảng ĐKCB, MFN) trong phạm vi phiếu cho phép.</summary>
public sealed class TransferCandidateSearch : CrudSearch
{
    public Guid TransferId { get; set; }
    public long? Mfn { get; set; }
    public string? BarcodeFrom { get; set; }
    public string? BarcodeTo { get; set; }

    /// <summary>Chỉ bản đăng ký theo phiếu nhận này (phiếu giao nhận: sách của đơn nhận).</summary>
    public Guid? ReceiptPublicId { get; set; }

    public ItemSearch ToItemSearch() => new()
    {
        Keyword = Keyword, Mfn = Mfn, BarcodeFrom = BarcodeFrom, BarcodeTo = BarcodeTo, ReceiptPublicId = ReceiptPublicId, PageIndex = PageIndex,
        PageSize = PageSize,
    };
}

internal static class TransferLines
{
    public const int MaxLines = 2000;

    /// <summary>Bản sách theo id chọn + mã quét. Mã không có → NotFound; id không có bị bỏ qua (đã xoá).</summary>
    public static async Task<(List<Item> Items, List<string> NotFound)> LoadAsync(ICrudDbContext db, AddLinesRequest request, CancellationToken ct)
    {
        var ids = (request.ItemIds ?? []).Distinct().ToList();
        var codes = ItemBarcodes.Clean(request.Barcodes);
        if (ids.Count + codes.Count is 0 or > MaxLines)
            throw new BusinessRuleException("LINES_EMPTY", $"Chọn từ 1 đến {MaxLines} bản sách.");
        var keys = codes.Select(Item.Key).ToList();
        var items = await db.Set<Item>().AsNoTracking().Where(x => ids.Contains(x.PublicId) || keys.Contains(x.BarcodeKey)).ToListAsync(ct);
        var found = items.Select(i => i.BarcodeKey).ToHashSet(StringComparer.Ordinal);
        return (items.OrderBy(i => i.BarcodeKey, StringComparer.Ordinal).ToList(), codes.Where(c => !found.Contains(Item.Key(c))).ToList());
    }

}

public sealed record StoreMoveRequest(long FromStoreId, long ToStoreId, string? DelivererName = null, string? ReceiverName = null,
    DateOnly? MovedOn = null, string? Note = null);

public sealed class StoreMoveSearch : CrudSearch
{
    public long? StoreId { get; set; }
    public bool? Completed { get; set; }
}

public sealed record StoreMoveDto(
    long Id, Guid PublicId, long FromStoreId, string? FromStoreName, long ToStoreId, string? ToStoreName, string? DelivererName,
    string? ReceiverName, DateOnly? MovedOn, string? Note, DateTimeOffset? CompletedAt, int MovedCount, int LineCount,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record CompleteResult(int Done, IReadOnlyList<SkippedItem> Skipped);

/// <summary>
/// Phiếu điều chuyển kho (monolith: CatalogueMoveController + StoreMoveService, quyền AB_MOVES). Chỉ thêm được bản sách đang ở kho
/// nguồn; "Hoàn thành" chuyển các bản sang kho nhận (bỏ qua bản đang mượn, xuất kho, mất, thanh lý hoặc đã rời kho nguồn — ghi lý do
/// trên dòng), phát ItemChanged, rồi khoá phiếu.
/// </summary>
public sealed class StoreMoveResource(ICrudDbContext db, ItemResource items, ItemEvents events, TimeProvider clock)
    : CrudResource<StoreMoveResource, StoreMove, StoreMoveSearch, StoreMoveRequest, StoreMoveDto>(db)
{
    protected override string EntityName => "Phiếu điều chuyển kho";

    protected override string? Describe(StoreMove entity) => $"Phiếu số {entity.Id}";

    protected override Expression<Func<StoreMove, StoreMoveDto>> Projection => x => new StoreMoveDto(
        x.Id, x.PublicId, x.FromStoreId, Db.Set<Store>().Where(s => s.Id == x.FromStoreId).Select(s => s.Name).FirstOrDefault(),
        x.ToStoreId, Db.Set<Store>().Where(s => s.Id == x.ToStoreId).Select(s => s.Name).FirstOrDefault(), x.DelivererName, x.ReceiverName,
        x.MovedOn, x.Note, x.CompletedAt, x.MovedCount, Db.Set<StoreMoveLine>().Count(l => l.MoveId == x.Id), x.CreatedAt, x.UpdatedAt);

    protected override StoreMove Create(StoreMoveRequest request) =>
        StoreMove.Create(request.FromStoreId, request.ToStoreId, request.DelivererName, request.ReceiverName, request.MovedOn, request.Note);

    protected override async Task UpdateAsync(StoreMove entity, StoreMoveRequest request, CancellationToken ct) =>
        entity.Update(request.FromStoreId, request.ToStoreId, request.DelivererName, request.ReceiverName, request.MovedOn, request.Note,
            hasLines: await Db.Set<StoreMoveLine>().AnyAsync(l => l.MoveId == entity.Id, ct));

    protected override void Update(StoreMove entity, StoreMoveRequest request) => throw new NotSupportedException("Sửa qua UpdateAsync.");

    protected override IQueryable<StoreMove> Filter(IQueryable<StoreMove> query, StoreMoveSearch search)
    {
        if (search.StoreId is { } store) query = query.Where(x => x.FromStoreId == store || x.ToStoreId == store);
        if (search.Completed is { } done) query = done ? query.Where(x => x.CompletedAt != null) : query.Where(x => x.CompletedAt == null);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => (x.DelivererName != null && x.DelivererName.ToLower().Contains(term))
                || (x.ReceiverName != null && x.ReceiverName.ToLower().Contains(term)) || (x.Note != null && x.Note.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override async Task ValidateAsync(StoreMove entity, CancellationToken ct)
    {
        var ids = new[] { entity.FromStoreId, entity.ToStoreId };
        if (await Db.Set<Store>().CountAsync(s => ids.Contains(s.Id), ct) != 2)
            throw new BusinessRuleException("STORE_NOT_FOUND", "Kho đã chọn không còn.");
    }

    protected override async Task EnsureDeletableAsync(StoreMove entity, CancellationToken ct)
    {
        entity.EnsureEditable();
        foreach (var line in await Db.Set<StoreMoveLine>().Where(l => l.MoveId == entity.Id).ToListAsync(ct)) Db.Set<StoreMoveLine>().Remove(line);
    }

    public async Task<IReadOnlyList<TransferLine>> LinesAsync(Guid publicId, CancellationToken ct)
    {
        var move = await LoadAsync(publicId, ct);
        return await LineRows(Db, Db.Set<StoreMoveLine>().AsNoTracking().Where(l => l.MoveId == move.Id).Select(l => new LineRef { PublicId = l.PublicId, ItemId = l.ItemId, SkipReason = l.SkipReason }))
            .ToListAsync(ct);
    }

    /// <summary>Bản sách ở kho nguồn chưa có trong phiếu.</summary>
    public async Task<CrudPage<ItemDto>> CandidatesAsync(TransferCandidateSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var move = await LoadAsync(search.TransferId, ct);
        var taken = Db.Set<StoreMoveLine>().Where(l => l.MoveId == move.Id).Select(l => l.ItemId);
        var itemSearch = search.ToItemSearch();
        itemSearch.StoreId = move.FromStoreId;
        return await items.PageAsync(itemSearch, i => !taken.Contains(i.Id), ct);
    }

    public async Task<ItemBatchResult> AddLinesAsync(Guid publicId, AddLinesRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var move = await LoadAsync(publicId, ct);
        move.EnsureEditable();
        var (found, notFound) = await TransferLines.LoadAsync(Db, request, ct);
        var ids = found.Select(i => i.Id).ToList();
        var taken = (await Db.Set<StoreMoveLine>().Where(l => l.MoveId == move.Id && ids.Contains(l.ItemId)).Select(l => l.ItemId).ToListAsync(ct)).ToHashSet();
        var skipped = new List<SkippedItem>();
        var added = 0;
        foreach (var item in found)
        {
            if (taken.Contains(item.Id)) skipped.Add(new SkippedItem(item.Barcode, "Đã có trong phiếu."));
            else if (item.StoreId != move.FromStoreId) skipped.Add(new SkippedItem(item.Barcode, "Không thuộc kho nguồn của phiếu."));
            else
            {
                Db.Set<StoreMoveLine>().Add(StoreMoveLine.Of(move.Id, item.Id));
                added++;
            }
        }
        await Db.SaveChangesAsync(ct);
        return new ItemBatchResult(added, notFound, skipped);
    }

    public async Task RemoveLineAsync(Guid linePublicId, CancellationToken ct)
    {
        var line = await Db.Set<StoreMoveLine>().FirstOrDefaultAsync(l => l.PublicId == linePublicId, ct) ?? throw new NotFoundException("Dòng phiếu", linePublicId);
        var move = await Set.FirstOrDefaultAsync(m => m.Id == line.MoveId, ct) ?? throw new NotFoundException(EntityName, line.MoveId);
        move.EnsureEditable();
        Db.Set<StoreMoveLine>().Remove(line);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>Hoàn thành điều chuyển: chuyển các bản sang kho nhận (bản không chuyển được ghi lý do trên dòng), khoá phiếu.</summary>
    public async Task<CompleteResult> CompleteAsync(Guid publicId, CancellationToken ct)
    {
        var move = await LoadAsync(publicId, ct);
        move.EnsureEditable();
        var lines = await Db.Set<StoreMoveLine>().Where(l => l.MoveId == move.Id).ToListAsync(ct);
        if (lines.Count == 0) throw new BusinessRuleException("MOVE_EMPTY", "Phiếu chưa có tài liệu nào.");
        var ids = lines.Select(l => l.ItemId).ToList();
        var byId = (await Db.Set<Item>().Where(i => ids.Contains(i.Id)).ToListAsync(ct)).ToDictionary(i => i.Id);
        var skipped = new List<SkippedItem>();
        var moved = 0;
        foreach (var line in lines)
        {
            if (!byId.TryGetValue(line.ItemId, out var item))
            {
                line.Skip("Bản sách đã bị xoá.");
                skipped.Add(new SkippedItem("?", "Bản sách đã bị xoá."));
                continue;
            }
            try
            {
                if (item.StoreId != move.FromStoreId) throw new BusinessRuleException("ITEM_NOT_IN_STORE", $"ĐKCB {item.Barcode} không còn ở kho nguồn.");
                item.MoveTo(move.ToStoreId);
            }
            catch (BusinessRuleException ex)
            {
                line.Skip(ex.Message);
                skipped.Add(new SkippedItem(item.Barcode, ex.Message));
                continue;
            }
            await events.PublishAsync(item, deleted: false, ct);
            moved++;
        }
        move.Complete(clock.GetUtcNow(), moved);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(StoreMove), EntityName, move.PublicId, CrudChange.StatusChanged,
                $"Phiếu số {move.Id}: hoàn thành, chuyển {moved} bản" + (skipped.Count > 0 ? $", bỏ qua {skipped.Count}" : "")), ct);
        await Db.SaveChangesAsync(ct);
        return new CompleteResult(moved, skipped);
    }

    /// <summary>Dòng phiếu rút gọn — khởi tạo bằng thuộc tính (không constructor) để EF ghép tiếp được câu truy vấn.</summary>
    internal sealed class LineRef
    {
        public Guid PublicId { get; init; }
        public long ItemId { get; init; }
        public string? SkipReason { get; init; }
    }

    internal static IQueryable<TransferLine> LineRows(ICrudDbContext db, IQueryable<LineRef> lines) =>
        from l in lines
        join i in db.Set<Item>() on l.ItemId equals i.Id
        join b in db.Set<BibSnapshot>() on i.BibPublicId equals b.BibPublicId into bj
        from b in bj.DefaultIfEmpty()
        join st in db.Set<Store>() on i.StoreId equals (long?)st.Id into sj
        from st in sj.DefaultIfEmpty()
        orderby i.BarcodeKey
        select new TransferLine(l.PublicId, i.PublicId, i.Barcode, i.BibId, b == null ? null : b.Title, b == null ? null : b.Author, i.StoreId,
            st == null ? null : st.Name, i.Status, i.OnLoan, l.SkipReason);
}

public sealed record DeliveryRequest(
    string? DelivererName = null, string? DelivererAddress = null, string? ReceiverName = null, string? ReceiverAddress = null,
    DateOnly? DeliveredOn = null, DateOnly? ReceivedOn = null, long? StoreId = null, int Status = DeliveryStatus.Delivering, string? Note = null,
    Guid? ReceiptPublicId = null);

public sealed class DeliverySearch : CrudSearch
{
    public int? DeliveryStatus { get; set; }
    /// <summary>true = đã ký nhận, false = chưa.</summary>
    public bool? Confirmed { get; set; }
}

public sealed record DeliveryDto(
    long Id, Guid PublicId, string? DelivererName, string? DelivererAddress, string? ReceiverName, string? ReceiverAddress, DateOnly? DeliveredOn,
    DateOnly? ReceivedOn, long? StoreId, string? StoreName, int Status, string? Note, DateTimeOffset? SignedAt, int ShelvedCount, int LineCount,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, Guid? ReceiptPublicId = null, long? ReceiptCode = null);

/// <summary>Đơn nhận chọn được cho phiếu giao nhận: đã hoàn tất, còn <see cref="Pending"/> bản chưa xếp giá chưa phân bổ.</summary>
public sealed record DeliveryReceiptOption(Guid PublicId, long Code, string? Name, DateOnly? ReceivedOn, long? StoreId, int Pending);

/// <summary>
/// Phiếu giao nhận sách mới về kho (monolith: CatalogueDelivererController, quyền AB_DELIVERERS). Chọn bản "chưa xếp giá" chưa nằm ở
/// phiếu giao nhận nào; ký nhận (khi "Đã nhận đủ") → các bản còn "chưa xếp giá" sang sẵn sàng phục vụ ở kho nhận của phiếu, phiếu khoá.
/// Chọn đơn nhận (phiếu nhận đã hoàn tất của phân hệ bổ sung) thì màn chọn sách liệt kê bản của đơn đó; vẫn tìm được theo MFN/ĐKCB/nhan đề.
/// </summary>
public sealed class DeliveryResource(ICrudDbContext db, ItemResource items, ItemEvents events, TimeProvider clock)
    : CrudResource<DeliveryResource, Delivery, DeliverySearch, DeliveryRequest, DeliveryDto>(db)
{
    protected override string EntityName => "Phiếu giao nhận";

    protected override string? Describe(Delivery entity) => $"Phiếu số {entity.Id}";

    protected override Expression<Func<Delivery, DeliveryDto>> Projection => x => new DeliveryDto(
        x.Id, x.PublicId, x.DelivererName, x.DelivererAddress, x.ReceiverName, x.ReceiverAddress, x.DeliveredOn, x.ReceivedOn, x.StoreId,
        Db.Set<Store>().Where(s => s.Id == x.StoreId).Select(s => s.Name).FirstOrDefault(), x.Status, x.Note, x.SignedAt, x.ShelvedCount,
        Db.Set<DeliveryLine>().Count(l => l.DeliveryId == x.Id), x.CreatedAt, x.UpdatedAt, x.ReceiptPublicId,
        Db.Set<ReceiptSnapshot>().Where(r => r.ReceiptPublicId == x.ReceiptPublicId).Select(r => (long?)r.Code).FirstOrDefault());

    protected override Delivery Create(DeliveryRequest request) => Delivery.Create(request.DelivererName, request.DelivererAddress, request.ReceiverName,
        request.ReceiverAddress, request.DeliveredOn, request.ReceivedOn, request.StoreId, request.Status, request.Note, request.ReceiptPublicId);

    protected override void Update(Delivery entity, DeliveryRequest request) => entity.Update(request.DelivererName, request.DelivererAddress,
        request.ReceiverName, request.ReceiverAddress, request.DeliveredOn, request.ReceivedOn, request.StoreId, request.Status, request.Note,
        request.ReceiptPublicId);

    protected override IQueryable<Delivery> Filter(IQueryable<Delivery> query, DeliverySearch search)
    {
        if (search.DeliveryStatus is { } status) query = query.Where(x => x.Status == status);
        if (search.Confirmed is { } confirmed) query = confirmed ? query.Where(x => x.SignedAt != null) : query.Where(x => x.SignedAt == null);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => (x.DelivererName != null && x.DelivererName.ToLower().Contains(term))
                || (x.ReceiverName != null && x.ReceiverName.ToLower().Contains(term)) || (x.Note != null && x.Note.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override async Task ValidateAsync(Delivery entity, CancellationToken ct)
    {
        if (entity.StoreId is { } id && !await Db.Set<Store>().AnyAsync(s => s.Id == id, ct))
            throw new BusinessRuleException("STORE_NOT_FOUND", "Kho đã chọn không còn.");
        if (entity.ReceiptPublicId is { } receipt && !await Db.Set<ReceiptSnapshot>().AnyAsync(r => r.ReceiptPublicId == receipt && !r.Deleted, ct))
            throw new BusinessRuleException("RECEIPT_NOT_FOUND", "Đơn nhận đã chọn không còn (hoặc chưa đồng bộ từ phân hệ bổ sung).");
    }

    public const int MaxReceiptOptions = 500;

    /// <summary>
    /// Đơn nhận cho ô chọn của phiếu giao nhận (monolith: CatalogueReceipt/SearchAvailableForDeliverer): phiếu nhận đã hoàn tất còn bản
    /// "chưa xếp giá" chưa nằm ở phiếu giao nhận nào. <paramref name="include"/>: luôn có đơn đang chọn của phiếu (kể cả đã phân bổ hết).
    /// </summary>
    public async Task<IReadOnlyList<DeliveryReceiptOption>> ReceiptOptionsAsync(Guid? include, CancellationToken ct)
    {
        var taken = Db.Set<DeliveryLine>().Select(l => l.ItemId);
        var pending = await Db.Set<Item>().AsNoTracking()
            .Where(i => i.ReceiptPublicId != null && i.Status == ItemStatus.Unshelved && !taken.Contains(i.Id))
            .GroupBy(i => i.ReceiptPublicId!.Value).Select(g => new { Receipt = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Receipt, x => x.Count, ct);
        var ids = pending.Keys.ToList();
        var receipts = await Db.Set<ReceiptSnapshot>().AsNoTracking()
            .Where(r => !r.Deleted && ((r.Completed && ids.Contains(r.ReceiptPublicId)) || r.ReceiptPublicId == include))
            .OrderByDescending(r => r.Code).Take(MaxReceiptOptions).ToListAsync(ct);
        return receipts.Select(r => new DeliveryReceiptOption(r.ReceiptPublicId, r.Code, r.Name, r.ReceivedOn, r.StoreId,
            pending.GetValueOrDefault(r.ReceiptPublicId))).ToList();
    }

    /// <summary>Xoá phiếu chưa ký: bỏ luôn các dòng để bản sách chọn được cho phiếu khác.</summary>
    protected override async Task EnsureDeletableAsync(Delivery entity, CancellationToken ct)
    {
        entity.EnsureEditable();
        foreach (var line in await Db.Set<DeliveryLine>().Where(l => l.DeliveryId == entity.Id).ToListAsync(ct)) Db.Set<DeliveryLine>().Remove(line);
    }

    public async Task<IReadOnlyList<TransferLine>> LinesAsync(Guid publicId, CancellationToken ct)
    {
        var delivery = await LoadAsync(publicId, ct);
        return await StoreMoveResource.LineRows(Db, Db.Set<DeliveryLine>().AsNoTracking().Where(l => l.DeliveryId == delivery.Id)
            .Select(l => new StoreMoveResource.LineRef { PublicId = l.PublicId, ItemId = l.ItemId })).ToListAsync(ct);
    }

    /// <summary>Bản "chưa xếp giá" chưa thuộc phiếu giao nhận nào.</summary>
    public async Task<CrudPage<ItemDto>> CandidatesAsync(TransferCandidateSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        await LoadAsync(search.TransferId, ct);
        var taken = Db.Set<DeliveryLine>().Select(l => l.ItemId);
        var itemSearch = search.ToItemSearch();
        itemSearch.ItemStatus = ItemStatus.Unshelved;
        return await items.PageAsync(itemSearch, i => !taken.Contains(i.Id), ct);
    }

    public async Task<ItemBatchResult> AddLinesAsync(Guid publicId, AddLinesRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var delivery = await LoadAsync(publicId, ct);
        delivery.EnsureEditable();
        var (found, notFound) = await TransferLines.LoadAsync(Db, request, ct);
        var ids = found.Select(i => i.Id).ToList();
        var taken = await Db.Set<DeliveryLine>().Where(l => ids.Contains(l.ItemId)).Select(l => new { l.ItemId, l.DeliveryId }).ToListAsync(ct);
        var inThis = taken.Where(t => t.DeliveryId == delivery.Id).Select(t => t.ItemId).ToHashSet();
        var elsewhere = taken.Where(t => t.DeliveryId != delivery.Id).Select(t => t.ItemId).ToHashSet();
        var skipped = new List<SkippedItem>();
        var added = 0;
        foreach (var item in found)
        {
            if (inThis.Contains(item.Id)) skipped.Add(new SkippedItem(item.Barcode, "Đã có trong phiếu."));
            else if (elsewhere.Contains(item.Id)) skipped.Add(new SkippedItem(item.Barcode, "Đã nằm ở phiếu giao nhận khác."));
            else if (item.Status != ItemStatus.Unshelved) skipped.Add(new SkippedItem(item.Barcode, "Không ở trạng thái chưa xếp giá."));
            else
            {
                Db.Set<DeliveryLine>().Add(DeliveryLine.Of(delivery.Id, item.Id));
                added++;
            }
        }
        await Db.SaveChangesAsync(ct);
        return new ItemBatchResult(added, notFound, skipped);
    }

    public async Task RemoveLineAsync(Guid linePublicId, CancellationToken ct)
    {
        var line = await Db.Set<DeliveryLine>().FirstOrDefaultAsync(l => l.PublicId == linePublicId, ct) ?? throw new NotFoundException("Dòng phiếu", linePublicId);
        var delivery = await Set.FirstOrDefaultAsync(d => d.Id == line.DeliveryId, ct) ?? throw new NotFoundException(EntityName, line.DeliveryId);
        delivery.EnsureEditable();
        Db.Set<DeliveryLine>().Remove(line);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Ký nhận (monolith: SignConfirm, port ELIB-LRC 09-23/10-03): chỉ bản còn "chưa xếp giá" sang sẵn sàng phục vụ (vào kho nhận của
    /// phiếu nếu có) — bản đang mượn, mất, thanh lý… không bị đặt lại.
    /// </summary>
    public async Task<CompleteResult> SignAsync(Guid publicId, CancellationToken ct)
    {
        var delivery = await LoadAsync(publicId, ct);
        delivery.EnsureEditable();
        var ids = await Db.Set<DeliveryLine>().Where(l => l.DeliveryId == delivery.Id).Select(l => l.ItemId).ToListAsync(ct);
        var skipped = new List<SkippedItem>();
        var shelved = 0;
        foreach (var item in await Db.Set<Item>().Where(i => ids.Contains(i.Id)).OrderBy(i => i.BarcodeKey).ToListAsync(ct))
        {
            if (item.Status != ItemStatus.Unshelved)
            {
                skipped.Add(new SkippedItem(item.Barcode, $"Đang ở trạng thái \"{ItemStatus.Names.GetValueOrDefault(item.Status, item.Status)}\"."));
                continue;
            }
            item.Shelve(delivery.StoreId);
            await events.PublishAsync(item, deleted: false, ct);
            shelved++;
        }
        delivery.Sign(clock.GetUtcNow(), shelved);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Delivery), EntityName, delivery.PublicId, CrudChange.StatusChanged,
                $"Phiếu số {delivery.Id}: ký nhận, xếp giá {shelved} bản"), ct);
        await Db.SaveChangesAsync(ct);
        return new CompleteResult(shelved, skipped);
    }
}
