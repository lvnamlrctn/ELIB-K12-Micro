using Elib.BuildingBlocks.Crud;
using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Holdings;
using Elib.Holdings.Domain;
using MassTransit;
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

/// <summary>Số ĐKCB kèm lý do — khôi phục sách mất, huỷ thanh lý (hoàn tác nên lý do bắt buộc).</summary>
public sealed record ItemReasonRequest(string? Barcode, string? Reason);

/// <summary>Đã xử lý (<see cref="Result"/>) hoặc chờ duyệt (<see cref="Pending"/>, HTTP 202, chưa đổi gì).</summary>
public sealed record ItemBatchOutcome(ItemBatchResult? Result, PendingApproval? Pending);

/// <summary>Các thao tác kho cân nhắc phê duyệt: báo mất theo lô (từ 10 bản), khôi phục mất, thanh lý (mọi lần), huỷ thanh lý.</summary>
public static class StoreApprovals
{
    public const string LostMark = "LOST_MARK";
    public const string LostRestore = "LOST_RESTORE";
    public const string Liquidate = "LIQUIDATE";
    public const string LiquidateCancel = "LIQUIDATE_CANCEL";

    public static readonly ApprovalKind[] Kinds =
    [
        new(LostMark, "LOST_BOOKS", nameof(Item), "Bản sách", "Báo mất nhiều bản", "Báo mất một lần từ số bản này trở lên.", Count: 10),
        new(LostRestore, "LOST_BOOKS", nameof(Item), "Bản sách", "Khôi phục sách đã báo mất", "Hoàn tác báo mất (tìm thấy lại): bản sách trở về trạng thái trước khi mất."),
        new(Liquidate, "LIQUIDATES", nameof(Item), "Bản sách", "Thanh lý tài liệu", "Mọi lần thanh lý (rút bản sách khỏi kho vĩnh viễn) đều cần người có thẩm quyền duyệt."),
        new(LiquidateCancel, "LIQUIDATES", nameof(Item), "Bản sách", "Huỷ thanh lý", "Hoàn tác thanh lý: bản sách trở về trạng thái trước khi thanh lý."),
    ];
}

public sealed record LiquidationRow(
    long Id, Guid PublicId, string Barcode, Guid? ItemPublicId, long? Mfn, string? Title, string? Author, string? Publisher, long? StoreId,
    string? StoreName, DateOnly LiquidatedOn, string? Reason, string? ItemStatus, DateTimeOffset CreatedAt, string? DecisionNo = null,
    DateOnly? DecisionDate = null);

public sealed record LiquidateRequest(IReadOnlyList<string>? Barcodes = null, DateOnly? LiquidatedOn = null, string? Reason = null,
    string? DecisionNo = null, DateOnly? DecisionDate = null);

