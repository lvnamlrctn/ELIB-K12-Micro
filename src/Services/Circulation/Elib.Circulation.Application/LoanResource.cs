using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Circulation;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>
/// Lịch sử lưu thông (monolith: CirculationHistory/Search, quyền LOAN_HISTORY). <see cref="State"/>: open (chưa trả, kể cả quá hạn),
/// borrowing (đang mượn, chưa quá hạn), overdue (đang mượn quá hạn), returned (đã trả), returned_ontime (trả đúng hạn),
/// returned_late (trả quá hạn); trống = tất cả — 4 trạng thái như ELIB-LRC. From/To lọc theo ngày mượn (giờ Việt Nam);
/// <see cref="StoreId"/> lọc theo kho của bản sách lúc mượn.
/// </summary>
public sealed class LoanSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public string? Barcode { get; set; }
    public string? State { get; set; }
    public long? CircPlaceId { get; set; }
    public long? StoreId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record LoanDto(
    long Id, Guid PublicId, Guid ReaderPublicId, string CardNo, string? ReaderName, Guid ItemPublicId, string Barcode, long Mfn,
    string? Title, string? Author, long? CircPlaceId, string? StoreName, DateTimeOffset LoanedAt, DateTimeOffset DueAt,
    DateTimeOffset? ReturnedAt, int RenewCount, string? Note, long Version,
    long? StoreId = null, long? ReturnCircPlaceId = null, string? LoanedByName = null, string? ReturnedByName = null, bool ReturnedLate = false,
    string? CloseKind = null, string? CloseReason = null, int ReopenCount = 0)
{
    /// <summary>Bạn đọc tự xem (OPAC) — không lộ tên cán bộ thao tác.</summary>
    public LoanDto ForReader() => this with { LoanedByName = null, ReturnedByName = null, CloseReason = null };
}

/// <summary>Bạn đọc ở quầy (monolith: ReaderSnapshot) — thông tin thẻ, lý do không được mượn, các lượt đang mượn, chính sách áp dụng.</summary>
public sealed record ReaderPanel(
    Guid ReaderPublicId, string CardNo, string FullName, string? ReaderTypeName, string? ClassName, string? CourseName, Guid? PhotoId,
    DateOnly? ExpireDate, bool IsLocked, bool IsExpired, bool HasOverdue, bool CanBorrow, string? BlockReason,
    int LoanDays, int? MaxLoans, int? MaxRenewals, IReadOnlyList<LoanDto> CurrentLoans, decimal UnpaidFines = 0, IReadOnlyList<HoldDto>? Holds = null);

public sealed record ReaderPanelRequest(string CardNo, long? CircPlaceId = null);

/// <summary>Mượn một hoặc nhiều ĐKCB cho bạn đọc tại điểm lưu thông (monolith: Checkout, Checkout/Bulk).</summary>
public sealed record CheckoutRequest(string CardNo, IReadOnlyList<string> Barcodes, long CircPlaceId);

public sealed record CheckoutLine(string Barcode, bool Success, string Message, LoanDto? Loan);

public sealed record CheckoutResult(int Succeeded, int Failed, IReadOnlyList<CheckoutLine> Lines);

/// <summary>Trả theo số ĐKCB quét được hoặc theo lượt mượn (monolith: Return).</summary>
public sealed record ReturnRequest(string? Barcode = null, Guid? LoanId = null, long? CircPlaceId = null);

/// <summary><see cref="HoldFor"/>: bản vừa trả được giữ cho người đặt mượn kế tiếp — cán bộ để riêng, không xếp lên giá.</summary>
public sealed record ReturnResult(LoanDto Loan, int OverdueDays, HoldDto? HoldFor = null);

/// <summary>Gia hạn (monolith: Renew) — bắt buộc lý do như monolith.</summary>
public sealed record RenewRequest(Guid LoanId, string Reason);

public sealed record LoanNoteRequest(Guid LoanId, string? Note, string Reason);

/// <summary>Đóng cưỡng bức / mở lại một lượt mượn — lý do bắt buộc; vượt thẩm quyền thì chờ duyệt (BORROW:approve).</summary>
public sealed record LoanActionRequest(Guid LoanId, string Reason);

/// <summary>Đã áp dụng (<see cref="Loan"/>) hoặc chờ duyệt (<see cref="Pending"/>, HTTP 202, chưa đổi gì).</summary>
public sealed record LoanActionOutcome(LoanDto? Loan, PendingApproval? Pending);

/// <summary>Các thao tác lượt mượn cân nhắc phê duyệt (quyền BORROW:approve; mặc định luôn cần duyệt, đơn vị chỉnh được).</summary>
public static class LoanApprovals
{
    public const string ForceClose = "LOAN_FORCE_CLOSE";
    public const string Reopen = "LOAN_REOPEN";

    /// <summary>Đóng lượt mượn vì mất tài liệu qua dòng phạt "Mất" của phiếu phạt (ngưỡng theo số lượt đóng một lần).</summary>
    public const string CloseLost = "LOAN_CLOSE_LOST";

