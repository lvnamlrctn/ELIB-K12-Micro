using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Circulation;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>
/// Lịch sử lưu thông (monolith: CirculationHistory/Search, quyền LOAN_HISTORY). <see cref="State"/>: open (đang mượn),
/// overdue (đang mượn quá hạn), returned (đã trả); trống = tất cả. From/To lọc theo ngày mượn (giờ Việt Nam).
/// </summary>
public sealed class LoanSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public string? Barcode { get; set; }
    public string? State { get; set; }
    public long? CircPlaceId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record LoanDto(
    long Id, Guid PublicId, Guid ReaderPublicId, string CardNo, string? ReaderName, Guid ItemPublicId, string Barcode, long Mfn,
    string? Title, string? Author, long? CircPlaceId, string? StoreName, DateTimeOffset LoanedAt, DateTimeOffset DueAt,
    DateTimeOffset? ReturnedAt, int RenewCount, string? Note, long Version);

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

/// <summary>
/// Lượt mượn (monolith: CirculationLoanController + CirculationHistoryController, quyền BORROW / LOAN_HISTORY). Đọc bản sao bạn đọc,
/// bản sách, biểu ghi trong cùng DB (docs 03: mượn vẫn chạy khi patron/holdings/catalog down). Mọi thay đổi phát
/// <see cref="LoanChanged"/> (outbox, cùng transaction).
/// </summary>
public sealed class LoanResource(
    ICrudDbContext db, Replicas replicas, LoanPublisher publisher, HoldAllocator holds, HoldResource holdList, ICurrentActor actor, TimeProvider clock)
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
        x.LoanedAt, x.DueAt, x.ReturnedAt, x.RenewCount, x.Note, x.Version);

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
        var now = clock.GetUtcNow();
        query = (s.State ?? "").Trim().ToLowerInvariant() switch
        {
            "open" => query.Where(x => x.ReturnedAt == null),
            "overdue" => query.Where(x => x.ReturnedAt == null && x.DueAt < now),
            "returned" => query.Where(x => x.ReturnedAt != null),
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
        var block = BlockReason(reader, loans.Any(l => l.DueAt < now));
        var unpaid = (await Db.Set<FineTicket>().AsNoTracking().Where(t => t.ReaderPublicId == reader.ReaderPublicId)
            .Select(t => t.Remaining).ToListAsync(ct)).Where(r => r > 0).Sum();
        return new ReaderPanel(reader.ReaderPublicId, reader.CardNo, reader.FullName, reader.ReaderTypeName, reader.ClassName, reader.CourseName,
            reader.PhotoId, reader.ExpireDate, reader.Status != IHasStatus.Active, IsExpired(reader), loans.Any(l => l.DueAt < now),
            block is null, block, policy.LoanDays, policy.MaxLoans, policy.MaxRenewals, loans, unpaid,
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
        var policy = await PolicyAsync(reader, place.Id, ct);

        var lines = new List<CheckoutLine>();
        var created = new List<Loan>();
        foreach (var barcode in barcodes)
        {
            var (loan, message) = await TryCheckoutAsync(reader, barcode, place, policy, open.Count + created.Count, now, ct);
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
        loan.Return(now, request.CircPlaceId, actor.Id);
        await PublishAsync(loan, ct);
        var item = await Db.Set<ItemReplica>().FirstOrDefaultAsync(i => i.ItemPublicId == loan.ItemPublicId, ct);
        var next = item is null ? null : await holds.AssignNextAsync(item, ct, returningLoan: loan.PublicId);
        await Record(loan, CrudChange.Updated, $"Trả ĐKCB {loan.Barcode} — thẻ {loan.CardNo}" + (loan.OverdueDays(now) is > 0 and var d ? $", quá hạn {d} ngày" : "")
            + (next is null ? "" : $"; giữ cho đặt mượn thẻ {next.CardNo}"), ct);
        await Db.SaveChangesAsync(ct);
        return new ReturnResult((await ToDtosAsync([loan], ct))[0], loan.OverdueDays(now), next is null ? null : await holdList.GetAsync(next.Id, ct));
    }

    /// <summary>Gia hạn (monolith: Renew): theo chính sách của bạn đọc tại điểm mượn; lý do bắt buộc, ghi vào nhật ký.</summary>
    public async Task<LoanDto> RenewAsync(RenewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireReason(request.Reason, "gia hạn");
        var loan = await Set.FirstOrDefaultAsync(x => x.PublicId == request.LoanId, ct) ?? throw new NotFoundException(EntityName, request.LoanId);
        var reader = await Db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == loan.ReaderPublicId, ct);
        var policy = await PolicyAsync(reader, loan.CircPlaceId, ct);
        var oldDue = loan.DueAt;
        var now = clock.GetUtcNow();
        loan.Renew(now, policy);
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

    /// <summary>Xuất Excel lịch sử lưu thông theo bộ lọc đang xem (monolith: CirculationHistory/Export), tối đa <see cref="CrudExcel.MaxExportRows"/> dòng.</summary>
    public async Task<byte[]> ExportAsync(LoanSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var rows = await Query(search).Take(CrudExcel.MaxExportRows + 1).Select(Projection).ToListAsync(ct);
        if (rows.Count > CrudExcel.MaxExportRows)
            throw new BusinessRuleException("EXPORT_TOO_LARGE", $"Kết quả quá {CrudExcel.MaxExportRows:N0} lượt mượn — thu hẹp khoảng ngày hoặc bộ lọc.");
        var now = clock.GetUtcNow();
        static string State(LoanDto l, DateTimeOffset now) => l.ReturnedAt is not null ? "Đã trả" : l.DueAt < now ? "Quá hạn" : "Đang mượn";
        return CrudExcel.Export<LoanDto>("Lịch sử lưu thông",
        [
            new("cardNo", "Số thẻ", l => l.CardNo),
            new("readerName", "Họ tên", l => l.ReaderName),
            new("barcode", "Số ĐKCB", l => l.Barcode),
            new("title", "Nhan đề", l => l.Title),
            new("author", "Tác giả", l => l.Author),
            new("loanedAt", "Ngày mượn", l => Loan.LocalDate(l.LoanedAt)),
            new("dueAt", "Hạn trả", l => Loan.LocalDate(l.DueAt)),
            new("returnedAt", "Ngày trả", l => l.ReturnedAt is { } r ? Loan.LocalDate(r) : null),
            new("renewCount", "Số lần gia hạn", l => l.RenewCount),
            new("state", "Tình trạng", l => State(l, now)),
            new("note", "Ghi chú", l => l.Note),
        ], rows);
    }

    private async Task<(Loan? Loan, string Message)> TryCheckoutAsync(
        PatronReplica reader, string barcode, CircPlace place, LoanPolicy policy, int openCount, DateTimeOffset now, CancellationToken ct)
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

        var loan = Loan.Open(reader, item, place.Id, now, policy.LoanDays, actor.Id);
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

    private Task Record(Loan loan, CrudChange change, string summary, CancellationToken ct) =>
        AuditSink?.RecordAsync(new CrudAuditEntry(nameof(Loan), EntityName, loan.PublicId, change, summary), ct) ?? Task.CompletedTask;

    private static string RequireReason(string? reason, string what)
    {
        var r = reason?.Trim();
        return r is { Length: > 0 and <= 500 } ? r : throw new BusinessRuleException("REASON_REQUIRED", $"Nhập lý do {what} (tối đa 500 ký tự).");
    }

    private static DateTimeOffset VietnamStart(DateOnly date) => Loan.VietnamStart(date);

    private Task PublishAsync(Loan loan, CancellationToken ct) => publisher.PublishAsync(loan, ct);
}
