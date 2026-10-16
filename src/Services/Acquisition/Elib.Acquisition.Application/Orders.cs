using System.Linq.Expressions;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Acquisition.Application;

public sealed record PurchaseOrderRequest(
    string Name, DateOnly? OrderedOn = null, DateOnly? DueOn = null, long? SourceId = null, long? SupplierId = null, long? BudgetId = null,
    long? FundId = null, string? Note = null, int Status = OrderStatus.Draft, int PaymentMethod = Domain.PaymentMethod.Cash)
{
    public AcquisitionRefs Refs => new(SourceId, SupplierId, BudgetId, FundId);
}

public sealed class PurchaseOrderSearch : CrudSearch
{
    public int? OrderStatus { get; set; }
    public long? SupplierId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record PurchaseOrderDto(
    long Id, Guid PublicId, string Name, DateOnly? OrderedOn, DateOnly? DueOn, long? SourceId, string? SourceName, long? SupplierId, string? SupplierName,
    long? BudgetId, long? FundId, string? Note, int Status, int PaymentMethod, int LineCount, int Quantity, int Received, decimal Total, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Dòng đơn đặt: chọn biểu ghi đã có (<see cref="Mfn"/>) hoặc nhập mô tả tài liệu chưa biên mục. <see cref="LinePublicId"/> trống = thêm dòng.
/// </summary>
public sealed record OrderLineRequest(
    Guid? LinePublicId = null, long? Mfn = null, string? Title = null, string? Author = null, string? Publisher = null, string? PublishYear = null,
    string? Isbn = null, int Quantity = 1, decimal? Price = null, string? Currency = null, decimal? Rate = null, string? CancelReason = null);

/// <summary><see cref="Received"/>: tổng số bản các dòng phiếu nhận "từ đơn đặt" trỏ về dòng này; <see cref="Remaining"/> = còn chờ nhận.</summary>
public sealed record OrderLineDto(
    long Id, Guid PublicId, long OrderId, Guid? BibPublicId, long? Mfn, string Title, string? Author, string? Publisher, string? PublishYear, string? Isbn,
    int Quantity, decimal? Price, string? Currency, decimal Rate, string? CancelReason, int Received, int Remaining);

/// <summary>Tìm dòng đơn đặt còn chờ nhận trên nhiều đơn (monolith: LookupReceivableLines) — màn phiếu nhận "chọn từ đơn đặt".</summary>
public sealed class ReceivableSearch : CrudSearch
{
    public long? OrderId { get; set; }
    public long? SupplierId { get; set; }
}

public sealed record ReceivableLine(
    Guid OrderLinePublicId, long OrderId, string OrderName, long? SupplierId, Guid? BibPublicId, long? Mfn, string Title, string? Author, string? Isbn,
    int Quantity, int Received, int Remaining, decimal? Price, string? Currency, decimal Rate);

/// <summary>
/// Đơn đặt bổ sung (monolith: CatalogueOrderController + CatalogueBookOrderController, quyền AB_ORDERS). Không xoá được đơn / dòng đã
/// có sách nhận; không giảm số lượng đặt xuống dưới số đã nhận; đơn đã huỷ không sửa dòng và không nhận thêm.
/// </summary>
public sealed class PurchaseOrderResource(ICrudDbContext db, BibSnapshots bibs)
    : CrudResource<PurchaseOrderResource, PurchaseOrder, PurchaseOrderSearch, PurchaseOrderRequest, PurchaseOrderDto>(db)
{
    public const int MaxLines = 2000;

    protected override string EntityName => "Đơn đặt";

    protected override string? Describe(PurchaseOrder entity) => $"Đơn đặt số {entity.Id}: {entity.Name}";

    protected override Expression<Func<PurchaseOrder, PurchaseOrderDto>> Projection => x => new PurchaseOrderDto(
        x.Id, x.PublicId, x.Name, x.OrderedOn, x.DueOn, x.SourceId, Db.Set<AcquisitionSource>().Where(s => s.Id == x.SourceId).Select(s => s.Name).FirstOrDefault(),
        x.SupplierId, Db.Set<Supplier>().Where(s => s.Id == x.SupplierId).Select(s => s.Name).FirstOrDefault(), x.BudgetId, x.FundId, x.Note, x.Status,
        x.PaymentMethod, Db.Set<OrderLine>().Count(l => l.OrderId == x.Id), Db.Set<OrderLine>().Where(l => l.OrderId == x.Id).Sum(l => (int?)l.Quantity) ?? 0,
        (from l in Db.Set<OrderLine>() where l.OrderId == x.Id
         join r in Db.Set<ReceiptLine>() on (long?)l.Id equals r.OrderLineId
         select (int?)r.Quantity).Sum() ?? 0,
        Db.Set<OrderLine>().Where(l => l.OrderId == x.Id && l.CancelReason == null).Sum(l => (decimal?)(l.Quantity * (l.Price ?? 0m) * l.Rate)) ?? 0m,
        x.CreatedAt, x.UpdatedAt);

    protected override PurchaseOrder Create(PurchaseOrderRequest request) =>
        PurchaseOrder.Create(request.Name, request.OrderedOn, request.DueOn, request.Refs, request.Note, request.Status, request.PaymentMethod);

    protected override void Update(PurchaseOrder entity, PurchaseOrderRequest request) =>
        entity.Update(request.Name, request.OrderedOn, request.DueOn, request.Refs, request.Note, request.Status, request.PaymentMethod);

    protected override IQueryable<PurchaseOrder> Filter(IQueryable<PurchaseOrder> query, PurchaseOrderSearch search)
    {
        if (search.OrderStatus is { } status) query = query.Where(x => x.Status == status);
        if (search.SupplierId is { } supplier) query = query.Where(x => x.SupplierId == supplier);
        if (search.From is { } from) query = query.Where(x => x.OrderedOn >= from);
        if (search.To is { } to) query = query.Where(x => x.OrderedOn <= to);
        if (search.Term is { } term)
        {
            var id = long.TryParse(term, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : -1;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => x.Id == id || x.Name.ToLower().Contains(term) || (x.Note != null && x.Note.ToLower().Contains(term))
                || Db.Set<OrderLine>().Any(l => l.OrderId == x.Id && l.Title.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override Task ValidateAsync(PurchaseOrder entity, CancellationToken ct) => AcquisitionRefChecks.EnsureAsync(Db, Refs(entity), ct);

    protected override async Task EnsureDeletableAsync(PurchaseOrder entity, CancellationToken ct)
    {
        var lines = await Db.Set<OrderLine>().Where(l => l.OrderId == entity.Id).ToListAsync(ct);
        var ids = lines.Select(l => (long?)l.Id).ToList();
        if (await Db.Set<ReceiptLine>().AnyAsync(r => ids.Contains(r.OrderLineId), ct))
            throw new ConflictException("ORDER_RECEIVED", $"Đơn đặt số {entity.Id} đã có sách nhận — không xoá được (đổi tình trạng sang \"Đã huỷ\" nếu cần).");
        foreach (var line in lines) Db.Set<OrderLine>().Remove(line);
    }

    public async Task<IReadOnlyList<OrderLineDto>> LinesAsync(Guid publicId, CancellationToken ct)
    {
        var order = await LoadAsync(publicId, ct);
        return await LineRows(Db.Set<OrderLine>().AsNoTracking().Where(l => l.OrderId == order.Id)).ToListAsync(ct);
    }

    public async Task<OrderLineDto> SaveLineAsync(Guid publicId, OrderLineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var order = await LoadAsync(publicId, ct);
        order.EnsureLinesEditable();
        var bib = request.Mfn is { } mfn ? await bibs.RequireAsync(mfn, ct) : null;
        if (bib is null && string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("TITLE_INVALID", "Chọn biểu ghi hoặc nhập nhan đề tài liệu cần đặt.");
        var item = new OrderItemData(request.Title ?? "", request.Author, request.Publisher, request.PublishYear, request.Isbn);

        OrderLine line;
        if (request.LinePublicId is { } linePublicId)
        {
            line = await Db.Set<OrderLine>().FirstOrDefaultAsync(l => l.PublicId == linePublicId && l.OrderId == order.Id, ct)
                ?? throw new NotFoundException("Dòng đơn đặt", linePublicId);
            var received = await ReceivedAsync(line.Id, ct);
            if (request.Quantity < received)
                throw new BusinessRuleException("QUANTITY_BELOW_RECEIVED", $"Dòng đã nhận {received} bản — số lượng đặt không được nhỏ hơn.");
            line.Update(bib, item, request.Quantity, request.Price, request.Currency, request.Rate, request.CancelReason);
        }
        else
        {
            if (await Db.Set<OrderLine>().CountAsync(l => l.OrderId == order.Id, ct) >= MaxLines)
                throw new BusinessRuleException("LINES_TOO_MANY", $"Một đơn đặt tối đa {MaxLines:N0} dòng.");
            line = OrderLine.Create(order.Id, bib, item, request.Quantity, request.Price, request.Currency, request.Rate, request.CancelReason);
            Db.Set<OrderLine>().Add(line);
        }
        if (line.BibPublicId is { } bibId && await Db.Set<OrderLine>().AnyAsync(l => l.OrderId == order.Id && l.BibPublicId == bibId && l.Id != line.Id, ct))
            throw new ConflictException("LINE_EXISTS", $"Biểu ghi MFN {line.Mfn} đã có trong đơn — sửa số lượng ở dòng đó.");
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(PurchaseOrder), EntityName, order.PublicId, CrudChange.Updated,
                $"Đơn đặt số {order.Id}: {(request.LinePublicId is null ? "thêm" : "sửa")} dòng \"{line.Title}\" × {line.Quantity}"), ct);
        await Db.SaveChangesAsync(ct);
        return await LineRows(Db.Set<OrderLine>().AsNoTracking().Where(l => l.Id == line.Id)).SingleAsync(ct);
    }

    public async Task RemoveLineAsync(Guid linePublicId, CancellationToken ct)
    {
        var line = await Db.Set<OrderLine>().FirstOrDefaultAsync(l => l.PublicId == linePublicId, ct) ?? throw new NotFoundException("Dòng đơn đặt", linePublicId);
        var order = await Set.FirstOrDefaultAsync(o => o.Id == line.OrderId, ct) ?? throw new NotFoundException(EntityName, line.OrderId);
        order.EnsureLinesEditable();
        var received = await ReceivedAsync(line.Id, ct);
        if (received > 0)
            throw new ConflictException("LINE_RECEIVED", $"Dòng \"{line.Title}\" đã nhận {received} bản — không xoá được; ghi lý do huỷ để ngừng nhận.");
        Db.Set<OrderLine>().Remove(line);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(PurchaseOrder), EntityName, order.PublicId, CrudChange.Updated,
                $"Đơn đặt số {order.Id}: bỏ dòng \"{line.Title}\""), ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task<CrudPage<ReceivableLine>> ReceivableAsync(ReceivableSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query =
            from l in Db.Set<OrderLine>().AsNoTracking()
            join o in Db.Set<PurchaseOrder>() on l.OrderId equals o.Id
            where o.Status != OrderStatus.Cancelled && l.CancelReason == null
            let received = Db.Set<ReceiptLine>().Where(r => r.OrderLineId == l.Id).Sum(r => (int?)r.Quantity) ?? 0
            where l.Quantity > received
            select new { l, o, received };
        if (search.OrderId is { } orderId) query = query.Where(x => x.o.Id == orderId);
        if (search.SupplierId is { } supplier) query = query.Where(x => x.o.SupplierId == supplier);
        if (search.Term is { } term)
        {
            var id = long.TryParse(term, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : -1;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => x.o.Id == id || x.l.Mfn == id || x.l.Title.ToLower().Contains(term)
                || (x.l.Author != null && x.l.Author.ToLower().Contains(term)) || (x.l.Isbn != null && x.l.Isbn.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        var (page, size) = (Math.Max(1, search.PageIndex), Math.Clamp(search.PageSize, 1, 200));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.o.Id).ThenBy(x => x.l.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new ReceivableLine(x.l.PublicId, x.o.Id, x.o.Name, x.o.SupplierId, x.l.BibPublicId, x.l.Mfn, x.l.Title, x.l.Author, x.l.Isbn,
                x.l.Quantity, x.received, x.l.Quantity - x.received, x.l.Price, x.l.Currency, x.l.Rate))
            .ToListAsync(ct);
        return new CrudPage<ReceivableLine>(rows, total, page, size);
    }

    private Task<int> ReceivedAsync(long orderLineId, CancellationToken ct) =>
        Db.Set<ReceiptLine>().Where(r => r.OrderLineId == orderLineId).SumAsync(r => r.Quantity, ct);

    private IQueryable<OrderLineDto> LineRows(IQueryable<OrderLine> lines) =>
        from l in lines
        let received = Db.Set<ReceiptLine>().Where(r => r.OrderLineId == l.Id).Sum(r => (int?)r.Quantity) ?? 0
        orderby l.Id
        select new OrderLineDto(l.Id, l.PublicId, l.OrderId, l.BibPublicId, l.Mfn, l.Title, l.Author, l.Publisher, l.PublishYear, l.Isbn, l.Quantity, l.Price,
            l.Currency, l.Rate, l.CancelReason, received, l.CancelReason != null || received >= l.Quantity ? 0 : l.Quantity - received);

    private static AcquisitionRefs Refs(PurchaseOrder o) => new(o.SourceId, o.SupplierId, o.BudgetId, o.FundId);
}