    public static readonly ApprovalKind[] Kinds =
    [
        new(ForceClose, "BORROW", nameof(Loan), "Lượt mượn", "Đóng cưỡng bức lượt mượn", "Đóng lượt mượn không có sách trả về (bạn đọc đã rời trường, mất dấu…)."),
        new(Reopen, "BORROW", nameof(Loan), "Lượt mượn", "Mở lại lượt mượn đã đóng", "Hoàn tác trả nhầm / đóng nhầm: lượt mượn trở lại đang mở."),
        new(CloseLost, "BORROW", nameof(FineTicket), "Phiếu phạt", "Đóng lượt mượn vì mất tài liệu", "Dòng phạt \"Mất tài liệu\" của phiếu phạt đóng lượt mượn và báo kho đổi bản sách sang Mất."),
    ];
}

/// <summary>
/// Lượt mượn (monolith: CirculationLoanController + CirculationHistoryController, quyền BORROW / LOAN_HISTORY). Đọc bản sao bạn đọc,
/// bản sách, biểu ghi trong cùng DB (docs 03: mượn vẫn chạy khi patron/holdings/catalog down). Mọi thay đổi phát
/// <see cref="LoanChanged"/> (outbox, cùng transaction).
/// </summary>
public sealed class LoanResource(
    ICrudDbContext db, Replicas replicas, LoanPublisher publisher, HoldAllocator holds, HoldResource holdList, ApprovalService approvals, ICurrentActor actor,
    TimeProvider clock, ICirculationSettings settings, CrudAuditContext? auditContext = null)
    : CrudResource<LoanResource, Loan, LoanSearch, LoanNoteRequest, LoanDto>(db)
{
    public const int MaxCheckout = 200;
    private const int LoanListLimit = 500;

    protected override string EntityName => "Lượt mượn";

    protected override string? Describe(Loan entity) => $"ĐKCB {entity.Barcode} — thẻ {entity.CardNo}";

    protected override Expression<Func<Loan, LoanDto>> Projection => x => new LoanDto(
        x.Id, x.PublicId, x.ReaderPublicId, x.CardNo,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.ItemPublicId, x.Barcode, x.Mfn,
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.Mfn).Select(b => b.Title).FirstOrDefault(),
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.Mfn).Select(b => b.Author).FirstOrDefault(),
        x.CircPlaceId,
        Db.Set<ItemReplica>().Where(i => i.ItemPublicId == x.ItemPublicId).Select(i => i.StoreName).FirstOrDefault(),
        x.LoanedAt, x.DueAt, x.ReturnedAt, x.RenewCount, x.Note, x.Version,
        x.StoreId, x.ReturnCircPlaceId, x.LoanedByName, x.ReturnedByName, x.ReturnedLate, x.CloseKind, x.CloseReason, x.ReopenCount);

    protected override Loan Create(LoanNoteRequest request) => throw new NotSupportedException("Lượt mượn tạo qua CheckoutAsync.");

    protected override void Update(Loan entity, LoanNoteRequest request) => throw new NotSupportedException("Lượt mượn sửa qua Return/Renew/Note.");

    protected override IQueryable<Loan> Filter(IQueryable<Loan> query, LoanSearch s)
    {
        if (!string.IsNullOrWhiteSpace(s.CardNo))
        {
            var card = PatronReplica.Key(s.CardNo);
            query = query.Where(x => x.CardNo == card);
        }
        if (!string.IsNullOrWhiteSpace(s.Barcode))
        {
            var key = ItemReplica.Key(s.Barcode);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToUpper() sang upper() của SQL
            query = query.Where(x => x.Barcode.ToUpper() == key);
#pragma warning restore CA1862, CA1304, CA1311
        }
        if (s.CircPlaceId is { } place) query = query.Where(x => x.CircPlaceId == place);
        if (s.StoreId is { } store) query = query.Where(x => x.StoreId == store);
        var now = clock.GetUtcNow();
        query = (s.State ?? "").Trim().ToLowerInvariant() switch
        {
            "open" => query.Where(x => x.ReturnedAt == null),
            "borrowing" => query.Where(x => x.ReturnedAt == null && x.DueAt >= now),
            "overdue" => query.Where(x => x.ReturnedAt == null && x.DueAt < now),
            "returned" => query.Where(x => x.ReturnedAt != null),
            "returned_ontime" => query.Where(x => x.ReturnedAt != null && !x.ReturnedLate),
            "returned_late" => query.Where(x => x.ReturnedAt != null && x.ReturnedLate),
            _ => query,
        };
        if (s.From is { } from)
        {
            var start = VietnamStart(from);
            query = query.Where(x => x.LoanedAt >= start);
        }
        if (s.To is { } to)
        {
            var end = VietnamStart(to.AddDays(1));
            query = query.Where(x => x.LoanedAt < end);
        }
        if (s.Term is { } term)
        {
            var key = term.ToUpperInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.CardNo.StartsWith(key) || x.Barcode.ToUpper().StartsWith(key)
                || Db.Set<BibSnapshot>().Any(b => b.Mfn == x.Mfn && b.Title.ToLower().Contains(term))
                || Db.Set<PatronReplica>().Any(r => r.ReaderPublicId == x.ReaderPublicId && r.FullName.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<Loan> Order(IQueryable<Loan> query) => query.OrderByDescending(x => x.LoanedAt).ThenByDescending(x => x.Id);

    /// <summary>Bạn đọc ở quầy: thông tin thẻ, có được mượn không (khoá/hết hạn/quá hạn), lượt đang mượn.</summary>
    public async Task<ReaderPanel> ReaderPanelAsync(ReaderPanelRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reader = await RequireReaderAsync(request.CardNo, ct);
        var policy = await PolicyAsync(reader, request.CircPlaceId, ct);
        var loans = await OpenLoansAsync(reader, ct);
        var now = clock.GetUtcNow();
        var place = request.CircPlaceId is { } placeId ? await Db.Set<CircPlace>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == placeId, ct) : null;
        var block = BlockReason(reader, loans.Any(l => l.DueAt < now)) ?? (place is null ? null : NotServedReason(reader, place));
        var unpaid = (await Db.Set<FineTicket>().AsNoTracking().Where(t => t.ReaderPublicId == reader.ReaderPublicId)
            .Select(t => t.Remaining).ToListAsync(ct)).Where(r => r > 0).Sum();
        return new ReaderPanel(reader.ReaderPublicId, reader.CardNo, reader.FullName, reader.ReaderTypeName, reader.ClassName, reader.CourseName,
            reader.PhotoId, reader.ExpireDate, reader.Status != IHasStatus.Active, IsExpired(reader), loans.Any(l => l.DueAt < now),
            block is null, block, place?.IsReadingRoom == true ? 0 : policy.LoanDays, policy.MaxLoans, policy.MaxRenewals, loans, unpaid,
            await holdList.ActiveForAsync(reader.ReaderPublicId, ct));
    }

    /// <summary>
    /// Mượn (monolith: Checkout/Bulk). Kiểm tra bạn đọc một lần (khoá, hết hạn thẻ, đang có tài liệu quá hạn) rồi từng ĐKCB
    /// (có, sẵn sàng, chưa ai mượn, thuộc kho của điểm lưu thông, chưa vượt số tài liệu được mượn). ĐKCB lỗi không chặn ĐKCB khác;
    /// lưu một lần.
    /// </summary>
    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var barcodes = (request.Barcodes ?? []).Select(b => (b ?? "").Trim()).Where(b => b.Length > 0)
            .DistinctBy(ItemReplica.Key).ToList();
        if (barcodes.Count is 0 or > MaxCheckout)
            throw new BusinessRuleException("CHECKOUT_EMPTY", $"Nhập từ 1 đến {MaxCheckout} số ĐKCB mỗi lần mượn.");
        var place = await Db.Set<CircPlace>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.CircPlaceId, ct)
            ?? throw new BusinessRuleException("CIRC_PLACE_REQUIRED", "Chọn điểm lưu thông.");
        var reader = await RequireReaderAsync(request.CardNo, ct);
        var now = clock.GetUtcNow();
        var open = await Set.Where(x => x.ReaderPublicId == reader.ReaderPublicId && x.ReturnedAt == null).Select(x => x.DueAt).ToListAsync(ct);
        if (BlockReason(reader, open.Any(d => d < now)) is { } block)
            throw new BusinessRuleException("READER_CANNOT_BORROW", block);
        if (NotServedReason(reader, place) is { } notServed)
            throw new BusinessRuleException("READER_TYPE_NOT_SERVED", notServed);
        var policy = await PolicyAsync(reader, place.Id, ct);
        var typeCounts = await OpenTypeCountsAsync(reader, policy, ct);
        var atPlace = place.MaxLoans is null ? 0 : await Set.CountAsync(x => x.ReaderPublicId == reader.ReaderPublicId && x.ReturnedAt == null && x.CircPlaceId == place.Id, ct);

        var lines = new List<CheckoutLine>();
        var created = new List<Loan>();
        foreach (var barcode in barcodes)
        {
            if (place.MaxLoans is { } placeMax && atPlace + created.Count >= placeMax)
            {
                lines.Add(new CheckoutLine(barcode, false, placeMax == 0
                    ? $"Điểm {place.Name} không cho mượn tài liệu."
                    : $"Bạn đọc đã mượn đủ {placeMax} tài liệu tại {place.Name}.", null));
                continue;
            }
            var (loan, message) = await TryCheckoutAsync(reader, barcode, place, policy, open.Count + created.Count, typeCounts, now, ct);
            if (loan is not null) created.Add(loan);
            lines.Add(new CheckoutLine(barcode, loan is not null, message, null));
        }
        if (created.Count > 0)
        {
            if (AuditSink is not null)
                await AuditSink.RecordAsync(new CrudAuditEntry(nameof(Loan), EntityName, reader.ReaderPublicId, CrudChange.Added,
                    $"Mượn {created.Count} tài liệu ({string.Join(", ", created.Select(l => l.Barcode))}) — thẻ {reader.CardNo}"), ct);
            await Db.SaveChangesAsync(ct);
        }

        var dtos = await ToDtosAsync(created, ct);
        lines = [.. lines.Select(l => l.Success ? l with { Loan = dtos.FirstOrDefault(d => ItemReplica.Key(d.Barcode) == ItemReplica.Key(l.Barcode)) } : l)];
        return new CheckoutResult(created.Count, lines.Count - created.Count, lines);
    }

    /// <summary>Trả (monolith: Return) theo ĐKCB quét được hoặc theo lượt mượn.</summary>
    public async Task<ReturnResult> ReturnAsync(ReturnRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        Loan? loan;
        if (request.LoanId is { } id)
        {
            loan = await Set.FirstOrDefaultAsync(x => x.PublicId == id, ct) ?? throw new NotFoundException(EntityName, id);
        }
        else
        {
            var key = ItemReplica.Key(request.Barcode ?? "");
            if (key.Length == 0) throw new BusinessRuleException("BARCODE_REQUIRED", "Nhập hoặc quét số ĐKCB cần trả.");
#pragma warning disable CA1862, CA1304, CA1311
            loan = await Set.FirstOrDefaultAsync(x => x.ReturnedAt == null && x.Barcode.ToUpper() == key, ct)
                ?? throw new BusinessRuleException("LOAN_NOT_FOUND", $"ĐKCB {request.Barcode!.Trim()} không có lượt mượn nào đang mở.", 404);
#pragma warning restore CA1862, CA1304, CA1311
        }
        var now = clock.GetUtcNow();
        loan.Return(now, request.CircPlaceId, actor.Id, StaffName());
        await PublishAsync(loan, ct);
        var item = await Db.Set<ItemReplica>().FirstOrDefaultAsync(i => i.ItemPublicId == loan.ItemPublicId, ct);
        var next = item is null ? null : await holds.AssignNextAsync(item, ct, returningLoan: loan.PublicId);
        var method = (await settings.GetAsync(ct)).OverdueMethod;
        await Record(loan, CrudChange.Updated, $"Trả ĐKCB {loan.Barcode} — thẻ {loan.CardNo}" + (loan.OverdueDays(now, method) is > 0 and var d ? $", quá hạn {d} ngày" : "")
            + (next is null ? "" : $"; giữ cho đặt mượn thẻ {next.CardNo}"), ct);
        await Db.SaveChangesAsync(ct);
        return new ReturnResult((await ToDtosAsync([loan], ct))[0], loan.OverdueDays(now, method), next is null ? null : await holdList.GetAsync(next.Id, ct));
    }

    /// <summary>
    /// Gia hạn (monolith: Renew): theo chính sách của bạn đọc tại điểm mượn; lý do bắt buộc, ghi vào nhật ký.
    /// <paramref name="requireOwnerReaderId"/>: bạn đọc tự gia hạn qua OPAC — lượt mượn không phải của mình thì coi như không có (404),
    /// không lộ việc lượt mượn đó tồn tại.
    /// </summary>
    public async Task<LoanDto> RenewAsync(RenewRequest request, CancellationToken ct, Guid? requireOwnerReaderId = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireReason(request.Reason, "gia hạn");
        var loan = await Set.FirstOrDefaultAsync(x => x.PublicId == request.LoanId, ct) ?? throw new NotFoundException(EntityName, request.LoanId);
        if (requireOwnerReaderId is { } owner && loan.ReaderPublicId != owner) throw new NotFoundException(EntityName, request.LoanId);
        if (loan.CircPlaceId is { } placeId && await Db.Set<CircPlace>().AsNoTracking().AnyAsync(p => p.Id == placeId && p.Kind == CircPlace.KindReadingRoom, ct))
            throw new BusinessRuleException("RENEW_READING_ROOM", "Tài liệu mượn đọc tại chỗ không gia hạn được.");
        var reader = await Db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == loan.ReaderPublicId, ct);
        var policy = await PolicyAsync(reader, loan.CircPlaceId, ct);
        var oldDue = loan.DueAt;
        var now = clock.GetUtcNow();
        long? bibType = policy.BibTypeRules.Count == 0 ? null : (await replicas.BibTypesAsync([loan.Mfn], ct)).GetValueOrDefault(loan.Mfn);
        loan.Renew(now, policy, bibType, (await settings.GetAsync(ct)).RenewFromToday);
        Db.Set<LoanRenewal>().Add(new LoanRenewal
        {
            LoanPublicId = loan.PublicId, ReaderPublicId = loan.ReaderPublicId, CircPlaceId = loan.CircPlaceId, RenewedAt = now,
            OldDueAt = oldDue, NewDueAt = loan.DueAt, Reason = reason, RenewedBy = actor.Id,
        });
        await PublishAsync(loan, ct);
        await Record(loan, CrudChange.Updated,
            $"Gia hạn ĐKCB {loan.Barcode} — thẻ {loan.CardNo}: hạn {Loan.LocalDate(oldDue):dd/MM/yyyy} → {Loan.LocalDate(loan.DueAt):dd/MM/yyyy}. Lý do: {reason}", ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    /// <summary>Sửa ghi chú lượt mượn (monolith: Note) — lý do bắt buộc.</summary>
    public async Task<LoanDto> NoteAsync(LoanNoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireReason(request.Reason, "sửa ghi chú");
        var loan = await Set.FirstOrDefaultAsync(x => x.PublicId == request.LoanId, ct) ?? throw new NotFoundException(EntityName, request.LoanId);
        var old = loan.Note;
        loan.SetNote(request.Note);
        await PublishAsync(loan, ct);
        await Record(loan, CrudChange.Updated, $"Sửa ghi chú ĐKCB {loan.Barcode}: \"{old}\" → \"{loan.Note}\". Lý do: {reason}", ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    public const int MaxStatePage = 2000;

    /// <summary>Lượt đang mượn theo id tăng dần sau <paramref name="afterId"/> — search dựng chỉ mục lần đầu / dựng lại.</summary>
    public async Task<(IReadOnlyList<LoanChanged> Items, long? Next)> OpenStatesAsync(long afterId, int take, CancellationToken ct)
    {
        var size = Math.Clamp(take, 1, MaxStatePage);
        var loans = await Set.AsNoTracking().Where(x => x.ReturnedAt == null && x.Id > afterId).OrderBy(x => x.Id).Take(size).ToListAsync(ct);
        return (loans.Select(publisher.ToEvent).ToList(), loans.Count == size ? loans[^1].Id : null);
    }

    /// <summary>Xuất Excel lịch sử lưu thông theo bộ lọc đang xem (monolith: CirculationHistory/Export), tối đa <see cref="CrudExcel.MaxExportRows"/> dòng.</summary>
    public async Task<byte[]> ExportAsync(LoanSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var rows = await Query(search).Take(CrudExcel.MaxExportRows + 1).Select(Projection).ToListAsync(ct);
        if (rows.Count > CrudExcel.MaxExportRows)
            throw new BusinessRuleException("EXPORT_TOO_LARGE", $"Kết quả quá {CrudExcel.MaxExportRows:N0} lượt mượn — thu hẹp khoảng ngày hoặc bộ lọc.");
        var now = clock.GetUtcNow();
        static string State(LoanDto l, DateTimeOffset now) =>
            l.ReturnedAt is not null ? l.ReturnedLate ? "Trả quá hạn" : "Trả đúng hạn" : l.DueAt < now ? "Quá hạn" : "Đang mượn";
        return CrudExcel.Export<LoanDto>("Lịch sử lưu thông",
        [
            new("cardNo", "Số thẻ", l => l.CardNo),
            new("readerName", "Họ tên", l => l.ReaderName),
            new("barcode", "Số ĐKCB", l => l.Barcode),
            new("title", "Nhan đề", l => l.Title),
            new("author", "Tác giả", l => l.Author),
            new("storeName", "Kho", l => l.StoreName),
            new("loanedAt", "Ngày mượn", l => Loan.LocalDate(l.LoanedAt)),
            new("loanedBy", "Người cho mượn", l => l.LoanedByName),
            new("dueAt", "Hạn trả", l => Loan.LocalDate(l.DueAt)),
            new("returnedAt", "Ngày trả", l => l.ReturnedAt is { } r ? Loan.LocalDate(r) : null),
            new("returnedBy", "Người nhận trả", l => l.ReturnedByName),
            new("renewCount", "Số lần gia hạn", l => l.RenewCount),
            new("state", "Tình trạng", l => State(l, now)),
            new("note", "Ghi chú", l => l.Note),
        ], rows);
    }

    /// <summary>Số tài liệu đang mượn theo nhóm (loại biểu ghi) — chỉ khi chính sách có luật nhóm.</summary>
    private async Task<Dictionary<long, int>> OpenTypeCountsAsync(PatronReplica reader, LoanPolicy policy, CancellationToken ct)
    {
        if (policy.BibTypeRules.Count == 0) return [];
        var mfns = await Set.Where(x => x.ReaderPublicId == reader.ReaderPublicId && x.ReturnedAt == null).Select(x => x.Mfn).ToListAsync(ct);
        var types = await replicas.BibTypesAsync(mfns, ct);
        return mfns.Select(m => types.GetValueOrDefault(m)).OfType<long>().GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<(Loan? Loan, string Message)> TryCheckoutAsync(
        PatronReplica reader, string barcode, CircPlace place, LoanPolicy policy, int openCount, Dictionary<long, int> typeCounts, DateTimeOffset now,
        CancellationToken ct)
    {
        var item = await replicas.ItemAsync(barcode, ct);
        if (item is null) return (null, "Mã ĐKCB không tồn tại.");
        if (item.Status != ItemReplica.Available)
            return (null, item.Status switch
            {
                "I" => "Tài liệu chưa xếp giá, chưa cho mượn.",
                "L" => "Tài liệu đã báo mất.",
                "S" => "Tài liệu đã thanh lý.",
                "X" => "Tài liệu đã xuất kho.",
                _ => "Tài liệu không sẵn sàng cho mượn.",
            });
        if (await Set.AsNoTracking().Where(x => x.ItemPublicId == item.ItemPublicId && x.ReturnedAt == null).Select(x => x.CardNo).FirstOrDefaultAsync(ct) is { } holder)
            return (null, holder == reader.CardNo ? "Bạn đọc đang mượn tài liệu này." : "Tài liệu đang được mượn.");
        if (!place.Allows(item.StoreId))
            return (null, $"Tài liệu thuộc kho {item.StoreName ?? "khác"}, không mượn tại {place.Name}.");
        if (policy.MaxLoans is { } max && openCount >= max)
            return (null, $"Bạn đọc đã mượn đủ {max} tài liệu theo chính sách.");
        if (await holds.HeldAsync(item.ItemPublicId, ct) is { } held && held.ReaderPublicId != reader.ReaderPublicId)
            return (null, $"Tài liệu đang giữ cho bạn đọc đặt mượn (thẻ {held.CardNo}) đến {ReaderNotifier.Date(held.ExpiresAt!.Value)}.");
        long? bibType = policy.BibTypeRules.Count == 0 ? null : (await replicas.BibTypesAsync([item.Mfn], ct)).GetValueOrDefault(item.Mfn);
        if (bibType is { } type && policy.RuleFor(type)?.MaxLoans is { } typeMax && typeCounts.GetValueOrDefault(type) >= typeMax)
            return (null, typeMax == 0
                ? "Chính sách không cho mượn nhóm tài liệu này."
                : $"Bạn đọc đã mượn đủ {typeMax} tài liệu thuộc nhóm này theo chính sách.");

        var loan = place.IsReadingRoom
            ? Loan.OpenReadingRoom(reader, item, place.Id, now, actor.Id, StaffName())
            : Loan.Open(reader, item, place.Id, now, policy.LoanDaysFor(bibType), actor.Id, StaffName());
        if (bibType is { } counted) typeCounts[counted] = typeCounts.GetValueOrDefault(counted) + 1;
        Set.Add(loan);
        await holds.FulfilAsync(reader, item, ct);
        await PublishAsync(loan, ct);
        return (loan, $"Mượn thành công, hạn trả {Loan.LocalDate(loan.DueAt):dd/MM/yyyy}.");
    }

    private async Task<PatronReplica> RequireReaderAsync(string? cardNo, CancellationToken ct) =>
        await replicas.ReaderAsync(cardNo ?? "", ct)
        ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{cardNo?.Trim()}'.", 404);

    private string? BlockReason(PatronReplica reader, bool hasOverdue) =>
        reader.Status != IHasStatus.Active ? "Thẻ bạn đọc đang bị khoá."
        : IsExpired(reader) ? $"Thẻ bạn đọc đã hết hạn ngày {reader.ExpireDate:dd/MM/yyyy}."
        : hasOverdue ? "Bạn đọc đang có tài liệu quá hạn, phải trả trước khi mượn thêm."
        : null;

    private static string? NotServedReason(PatronReplica reader, CircPlace place) =>
        place.Serves(reader.ReaderTypeId) ? null
        : $"{(reader.ReaderTypeName is { Length: > 0 } type ? $"Loại bạn đọc {type}" : "Bạn đọc chưa có loại bạn đọc nên")} không được phục vụ tại {place.Name}.";

    private bool IsExpired(PatronReplica reader) => reader.ExpireDate is { } expire && expire < Loan.LocalDate(clock.GetUtcNow());

    private async Task<LoanPolicy> PolicyAsync(PatronReplica? reader, long? circPlaceId, CancellationToken ct) =>
        LoanPolicyResource.Resolve(await Db.Set<LoanPolicy>().AsNoTracking().ToListAsync(ct), reader?.ReaderTypeId, circPlaceId);

    private async Task<IReadOnlyList<LoanDto>> OpenLoansAsync(PatronReplica reader, CancellationToken ct)
    {
        var mfns = await Set.Where(x => x.ReaderPublicId == reader.ReaderPublicId && x.ReturnedAt == null).Select(x => x.Mfn).ToListAsync(ct);
        await EnsureTitlesAsync(mfns, ct);
        return await Set.AsNoTracking().Where(x => x.ReaderPublicId == reader.ReaderPublicId && x.ReturnedAt == null)
            .OrderBy(x => x.DueAt).Take(LoanListLimit).Select(Projection).ToListAsync(ct);
    }

    private async Task<IReadOnlyList<LoanDto>> ToDtosAsync(List<Loan> loans, CancellationToken ct)
    {
        if (loans.Count == 0) return [];
        await EnsureTitlesAsync(loans.Select(l => l.Mfn), ct);
        var ids = loans.Select(l => l.Id).ToList();
        return await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).Select(Projection).ToListAsync(ct);
    }

    /// <summary>Nhan đề chỉ để hiển thị — catalog không trả lời được thì bỏ qua, không làm hỏng lượt mượn/trả.</summary>
    private async Task EnsureTitlesAsync(IEnumerable<long> mfns, CancellationToken ct)
    {
        try
        {
            await replicas.EnsureBibsAsync(mfns, ct);
        }
        catch (HttpRequestException)
        {
            // catalog đang down — hiển thị không có nhan đề
        }
    }

    /// <summary>Người đang thao tác ("Họ tên (tên đăng nhập)" từ token; trạm SIP2/việc nền lấy theo người đã đặt).</summary>
    public string? StaffName() => auditContext?.Capture().ActorName;

    private Task Record(Loan loan, CrudChange change, string summary, CancellationToken ct, JsonObject? changes = null, string? reason = null) =>
        AuditSink?.RecordAsync(new CrudAuditEntry(nameof(Loan), EntityName, loan.PublicId, change, summary, changes, reason), ct) ?? Task.CompletedTask;

    private sealed record LoanSnapshot(string State, string? CloseKind, string? CloseReason, DateTimeOffset? ReturnedAt, int ReopenCount);

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    private static LoanSnapshot Snapshot(Loan l) => new(l.IsOpen ? "open" : "closed", l.CloseKind, l.CloseReason, l.ReturnedAt, l.ReopenCount);

    private static JsonObject ToObject(object snapshot) => JsonSerializer.SerializeToNode(snapshot, SnapshotJson)!.AsObject();

    private async Task<Loan> RequireLoanAsync(Guid id, CancellationToken ct) =>
        await Set.FirstOrDefaultAsync(x => x.PublicId == id, ct) ?? throw new NotFoundException(EntityName, id);

    private static string Label(Loan loan) => $"ĐKCB {loan.Barcode} — thẻ {loan.CardNo}";

    /// <summary>
    /// Đóng cưỡng bức lượt mượn đang mở (bạn đọc đã rời trường, mất dấu tài liệu nhưng không tính phạt mất…): lượt đóng, bản sách KHÔNG đổi
    /// trạng thái (về sẵn sàng như đã trả), không sinh phiếu phạt. Lý do bắt buộc, lưu trước/sau; mặc định phải chờ người có BORROW:approve duyệt.
    /// </summary>
    public async Task<LoanActionOutcome> ForceCloseAsync(LoanActionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ChangeReason.Require(request.Reason, null, "đóng cưỡng bức lượt mượn");
        var loan = await RequireLoanAsync(request.LoanId, ct);
        if (!loan.IsOpen) throw new BusinessRuleException("LOAN_ALREADY_RETURNED", $"ĐKCB {loan.Barcode} đã đóng lúc {loan.ReturnedAt:dd/MM/yyyy HH:mm}.");
        if (await approvals.RequiresAsync(LoanApprovals.ForceClose, null, null, null, ct))
        {
            var after = Snapshot(loan) with { State = "closed", CloseKind = Loan.CloseForced, CloseReason = reason };
            return new LoanActionOutcome(null, await approvals.SubmitAsync(LoanApprovals.ForceClose, loan.PublicId, Label(loan),
                $"Đóng cưỡng bức {Label(loan)}", request with { Reason = reason }, ToObject(Snapshot(loan)), ToObject(after), reason, null, null, loan.Version, ct));
        }
        return new LoanActionOutcome(await ApplyForceCloseAsync(loan, reason, ct), null);
    }

    /// <summary>Chạy lại đóng cưỡng bức đã được duyệt (người duyệt là người thao tác).</summary>
    public async Task ApplyApprovedForceCloseAsync(Guid loanId, string reason, CancellationToken ct) =>
        await ApplyForceCloseAsync(await RequireLoanAsync(loanId, ct), reason, ct);

    private async Task<LoanDto> ApplyForceCloseAsync(Loan loan, string reason, CancellationToken ct)
    {
        var before = Snapshot(loan);
        loan.ForceClose(clock.GetUtcNow(), actor.Id, StaffName(), reason);
        await PublishAsync(loan, ct);
        var item = await Db.Set<ItemReplica>().FirstOrDefaultAsync(i => i.ItemPublicId == loan.ItemPublicId, ct);
        if (item is not null) await holds.AssignNextAsync(item, ct, returningLoan: loan.PublicId); // bản trở lại sẵn sàng: giữ cho người đặt mượn kế tiếp
        await Record(loan, CrudChange.Updated, $"Đóng cưỡng bức {Label(loan)}", ct, CrudDiff.Compute(before, Snapshot(loan)), reason);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    /// <summary>
    /// Mở lại lượt mượn đã đóng (hoàn tác trả nhầm / đóng nhầm). Chặn khi bản đã được mượn lại hoặc đang giữ cho người đặt mượn, hoặc khi
    /// phiếu phạt liên quan đã thu tiền / còn dòng "Mất" (phải điều chỉnh phiếu trước, không để tiền và trạng thái lệch nhau).
    /// </summary>
    public async Task<LoanActionOutcome> ReopenAsync(LoanActionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ChangeReason.Require(request.Reason, null, "mở lại lượt mượn");
        var loan = await RequireLoanAsync(request.LoanId, ct);
        if (loan.IsOpen) throw new BusinessRuleException("LOAN_NOT_CLOSED", $"Lượt mượn {loan.Barcode} đang mở.");
        await EnsureReopenableAsync(loan, ct);
        if (await approvals.RequiresAsync(LoanApprovals.Reopen, null, null, null, ct))
        {
            var after = Snapshot(loan) with { State = "open", CloseKind = null, CloseReason = null, ReturnedAt = null };
            return new LoanActionOutcome(null, await approvals.SubmitAsync(LoanApprovals.Reopen, loan.PublicId, Label(loan),
                $"Mở lại lượt mượn {Label(loan)}", request with { Reason = reason }, ToObject(Snapshot(loan)), ToObject(after), reason, null, null, loan.Version, ct));
        }
        return new LoanActionOutcome(await ApplyReopenAsync(loan, reason, ct), null);
    }

    public async Task ApplyApprovedReopenAsync(Guid loanId, string reason, CancellationToken ct)
    {
        var loan = await RequireLoanAsync(loanId, ct);
        await EnsureReopenableAsync(loan, ct); // điều kiện có thể đổi từ lúc đề nghị
        await ApplyReopenAsync(loan, reason, ct);
    }

    public Task<long?> VersionAsync(Guid loanId, CancellationToken ct) =>
        Set.AsNoTracking().Where(x => x.PublicId == loanId).Select(x => (long?)x.Version).FirstOrDefaultAsync(ct);

    private async Task<LoanDto> ApplyReopenAsync(Loan loan, string reason, CancellationToken ct)
    {
        var before = Snapshot(loan);
        loan.Reopen();
        await publisher.PublishAsync(loan, ct, reopened: true);
        await Record(loan, CrudChange.Reversed, $"Mở lại lượt mượn {Label(loan)}", ct, CrudDiff.Compute(before, Snapshot(loan)), reason);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    private async Task EnsureReopenableAsync(Loan loan, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.ItemPublicId == loan.ItemPublicId && x.Id != loan.Id && (x.ReturnedAt == null || x.LoanedAt > loan.LoanedAt), ct))
            throw new BusinessRuleException("LOAN_ITEM_REBORROWED", $"ĐKCB {loan.Barcode} đã được mượn lại sau lượt này — không mở lại được.", 409);
        if (await holds.HeldAsync(loan.ItemPublicId, ct) is not null)
            throw new BusinessRuleException("LOAN_REOPEN_HELD", $"ĐKCB {loan.Barcode} đang giữ cho người đặt mượn — huỷ đặt mượn trước khi mở lại lượt.", 409);
        var lines = await Db.Set<FineLine>().Where(l => l.LoanPublicId == loan.PublicId).Select(l => new { l.FineTicketId, l.ReasonId }).ToListAsync(ct);
        if (lines.Count == 0) return;
        var ticketIds = lines.Select(l => l.FineTicketId).Distinct().ToList();
        var tickets = await Db.Set<FineTicket>().AsNoTracking().Where(t => ticketIds.Contains(t.Id)).ToListAsync(ct);
        if (tickets.FirstOrDefault(t => t.Paid != 0 || t.Status == FineTicketStatus.Done) is { } paid)
            throw new BusinessRuleException("LOAN_REOPEN_PAID_FINE",
                $"Phiếu phạt {paid.Code} liên quan đã thu tiền hoặc hoàn thành — dùng \"Điều chỉnh\" / mở lại phiếu trước khi mở lại lượt mượn.", 409);
        var lostReasons = await Db.Set<FineReason>().IgnoreQueryFilters(["SoftDelete"]).Where(r => r.ItemStatus == Loan.LostStatus).Select(r => r.Id).ToListAsync(ct);
        if (lines.Any(l => lostReasons.Contains(l.ReasonId)))
            throw new BusinessRuleException("LOAN_REOPEN_LOST_LINE",
                "Phiếu phạt còn dòng \"Mất tài liệu\" của lượt mượn này — xoá dòng đó khỏi phiếu trước khi mở lại lượt mượn.", 409);
    }

    private static string RequireReason(string? reason, string what) => ChangeReason.Require(reason, null, what);

    private static DateTimeOffset VietnamStart(DateOnly date) => Loan.VietnamStart(date);

    private Task PublishAsync(Loan loan, CancellationToken ct) => publisher.PublishAsync(loan, ct);

}

/// <summary>Áp dụng yêu cầu lượt mượn đã duyệt (đóng cưỡng bức / mở lại) dưới danh nghĩa người duyệt.</summary>
public sealed class LoanChangeHandler(LoanResource loans) : IChangeRequestHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool CanHandle(string kind) => kind is LoanApprovals.ForceClose or LoanApprovals.Reopen;

    public Task<long?> CurrentVersionAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return loans.VersionAsync(request.TargetPublicId, cancellationToken);
    }

    public async Task ApplyAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = JsonSerializer.Deserialize<LoanActionRequest>(request.PayloadJson, Json)!;
        if (request.Kind == LoanApprovals.ForceClose) await loans.ApplyApprovedForceCloseAsync(payload.LoanId, request.Reason, cancellationToken);
        else await loans.ApplyApprovedReopenAsync(payload.LoanId, request.Reason, cancellationToken);
    }
}