/// <summary>
/// Xử lý sách mất (monolith: StoreLostBookController + LostBookService, quyền LOST_BOOKS): báo mất theo lô ĐKCB, khôi phục khi tìm
/// thấy, danh sách + xuất Excel. Bản đang mượn không báo mất ở đây — xử lý qua phiếu phạt ở circulation (lượt mượn đóng vì mất
/// cũng ghi bản ghi báo mất, xem <see cref="ItemResource.ApplyLoanAsync"/>).
/// </summary>
public sealed class LostBooks(
    ICrudDbContext db, ItemEvents events, HoldingsExcel excel, TimeProvider clock, ApprovalService approvals, IPublishEndpoint publisher, ITenantContext tenant,
    ICurrentActor actor, CrudAuditContext? auditContext = null, ICrudAuditSink? audit = null)
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

    /// <summary>
    /// Báo mất theo lô: lý do BẮT BUỘC; bản đang mượn / đã mất / đã thanh lý bị bỏ qua kèm lý do; các bản còn lại lưu một lần. Báo mất từ số
    /// bản ngưỡng trở lên (mặc định 10) vượt thẩm quyền → cả lô chờ người có LOST_BOOKS:approve duyệt, chưa đổi gì.
    /// </summary>
    public async Task<ItemBatchOutcome> MarkLostAsync(MarkLostRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ChangeReason.Require(request.Reason, auditContext?.Capture().Reason, "báo mất");
        var (items, notFound) = await ItemBarcodes.LoadAsync(db, request.Barcodes, ct);
        if (items.Count > 0 && await approvals.RequiresAsync(StoreApprovals.LostMark, null, null, items.Count, ct))
        {
            var sample = string.Join(", ", items.Take(5).Select(i => i.Barcode)) + (items.Count > 5 ? ", …" : "");
            var pending = await approvals.SubmitAsync(StoreApprovals.LostMark, Guid.CreateVersion7(), $"{items.Count} bản", $"Báo mất {items.Count} bản ({sample})",
                request with { Reason = reason }, new JsonObject { ["copies"] = items.Count, ["status"] = "R/I" }, new JsonObject { ["copies"] = items.Count, ["status"] = "L" },
                reason, null, items.Count, 0, ct);
            return new ItemBatchOutcome(null, pending);
        }
        return new ItemBatchOutcome(await ApplyMarkLostAsync(items, notFound, request.LostOn, reason, ct), null);
    }

    /// <summary>Chạy lại một lô báo mất đã được duyệt (người duyệt là người thao tác).</summary>
    public async Task ApplyApprovedMarkLostAsync(MarkLostRequest request, string reason, CancellationToken ct)
    {
        var (items, notFound) = await ItemBarcodes.LoadAsync(db, request.Barcodes, ct);
        await ApplyMarkLostAsync(items, notFound, request.LostOn, reason, ct);
    }

    private async Task<ItemBatchResult> ApplyMarkLostAsync(List<Item> items, List<string> notFound, DateOnly? lostOnRequest, string reason, CancellationToken ct)
    {
        var lostOn = lostOnRequest ?? VietnamDate.Of(clock.GetUtcNow());
        var skipped = new List<SkippedItem>();
        var done = 0;
        foreach (var item in items)
        {
            var previous = item.Status;
            var record = LostRecord.For(item, lostOn, reason, previousStatus: previous); // lý do sai → lỗi cả request, không phải lỗi từng bản
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
            await AuditAsync(item, CrudChange.Updated, $"ĐKCB {item.Barcode}: báo mất ({reason})", previous, item.Status, reason, ct);
            done++;
        }
        await db.SaveChangesAsync(ct);
        return new ItemBatchResult(done, notFound, skipped);
    }

    /// <summary>
    /// Tìm thấy lại bản đã báo mất — HOÀN TÁC báo mất: lý do bắt buộc, bản ghi gốc giữ nguyên (ghi ai/khi nào/vì sao hoàn tác), bản sách về
    /// đúng trạng thái trước khi mất. Mặc định phải chờ người có LOST_BOOKS:approve duyệt. Bản mất khi đang mượn thì phát <see cref="ItemLossReversed"/>
    /// để circulation đề xuất điều chỉnh phiếu phạt (không tự hoàn tiền, không tự mở lượt mượn).
    /// </summary>
    public async Task<PendingApproval?> RestoreAsync(ItemReasonRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ChangeReason.Require(request.Reason, auditContext?.Capture().Reason, "khôi phục sách mất");
        var item = await ItemBarcodes.RequireAsync(db, request.Barcode, ct);
        if (item.Status != ItemStatus.Lost) throw new BusinessRuleException("ITEM_NOT_LOST", $"ĐKCB {item.Barcode} không ở trạng thái mất.");
        if (await approvals.RequiresAsync(StoreApprovals.LostRestore, null, null, null, ct))
            return await approvals.SubmitAsync(StoreApprovals.LostRestore, item.PublicId, $"ĐKCB {item.Barcode}", $"Khôi phục ĐKCB {item.Barcode} (tìm thấy lại)",
                request with { Reason = reason }, new JsonObject { ["status"] = ItemStatus.Lost }, new JsonObject { ["status"] = "R/I" }, reason, null, null, item.Version, ct);
        await ApplyRestoreAsync(item, reason, null, ct);
        return null;
    }

    public async Task ApplyApprovedRestoreAsync(string? barcode, string reason, Guid requestId, CancellationToken ct) =>
        await ApplyRestoreAsync(await ItemBarcodes.RequireAsync(db, barcode, ct), reason, requestId, ct);

    private async Task ApplyRestoreAsync(Item item, string reason, Guid? requestId, CancellationToken ct)
    {
        var records = await db.Set<LostRecord>().Where(x => x.ItemId == item.Id && x.ReversedAt == null).OrderByDescending(x => x.Id).ToListAsync(ct);
        var before = item.Status;
        item.RestoreFound(records.FirstOrDefault()?.PreviousStatus);
        var now = clock.GetUtcNow();
        foreach (var record in records) record.Reverse(now, auditContext?.Capture().ActorName, reason, requestId);
        await events.PublishAsync(item, deleted: false, ct);
        var loanLoss = records.FirstOrDefault(r => r.CardNo is not null);
        if (loanLoss is not null)
            await publisher.Publish(new ItemLossReversed
            {
                TenantId = tenant.RequireTenantId(), Actor = new EventActor(actor.Id, actor.Kind), ItemPublicId = item.PublicId, Barcode = item.Barcode,
                CardNo = loanLoss.CardNo, Reason = reason, ActorName = auditContext?.Capture().ActorName,
            }, ct);
        await AuditAsync(item, CrudChange.Reversed, $"ĐKCB {item.Barcode}: khôi phục mất (tìm thấy)", before, item.Status, reason, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<long?> VersionAsync(Guid itemPublicId, CancellationToken ct) =>
        db.Set<Item>().AsNoTracking().Where(x => x.PublicId == itemPublicId).Select(x => (long?)x.Version).FirstOrDefaultAsync(ct);

    private IQueryable<LostRow> Rows(StoreRecordSearch s)
    {
        HoldingsExcel.EnsureRange(s.From, s.To);
        var records = db.Set<LostRecord>().AsNoTracking().Where(x => x.ReversedAt == null);
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

    private Task AuditAsync(Item item, CrudChange change, string label, string? from, string? to, string reason, CancellationToken ct) => audit is null
        ? Task.CompletedTask
        : audit.RecordAsync(new CrudAuditEntry(nameof(Item), EntityName, item.PublicId, change, label,
            new JsonObject { ["status"] = new JsonArray(from, to) }, reason), ct);
}

/// <summary>
/// Thanh lý (monolith: StoreLiquidateController + LiquidateService, quyền LIQUIDATES): thanh lý theo lô ĐKCB (monolith từng mã một),
/// huỷ thanh lý, danh sách + xuất Excel. Chặn bản đang mượn, đang xuất kho; bản đã báo mất thanh lý được (huỷ thì về lại "Mất").
/// </summary>
public sealed class Liquidations(
    ICrudDbContext db, ItemEvents events, HoldingsExcel excel, TimeProvider clock, ApprovalService approvals, CrudAuditContext? auditContext = null,
    ICrudAuditSink? audit = null)
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
            ["Đăng ký cá biệt", "Nhan đề", "Tác giả", "Nhà xuất bản", "Kho", "Ngày thanh lý", "Lý do thanh lý", "Số quyết định", "Ngày quyết định"],
            rows.Select(r => (IReadOnlyList<object?>)[r.Barcode, r.Title, r.Author, r.Publisher, r.StoreName, r.LiquidatedOn, r.Reason, r.DecisionNo, r.DecisionDate])));
    }

    /// <summary>
    /// Thanh lý theo lô: lý do BẮT BUỘC, ghi số/ngày quyết định; mọi lần thanh lý mặc định phải chờ người có LIQUIDATES:approve duyệt (đơn vị
    /// chỉnh được ở "Thẩm quyền phê duyệt"). Chặn bản đang mượn, đang xuất kho; bản đã báo mất thanh lý được (huỷ thì về lại đúng trạng thái trước).
    /// </summary>
    public async Task<ItemBatchOutcome> LiquidateAsync(LiquidateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ChangeReason.Require(request.Reason, auditContext?.Capture().Reason, "thanh lý");
        var (items, notFound) = await ItemBarcodes.LoadAsync(db, request.Barcodes, ct);
        if (items.Count > 0 && await approvals.RequiresAsync(StoreApprovals.Liquidate, null, null, items.Count, ct))
        {
            var sample = string.Join(", ", items.Take(5).Select(i => i.Barcode)) + (items.Count > 5 ? ", …" : "");
            var decision = string.IsNullOrWhiteSpace(request.DecisionNo) ? "" : $", QĐ {request.DecisionNo.Trim()}";
            var pending = await approvals.SubmitAsync(StoreApprovals.Liquidate, Guid.CreateVersion7(), $"{items.Count} bản", $"Thanh lý {items.Count} bản ({sample}){decision}",
                request with { Reason = reason }, new JsonObject { ["copies"] = items.Count, ["status"] = "R/I/L" }, new JsonObject { ["copies"] = items.Count, ["status"] = "S" },
                reason, null, items.Count, 0, ct);
            return new ItemBatchOutcome(null, pending);
        }
        return new ItemBatchOutcome(await ApplyLiquidateAsync(items, notFound, request, reason, ct), null);
    }

    public async Task ApplyApprovedLiquidateAsync(LiquidateRequest request, string reason, CancellationToken ct)
    {
        var (items, notFound) = await ItemBarcodes.LoadAsync(db, request.Barcodes, ct);
        await ApplyLiquidateAsync(items, notFound, request, reason, ct);
    }

    private async Task<ItemBatchResult> ApplyLiquidateAsync(List<Item> items, List<string> notFound, LiquidateRequest request, string reason, CancellationToken ct)
    {
        var on = request.LiquidatedOn ?? VietnamDate.Of(clock.GetUtcNow());
        var skipped = new List<SkippedItem>();
        var done = 0;
        foreach (var item in items)
        {
            var previous = item.Status;
            var record = Liquidation.For(item, on, reason, request.DecisionNo, request.DecisionDate, previous);
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
            await AuditAsync(item, CrudChange.Updated, $"ĐKCB {item.Barcode}: thanh lý ({reason})" + (record.DecisionNo is { } d ? $", QĐ {d}" : ""), previous, item.Status, reason, ct);
            done++;
        }
        await db.SaveChangesAsync(ct);
        return new ItemBatchResult(done, notFound, skipped);
    }

    /// <summary>Huỷ thanh lý — HOÀN TÁC thanh lý: lý do bắt buộc, giữ bản ghi gốc, bản sách về đúng trạng thái trước khi thanh lý. Chờ duyệt theo thẩm quyền.</summary>
    public async Task<PendingApproval?> CancelAsync(ItemReasonRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ChangeReason.Require(request.Reason, auditContext?.Capture().Reason, "huỷ thanh lý");
        var item = await ItemBarcodes.RequireAsync(db, request.Barcode, ct);
        if (item.Status != ItemStatus.Liquidated) throw new BusinessRuleException("ITEM_NOT_LIQUIDATED", $"ĐKCB {item.Barcode} không ở trạng thái thanh lý.");
        if (await approvals.RequiresAsync(StoreApprovals.LiquidateCancel, null, null, null, ct))
            return await approvals.SubmitAsync(StoreApprovals.LiquidateCancel, item.PublicId, $"ĐKCB {item.Barcode}", $"Huỷ thanh lý ĐKCB {item.Barcode}",
                request with { Reason = reason }, new JsonObject { ["status"] = ItemStatus.Liquidated }, new JsonObject { ["status"] = "R/I/L" }, reason, null, null, item.Version, ct);
        await ApplyCancelAsync(item, reason, null, ct);
        return null;
    }

    public async Task ApplyApprovedCancelAsync(string? barcode, string reason, Guid requestId, CancellationToken ct) =>
        await ApplyCancelAsync(await ItemBarcodes.RequireAsync(db, barcode, ct), reason, requestId, ct);

    private async Task ApplyCancelAsync(Item item, string reason, Guid? requestId, CancellationToken ct)
    {
        var records = await db.Set<Liquidation>().Where(x => x.ItemId == item.Id && x.ReversedAt == null).OrderByDescending(x => x.Id).ToListAsync(ct);
        var before = item.Status;
        item.CancelLiquidation(stillLost: await db.Set<LostRecord>().AnyAsync(x => x.ItemId == item.Id && x.ReversedAt == null, ct), records.FirstOrDefault()?.PreviousStatus);
        var now = clock.GetUtcNow();
        foreach (var record in records) record.Reverse(now, auditContext?.Capture().ActorName, reason, requestId);
        await events.PublishAsync(item, deleted: false, ct);
        await AuditAsync(item, CrudChange.Reversed, $"ĐKCB {item.Barcode}: huỷ thanh lý", before, item.Status, reason, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<long?> VersionAsync(Guid itemPublicId, CancellationToken ct) =>
        db.Set<Item>().AsNoTracking().Where(x => x.PublicId == itemPublicId).Select(x => (long?)x.Version).FirstOrDefaultAsync(ct);

    private IQueryable<LiquidationRow> Rows(StoreRecordSearch s)
    {
        HoldingsExcel.EnsureRange(s.From, s.To);
        var records = db.Set<Liquidation>().AsNoTracking().Where(x => x.ReversedAt == null);
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
            x.r.Reason, x.i == null ? null : x.i.Status, x.r.CreatedAt, x.r.DecisionNo, x.r.DecisionDate));
    }

    private Task AuditAsync(Item item, CrudChange change, string label, string? from, string? to, string reason, CancellationToken ct) => audit is null
        ? Task.CompletedTask
        : audit.RecordAsync(new CrudAuditEntry(nameof(Item), EntityName, item.PublicId, change, label,
            new JsonObject { ["status"] = new JsonArray(from, to) }, reason), ct);
}

