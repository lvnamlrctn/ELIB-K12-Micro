using System.Linq.Expressions;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Acquisition;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Acquisition.Application;

public sealed record ReceiptRequest(
    DateOnly ReceivedOn, string? Name = null, long? SourceId = null, long? SupplierId = null, long? BudgetId = null, long? FundId = null,
    long? StoreId = null, string? Note = null, int PaymentStatus = Domain.PaymentStatus.Unpaid, int PaymentMethod = Domain.PaymentMethod.Cash)
{
    public AcquisitionRefs Refs => new(SourceId, SupplierId, BudgetId, FundId);
}

/// <summary>Đổi thanh toán của phiếu (kể cả phiếu đã hoàn tất).</summary>
public sealed record ReceiptPaymentRequest(int PaymentStatus, int PaymentMethod);

public sealed class ReceiptSearch : CrudSearch
{
    public bool? Completed { get; set; }
    public long? SupplierId { get; set; }
    public long? SourceId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record ReceiptDto(
    long Id, Guid PublicId, string? Name, DateOnly ReceivedOn, long? SourceId, string? SourceName, long? SupplierId, string? SupplierName, long? BudgetId,
    long? FundId, long? StoreId, string? Note, int PaymentStatus, int PaymentMethod, DateTimeOffset? CompletedAt, int LineCount, int Quantity, decimal Total,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// Thêm/sửa dòng phiếu nhận. <see cref="LinePublicId"/> trống = thêm: chọn biểu ghi (<see cref="Mfn"/>), hoặc dòng đơn đặt
/// (<see cref="OrderLinePublicId"/> — lấy biểu ghi của dòng đặt nếu có, số bản không vượt số còn chờ nhận).
/// </summary>
public sealed record ReceiptLineRequest(
    Guid? LinePublicId = null, long? Mfn = null, int Quantity = 1, decimal? Price = null, string? Currency = null, Guid? OrderLinePublicId = null,
    decimal? Rate = null);

/// <summary><see cref="Registered"/>: số ĐKCB đã đăng ký cho dòng (null = holdings không trả lời được).</summary>
public sealed record ReceiptLineDto(
    long Id, Guid PublicId, Guid BibPublicId, long Mfn, string Title, string? Author, int Quantity, decimal? Price, string? Currency, decimal Rate,
    Guid? OrderLinePublicId, long? OrderId, int? Registered);

/// <summary>Dòng phiếu kèm ĐKCB đã đăng ký; <see cref="ItemsAvailable"/> = false khi holdings không trả lời (chỉ hiện dòng).</summary>
public sealed record ReceiptLinesView(IReadOnlyList<ReceiptLineDto> Lines, IReadOnlyList<ReceiptItem> Items, bool ItemsAvailable);

/// <summary>Đăng ký ĐKCB cho một dòng: theo lô (<see cref="Quantity"/> + tiền tố) hoặc một mã nhập tay (<see cref="Barcode"/>).</summary>
public sealed record RegisterReceiptItemsRequest(
    int Quantity = 0, string? Prefix = null, int Digits = 6, long? StartNumber = null, string? Barcode = null, long? StoreId = null);

/// <summary>
/// Phiếu nhận / đơn nhận (monolith: CatalogueReceiptController, quyền AB_RECEIPTS). Mỗi dòng một biểu ghi với số bản nhận; ĐKCB đăng
/// ký từ dòng (holdings khoá: không vượt số bản của dòng — port ELIB-LRC 09-22). "Hoàn tất" khoá phiếu (không sửa, không thêm/bỏ dòng,
/// không đăng ký/xoá ĐKCB) và đưa phiếu vào ô chọn đơn nhận của giao nhận; "Mở lại" (quyền xoá — như monolith chỉ quản trị sửa được
/// đơn đã hoàn tất). Mọi thay đổi phát <see cref="ReceiptChanged"/> (outbox).
/// </summary>
public sealed class ReceiptResource(
    ICrudDbContext db, BibSnapshots bibs, IHoldingsReceiptItems holdings, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor,
    TimeProvider clock)
    : CrudResource<ReceiptResource, Receipt, ReceiptSearch, ReceiptRequest, ReceiptDto>(db)
{
    public const int MaxLines = 2000;

    protected override string EntityName => "Phiếu nhận";

    protected override string? Describe(Receipt entity) => $"Phiếu nhận số {entity.Id}" + (entity.Name is { } n ? $": {n}" : "");

    protected override Expression<Func<Receipt, ReceiptDto>> Projection => x => new ReceiptDto(
        x.Id, x.PublicId, x.Name, x.ReceivedOn, x.SourceId, Db.Set<AcquisitionSource>().Where(s => s.Id == x.SourceId).Select(s => s.Name).FirstOrDefault(),
        x.SupplierId, Db.Set<Supplier>().Where(s => s.Id == x.SupplierId).Select(s => s.Name).FirstOrDefault(), x.BudgetId, x.FundId, x.StoreId, x.Note,
        x.PaymentStatus, x.PaymentMethod, x.CompletedAt, Db.Set<ReceiptLine>().Count(l => l.ReceiptId == x.Id),
        Db.Set<ReceiptLine>().Where(l => l.ReceiptId == x.Id).Sum(l => (int?)l.Quantity) ?? 0,
        Db.Set<ReceiptLine>().Where(l => l.ReceiptId == x.Id).Sum(l => (decimal?)(l.Quantity * (l.Price ?? 0m) * l.Rate)) ?? 0m,
        x.CreatedAt, x.UpdatedAt);

    protected override Receipt Create(ReceiptRequest request) =>
        Receipt.Create(request.Name, request.ReceivedOn, request.Refs, request.StoreId, request.Note, request.PaymentStatus, request.PaymentMethod);

    protected override void Update(Receipt entity, ReceiptRequest request) =>
        entity.Update(request.Name, request.ReceivedOn, request.Refs, request.StoreId, request.Note, request.PaymentStatus, request.PaymentMethod);

    protected override IQueryable<Receipt> Filter(IQueryable<Receipt> query, ReceiptSearch search)
    {
        if (search.Completed is { } done) query = done ? query.Where(x => x.CompletedAt != null) : query.Where(x => x.CompletedAt == null);
        if (search.SupplierId is { } supplier) query = query.Where(x => x.SupplierId == supplier);
        if (search.SourceId is { } source) query = query.Where(x => x.SourceId == source);
        if (search.From is { } from) query = query.Where(x => x.ReceivedOn >= from);
        if (search.To is { } to) query = query.Where(x => x.ReceivedOn <= to);
        if (search.Term is { } term)
        {
            var id = long.TryParse(term, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : -1;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => x.Id == id || (x.Name != null && x.Name.ToLower().Contains(term)) || (x.Note != null && x.Note.ToLower().Contains(term))
                || Db.Set<ReceiptLine>().Any(l => l.ReceiptId == x.Id && (l.Mfn == id || l.Title.ToLower().Contains(term))));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override Task ValidateAsync(Receipt entity, CancellationToken ct) =>
        AcquisitionRefChecks.EnsureAsync(Db, new AcquisitionRefs(entity.SourceId, entity.SupplierId, entity.BudgetId, entity.FundId), ct);

    /// <summary>Phiếu mới chưa có dòng — chưa phát event (Id = mã phiếu chỉ có sau khi lưu); sửa/xoá thì phát trạng thái mới.</summary>
    protected override async Task OnSavingAsync(Receipt entity, CrudChange change, CancellationToken ct)
    {
        if (change == CrudChange.Added) return;
        if (change == CrudChange.Deleted) entity.Touch();
        await PublishAsync(entity, deleted: change == CrudChange.Deleted, ct);
    }

    /// <summary>Xoá phiếu chưa hoàn tất và chưa có ĐKCB (hỏi holdings) — bỏ luôn các dòng.</summary>
    protected override async Task EnsureDeletableAsync(Receipt entity, CancellationToken ct)
    {
        entity.EnsureEditable();
        var items = await holdings.ListAsync(tenant.RequireTenantId(), entity.PublicId, ct);
        if (items.Count > 0)
            throw new ConflictException("RECEIPT_HAS_ITEMS", $"Phiếu nhận số {entity.Id} đã đăng ký {items.Count} ĐKCB — xoá ĐKCB trước.");
        foreach (var line in await Db.Set<ReceiptLine>().Where(l => l.ReceiptId == entity.Id).ToListAsync(ct)) Db.Set<ReceiptLine>().Remove(line);
    }

    public async Task<ReceiptLinesView> LinesAsync(Guid publicId, CancellationToken ct)
    {
        var receipt = await LoadAsync(publicId, ct);
        var lines = await LineRows(Db.Set<ReceiptLine>().AsNoTracking().Where(l => l.ReceiptId == receipt.Id)).ToListAsync(ct);
        IReadOnlyList<ReceiptItem>? items;
        try
        {
            items = await holdings.ListAsync(tenant.RequireTenantId(), receipt.PublicId, ct);
        }
        catch (BusinessRuleException ex) when (ex.StatusCode >= 500)
        {
            items = null; // holdings đang down — vẫn xem/sửa được dòng, chỉ thiếu số ĐKCB
        }
        if (items is null) return new ReceiptLinesView(lines, [], false);
        var counts = items.Where(i => i.LinePublicId is not null).GroupBy(i => i.LinePublicId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return new ReceiptLinesView([.. lines.Select(l => l with { Registered = counts.GetValueOrDefault(l.PublicId) })], items, true);
    }

    public async Task<ReceiptLineDto> SaveLineAsync(Guid publicId, ReceiptLineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var receipt = await LoadAsync(publicId, ct);
        receipt.EnsureEditable();

        ReceiptLine line;
        if (request.LinePublicId is { } linePublicId)
        {
            line = await Db.Set<ReceiptLine>().FirstOrDefaultAsync(l => l.PublicId == linePublicId && l.ReceiptId == receipt.Id, ct)
                ?? throw new NotFoundException("Dòng phiếu nhận", linePublicId);
            if (request.Quantity < line.Quantity)
            {
                // Khoá số lượng theo ĐKCB: không giảm xuống dưới số đã đăng ký (hỏi holdings — không trả lời được thì không cho giảm).
                var registered = (await holdings.ListAsync(tenant.RequireTenantId(), receipt.PublicId, ct)).Count(i => i.LinePublicId == line.PublicId);
                if (request.Quantity < registered)
                    throw new BusinessRuleException("QUANTITY_BELOW_REGISTERED",
                        $"Dòng MFN {line.Mfn} đã đăng ký {registered} ĐKCB — số bản nhận không được nhỏ hơn (xoá bớt ĐKCB trước).");
            }
            if (line.OrderLineId is { } orderLineId) await EnsureOrderRemainingAsync(orderLineId, request.Quantity, line.Id, ct);
            line.Update(null, request.Quantity, request.Price, request.Currency, request.Rate);
        }
        else
        {
            if (await Db.Set<ReceiptLine>().CountAsync(l => l.ReceiptId == receipt.Id, ct) >= MaxLines)
                throw new BusinessRuleException("LINES_TOO_MANY", $"Một phiếu nhận tối đa {MaxLines:N0} dòng.");
            OrderLine? orderLine = null;
            if (request.OrderLinePublicId is { } orderLinePublicId)
            {
                orderLine = await Db.Set<OrderLine>().FirstOrDefaultAsync(l => l.PublicId == orderLinePublicId, ct)
                    ?? throw new NotFoundException("Dòng đơn đặt", orderLinePublicId);
                var order = await Db.Set<PurchaseOrder>().FirstAsync(o => o.Id == orderLine.OrderId, ct);
                order.EnsureLinesEditable();
                if (orderLine.CancelReason is not null)
                    throw new BusinessRuleException("ORDER_LINE_CANCELLED", $"Dòng đặt \"{orderLine.Title}\" đã huỷ ({orderLine.CancelReason}).");
                await EnsureOrderRemainingAsync(orderLine.Id, request.Quantity, null, ct);
            }
            var mfn = request.Mfn ?? orderLine?.Mfn
                ?? throw new BusinessRuleException("MFN_REQUIRED", orderLine is null
                    ? "Chọn biểu ghi của tài liệu nhận."
                    : $"Dòng đặt \"{orderLine.Title}\" chưa có biểu ghi — biên mục rồi chọn biểu ghi (MFN) cho dòng nhận.");
            var bib = await bibs.RequireAsync(mfn, ct);
            if (await Db.Set<ReceiptLine>().AnyAsync(l => l.ReceiptId == receipt.Id && l.BibPublicId == bib.BibPublicId, ct))
                throw new ConflictException("LINE_EXISTS", $"Biểu ghi MFN {bib.Mfn} đã có trong phiếu — sửa số bản ở dòng đó.");
            line = ReceiptLine.Create(receipt.Id, bib, request.Quantity, request.Price ?? orderLine?.Price, request.Currency ?? orderLine?.Currency,
                request.Rate ?? orderLine?.Rate, orderLine?.Id);
            line.PublicId = Guid.CreateVersion7(); // event phát trước khi lưu — dòng cần sẵn PublicId
            Db.Set<ReceiptLine>().Add(line);
        }

        receipt.Touch();
        await PublishAsync(receipt, deleted: false, ct);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Receipt), EntityName, receipt.PublicId, CrudChange.Updated,
                $"Phiếu nhận số {receipt.Id}: {(request.LinePublicId is null ? "thêm" : "sửa")} dòng MFN {line.Mfn} × {line.Quantity}"), ct);
        await Db.SaveChangesAsync(ct);
        return await LineRows(Db.Set<ReceiptLine>().AsNoTracking().Where(l => l.Id == line.Id)).SingleAsync(ct);
    }

    public async Task RemoveLineAsync(Guid linePublicId, CancellationToken ct)
    {
        var (receipt, line) = await LoadLineAsync(linePublicId, ct);
        receipt.EnsureEditable();
        var registered = (await holdings.ListAsync(tenant.RequireTenantId(), receipt.PublicId, ct)).Count(i => i.LinePublicId == line.PublicId);
        if (registered > 0)
            throw new ConflictException("LINE_HAS_ITEMS", $"Dòng MFN {line.Mfn} còn {registered} ĐKCB — xoá ĐKCB trước.");
        Db.Set<ReceiptLine>().Remove(line);
        receipt.Touch();
        await PublishAsync(receipt, deleted: false, ct);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Receipt), EntityName, receipt.PublicId, CrudChange.Updated,
                $"Phiếu nhận số {receipt.Id}: bỏ dòng MFN {line.Mfn}"), ct);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>Đăng ký ĐKCB cho một dòng (monolith: RegisterBarcodes / RegisterSingleBarcode) — holdings khoá số lượng theo dòng.</summary>
    public async Task<IReadOnlyList<RegisteredItem>> RegisterItemsAsync(Guid linePublicId, RegisterReceiptItemsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (receipt, line) = await LoadLineAsync(linePublicId, ct);
        receipt.EnsureEditable();
        var state = await StateAsync(receipt, deleted: false, ct);
        var items = await holdings.RegisterAsync(tenant.RequireTenantId(), new HoldingsRegisterRequest(state, line.PublicId, request.Quantity, request.Prefix,
            request.Digits, request.StartNumber, request.Barcode, request.StoreId), ct);
        if (AuditSink is not null && items.Count > 0)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Receipt), EntityName, receipt.PublicId, CrudChange.Updated,
                $"Phiếu nhận số {receipt.Id}: đăng ký {items.Count} ĐKCB {items[0].Barcode}" + (items.Count > 1 ? $"–{items[^1].Barcode}" : "")
                + $" cho MFN {line.Mfn}"), ct);
        await Db.SaveChangesAsync(ct);
        return items;
    }

    /// <summary>Xoá ĐKCB đăng ký nhầm (monolith: DeleteBarcode) — phiếu chưa hoàn tất; holdings chặn bản đã xếp giá / đã giao nhận.</summary>
    public async Task RemoveItemAsync(Guid publicId, Guid itemPublicId, CancellationToken ct)
    {
        var receipt = await LoadAsync(publicId, ct);
        receipt.EnsureEditable();
        await holdings.RemoveAsync(tenant.RequireTenantId(), receipt.PublicId, itemPublicId, ct);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Receipt), EntityName, receipt.PublicId, CrudChange.Updated,
                $"Phiếu nhận số {receipt.Id}: xoá một ĐKCB"), ct);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>Chốt thanh toán — quyền sửa phiếu, làm được sau khi phiếu đã hoàn tất (chỉ đổi trạng thái / phương thức thanh toán, không đụng dòng, ĐKCB).</summary>
    public async Task<ReceiptDto> SetPaymentAsync(Guid publicId, ReceiptPaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var receipt = await LoadAsync(publicId, ct);
        receipt.SetPayment(request.PaymentStatus, request.PaymentMethod);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Receipt), EntityName, receipt.PublicId, CrudChange.StatusChanged,
                $"Phiếu nhận số {receipt.Id}: thanh toán — {PaymentStatus.Names[receipt.PaymentStatus]}, {PaymentMethod.Names[receipt.PaymentMethod]}"), ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(receipt);
    }

    /// <summary>Hoàn tất (khoá) phiếu — cần ít nhất một dòng.</summary>
    public async Task<ReceiptDto> CompleteAsync(Guid publicId, CancellationToken ct)
    {
        var receipt = await LoadAsync(publicId, ct);
        if (!await Db.Set<ReceiptLine>().AnyAsync(l => l.ReceiptId == receipt.Id, ct))
            throw new BusinessRuleException("RECEIPT_EMPTY", $"Phiếu nhận số {receipt.Id} chưa có dòng nào.");
        receipt.Complete(clock.GetUtcNow());
        return await ChangeLockAsync(receipt, "hoàn tất (khoá)", ct);
    }

    public async Task<ReceiptDto> ReopenAsync(Guid publicId, CancellationToken ct)
    {
        var receipt = await LoadAsync(publicId, ct);
        receipt.Reopen();
        return await ChangeLockAsync(receipt, "mở lại", ct);
    }

    private async Task<ReceiptDto> ChangeLockAsync(Receipt receipt, string what, CancellationToken ct)
    {
        await PublishAsync(receipt, deleted: false, ct);
        if (AuditSink is not null)
            await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Receipt), EntityName, receipt.PublicId, CrudChange.StatusChanged,
                $"Phiếu nhận số {receipt.Id}: {what}"), ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(receipt);
    }

    private async Task EnsureOrderRemainingAsync(long orderLineId, int quantity, long? exceptLineId, CancellationToken ct)
    {
        var ordered = await Db.Set<OrderLine>().Where(l => l.Id == orderLineId).Select(l => new { l.Quantity, l.Title }).FirstAsync(ct);
        var received = await Db.Set<ReceiptLine>().Where(r => r.OrderLineId == orderLineId && r.Id != exceptLineId).SumAsync(r => r.Quantity, ct);
        if (received + quantity > ordered.Quantity)
            throw new BusinessRuleException("ORDER_LINE_EXCEEDED",
                $"Dòng đặt \"{ordered.Title}\" còn chờ nhận {Math.Max(0, ordered.Quantity - received)} bản — tăng số lượng đặt nếu nhận thêm.");
    }

    private async Task<(Receipt Receipt, ReceiptLine Line)> LoadLineAsync(Guid linePublicId, CancellationToken ct)
    {
        var line = await Db.Set<ReceiptLine>().FirstOrDefaultAsync(l => l.PublicId == linePublicId, ct) ?? throw new NotFoundException("Dòng phiếu nhận", linePublicId);
        var receipt = await Set.FirstOrDefaultAsync(r => r.Id == line.ReceiptId, ct) ?? throw new NotFoundException(EntityName, line.ReceiptId);
        return (receipt, line);
    }

    private async Task PublishAsync(Receipt receipt, bool deleted, CancellationToken ct) => await publisher.Publish(await StateAsync(receipt, deleted, ct), ct);

    /// <summary>
    /// Trạng thái hiện tại của phiếu kèm dòng — gồm cả thay đổi chưa lưu trong DbContext (dòng vừa thêm/sửa/bỏ), vì event được phát
    /// trước SaveChanges để vào outbox cùng transaction.
    /// </summary>
    private async Task<ReceiptChanged> StateAsync(Receipt receipt, bool deleted, CancellationToken ct)
    {
        await Db.Set<ReceiptLine>().Where(l => l.ReceiptId == receipt.Id).LoadAsync(ct);
        var lines = Db.Set<ReceiptLine>().Local.Where(l => l.ReceiptId == receipt.Id).OrderBy(l => l.Id == 0 ? long.MaxValue : l.Id).ToList();
        if (receipt.PublicId == Guid.Empty) receipt.PublicId = Guid.CreateVersion7();
        return new ReceiptChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            ReceiptPublicId = receipt.PublicId,
            Code = receipt.Id,
            Name = receipt.Name,
            ReceivedOn = receipt.ReceivedOn,
            StoreId = receipt.StoreId,
            SourceName = await Db.Set<AcquisitionSource>().Where(s => s.Id == receipt.SourceId).Select(s => s.Name).FirstOrDefaultAsync(ct),
            SupplierName = await Db.Set<Supplier>().Where(s => s.Id == receipt.SupplierId).Select(s => s.Name).FirstOrDefaultAsync(ct),
            Completed = receipt.IsCompleted,
            Deleted = deleted,
            Version = receipt.Version,
            Lines = deleted ? [] : [.. lines.Select(l => new ReceiptLineState
            {
                LinePublicId = l.PublicId, BibPublicId = l.BibPublicId, Mfn = l.Mfn, Quantity = l.Quantity, Price = l.Price, Currency = l.Currency,
            })],
        };
    }

    private IQueryable<ReceiptLineDto> LineRows(IQueryable<ReceiptLine> lines) =>
        from l in lines
        join o in Db.Set<OrderLine>() on l.OrderLineId equals (long?)o.Id into oj
        from o in oj.DefaultIfEmpty()
        orderby l.Id
        select new ReceiptLineDto(l.Id, l.PublicId, l.BibPublicId, l.Mfn, l.Title, l.Author, l.Quantity, l.Price, l.Currency, l.Rate,
            o == null ? null : o.PublicId, o == null ? null : o.OrderId, null);
}