/// <summary>Áp dụng yêu cầu kho đã duyệt (báo mất lô, khôi phục, thanh lý, huỷ thanh lý) dưới danh nghĩa người duyệt.</summary>
public sealed class StoreRecordChangeHandler(LostBooks lost, Liquidations liquidations) : IChangeRequestHandler
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    public bool CanHandle(string kind) => kind is StoreApprovals.LostMark or StoreApprovals.LostRestore or StoreApprovals.Liquidate or StoreApprovals.LiquidateCancel;

    public Task<long?> CurrentVersionAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Lô nhiều bản không có một bản ghi đích duy nhất (phiên bản 0); khôi phục/huỷ thanh lý so phiên bản bản sách lúc đề nghị.
        return request.Kind switch
        {
            StoreApprovals.LostRestore => lost.VersionAsync(request.TargetPublicId, cancellationToken),
            StoreApprovals.LiquidateCancel => liquidations.VersionAsync(request.TargetPublicId, cancellationToken),
            _ => Task.FromResult<long?>(0),
        };
    }

    public async Task ApplyAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Kind)
        {
            case StoreApprovals.LostMark:
                await lost.ApplyApprovedMarkLostAsync(System.Text.Json.JsonSerializer.Deserialize<MarkLostRequest>(request.PayloadJson, Json)!, request.Reason, cancellationToken);
                break;
            case StoreApprovals.LostRestore:
                await lost.ApplyApprovedRestoreAsync(System.Text.Json.JsonSerializer.Deserialize<ItemReasonRequest>(request.PayloadJson, Json)!.Barcode, request.Reason, request.PublicId, cancellationToken);
                break;
            case StoreApprovals.Liquidate:
                await liquidations.ApplyApprovedLiquidateAsync(System.Text.Json.JsonSerializer.Deserialize<LiquidateRequest>(request.PayloadJson, Json)!, request.Reason, cancellationToken);
                break;
            default:
                await liquidations.ApplyApprovedCancelAsync(System.Text.Json.JsonSerializer.Deserialize<ItemReasonRequest>(request.PayloadJson, Json)!.Barcode, request.Reason, request.PublicId, cancellationToken);
                break;
        }
    }
}
