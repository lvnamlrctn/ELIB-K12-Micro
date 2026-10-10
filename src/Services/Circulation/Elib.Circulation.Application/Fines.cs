using System.Globalization;
using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Circulation;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>Phát <see cref="LoanChanged"/> (outbox, cùng transaction với thay đổi lượt mượn) — dùng chung cho quầy, phiếu phạt, job.</summary>
public sealed class LoanPublisher(IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor)
{
    public Task PublishAsync(Loan loan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(loan);
        if (loan.PublicId == Guid.Empty) loan.PublicId = Guid.CreateVersion7(); // lượt mới: interceptor chỉ gán khi còn trống
        return publisher.Publish(ToEvent(loan), ct);
    }

    public LoanChanged ToEvent(Loan loan) =>
        new()
        {
            TenantId = tenant.RequireTenantId(),
            Actor = new EventActor(actor.Id, actor.Kind),
            LoanPublicId = loan.PublicId,
            ReaderPublicId = loan.ReaderPublicId,
            CardNo = loan.CardNo,
            ItemPublicId = loan.ItemPublicId,
            Barcode = loan.Barcode,
            Mfn = loan.Mfn,
            CircPlaceId = loan.CircPlaceId,
            LoanedAt = loan.LoanedAt,
            DueAt = loan.DueAt,
            ReturnedAt = loan.ReturnedAt,
            RenewCount = loan.RenewCount,
            ClosedItemStatus = loan.ClosedItemStatus,
            Version = loan.Version,
        };
}

public sealed record FineReasonRequest(string Code, string Name, decimal Amount = 0, string? ItemStatus = null);

public sealed record FineReasonDto(long Id, Guid PublicId, string Code, string Name, decimal Amount, string? ItemStatus, bool IsBuiltIn,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Lý do phạt (monolith: CFineTypeController, quyền FINE_REASONS). Mã không trùng; lý do "Quá hạn" không xoá/đổi mã được.</summary>
public sealed class FineReasonResource(ICrudDbContext db)
    : CrudResource<FineReasonResource, FineReason, CrudSearch, FineReasonRequest, FineReasonDto>(db)
{
    protected override string EntityName => "Lý do phạt";

    protected override string? Describe(FineReason entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<FineReason, FineReasonDto>> Projection => x => new FineReasonDto(
        x.Id, x.PublicId, x.Code, x.Name, x.Amount, x.ItemStatus, x.Code == FineReason.OverdueCode, x.CreatedAt, x.UpdatedAt);

    protected override FineReason Create(FineReasonRequest r) => FineReason.Create(r.Code, r.Name, r.Amount, r.ItemStatus);

    protected override void Update(FineReason entity, FineReasonRequest r)
    {
        if (entity.Code == FineReason.OverdueCode && !string.Equals(r.Code?.Trim(), FineReason.OverdueCode, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("FINE_REASON_BUILT_IN", "Không đổi được mã lý do \"Quá hạn\" — dòng phạt quá hạn dùng mã này.");
        entity.Update(r.Code ?? "", r.Name, r.Amount, r.ItemStatus);
    }

    protected override IQueryable<FineReason> Filter(IQueryable<FineReason> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<FineReason> Order(IQueryable<FineReason> query) => query.OrderBy(x => x.Code).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(FineReason entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("FINE_REASON_CODE_EXISTS", $"Mã lý do phạt '{entity.Code}' đã có.");
    }

    protected override Task EnsureDeletableAsync(FineReason entity, CancellationToken ct) => entity.Code == FineReason.OverdueCode
        ? throw new BusinessRuleException("FINE_REASON_BUILT_IN", "Không xoá được lý do \"Quá hạn\" — dòng phạt quá hạn dùng mã này.")
        : Task.CompletedTask;

    /// <summary>Lý do phạt mặc định của monolith (thêm cái còn thiếu theo mã) — khởi tạo đơn vị và nút "Thêm lý do mặc định".</summary>
    public async Task<int> AddDefaultsAsync(CancellationToken ct)
    {
        (string Code, string Name, string? Status)[] defaults =
        [
            (FineReason.OverdueCode, "Quá hạn", null),
            (FineReason.LostCode, "Mất tài liệu", Loan.LostStatus),
            ("HUHONG", "Hư hỏng tài liệu", null),
        ];
        var existing = await Set.Select(x => x.Code).ToListAsync(ct);
        var missing = defaults.Where(d => !existing.Contains(d.Code)).ToList();
        foreach (var d in missing) Set.Add(FineReason.Create(d.Code, d.Name, 0, d.Status));
        if (missing.Count > 0) await Db.SaveChangesAsync(ct);
        return missing.Count;
    }
}

/// <summary>Danh sách phiếu phạt (monolith: FineTicket/Search). TicketStatus: 1 đang xử lý, 2 đã hoàn thành. From/To: ngày phạt (giờ VN).</summary>
public sealed class FineTicketSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public int? TicketStatus { get; set; }
    public bool? Unpaid { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record FineTicketDto(
    long Id, Guid PublicId, long Number, Guid ReaderPublicId, string CardNo, string? ReaderName, DateTimeOffset FineDate, int Status,
    int Round, decimal? ManualAmount, decimal Total, decimal Discount, decimal Paid, decimal Remaining, string? Note, int LineCount,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)
{
    public string Code => FineTicket.FormatCode(Number);
}

public sealed record FineLineDto(
    long Id, long ReasonId, string ReasonCode, string? ReasonName, decimal Amount, Guid? LoanPublicId, string? Barcode, long? Mfn,
    string? Title, int OverdueDays, DateTimeOffset? DueAt, bool LoanOpen);

public sealed record FineTicketDetail(FineTicketDto Ticket, IReadOnlyList<FineLineDto> Lines, decimal FinePerDay);

public sealed record FineTicketTotals(decimal Receivable, decimal Received, decimal Remaining);

/// <summary>Phiếu phạt thủ công (monolith: FineTicket/Create) — không có dòng tài liệu.</summary>
public sealed record FineTicketCreateRequest(string CardNo, decimal Amount, string? Note = null, DateTimeOffset? FineDate = null);

/// <summary>Gom tài liệu đang mượn quá hạn (+ lượt cán bộ tích chọn, ví dụ mất tài liệu) vào phiếu đang mở của bạn đọc.</summary>
public sealed record BuildFineTicketRequest(string CardNo, long? CircPlaceId = null, IReadOnlyList<Guid>? LoanIds = null);

/// <summary>Dòng sửa (Id có) hoặc thêm mới (Id trống — theo ĐKCB hoặc khoản tự do).</summary>
public sealed record FineLineChange(long? Id, string ReasonCode, decimal Amount, string? Barcode = null);

public sealed record SaveFineTicketRequest(
    int Status, decimal Discount, decimal Paid, string? Note = null, decimal? ManualAmount = null, DateTimeOffset? FineDate = null,
    IReadOnlyList<FineLineChange>? Lines = null, IReadOnlyList<long>? DeletedLineIds = null);

/// <summary>
/// Phiếu phạt (monolith: CFineTicketController + FineTicketService, quyền FINES). Dòng quá hạn tính theo ngày × tiền phạt mỗi ngày của
/// chính sách; dòng có lý do "Mất" đóng lượt mượn đang mở và báo holdings đổi trạng thái bản sách (LoanChanged.ClosedItemStatus).
/// </summary>
public sealed class FineTicketResource(
    ICrudDbContext db, Replicas replicas, LoanPublisher loans, FineReasonResource fineReasons, ICurrentActor actor, TimeProvider clock)
    : CrudResource<FineTicketResource, FineTicket, FineTicketSearch, FineTicketCreateRequest, FineTicketDto>(db)
{
    public const int MaxLines = 200;

    protected override string EntityName => "Phiếu phạt";

    protected override string? Describe(FineTicket entity) =>
        $"Phiếu {entity.Code} — thẻ {entity.CardNo}, tổng {Money(entity.Total)}, đã nộp {Money(entity.Paid)}";

    protected override Expression<Func<FineTicket, FineTicketDto>> Projection => x => new FineTicketDto(
        x.Id, x.PublicId, x.Number, x.ReaderPublicId, x.CardNo,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.FineDate, x.Status, x.Round, x.ManualAmount, x.Total, x.Discount, x.Paid, x.Remaining, x.Note, x.Lines.Count,
        x.CreatedAt, x.UpdatedAt);

    protected override FineTicket Create(FineTicketCreateRequest request) => throw new NotSupportedException("Phiếu phạt tạo qua CreateAsync.");

    protected override async Task<FineTicket> CreateAsync(FineTicketCreateRequest request, CancellationToken ct)
    {
        var reader = await RequireReaderAsync(request.CardNo, ct);
        var ticket = await NewTicketAsync(reader, ct);
        ticket.Settle(request.FineDate, request.Amount, 0, 0, request.Note, FineTicketStatus.Open);
        return ticket;
    }

    protected override void Update(FineTicket entity, FineTicketCreateRequest request) => throw new NotSupportedException("Phiếu phạt sửa qua SaveAsync.");

    protected override async Task EnsureDeletableAsync(FineTicket entity, CancellationToken ct)
    {
        if (!entity.IsOpen || entity.Paid > 0)
            throw new BusinessRuleException("FINE_TICKET_LOCKED", $"Phiếu {entity.Code} đã hoàn thành hoặc đã thu tiền — không xoá được.");
        await Db.Set<FineTicket>().Entry(entity).Collection(t => t.Lines).LoadAsync(ct);
        var statusReasons = await Db.Set<FineReason>().IgnoreQueryFilters(["SoftDelete"]).Where(r => r.ItemStatus != null).Select(r => r.Id).ToListAsync(ct);
        if (entity.Lines.Any(l => l.LoanPublicId is not null && statusReasons.Contains(l.ReasonId)))
            throw new BusinessRuleException("FINE_TICKET_LOCKED", $"Phiếu {entity.Code} có dòng báo mất tài liệu — không xoá được.");
    }

    protected override IQueryable<FineTicket> Filter(IQueryable<FineTicket> query, FineTicketSearch s)
    {
        if (!string.IsNullOrWhiteSpace(s.CardNo))
        {
            var card = PatronReplica.Key(s.CardNo);
            query = query.Where(x => x.CardNo == card);
        }
        if (s.TicketStatus is { } status) query = query.Where(x => x.Status == status);
        if (s.Unpaid == true) query = query.Where(x => x.Remaining > 0);
        if (s.From is { } from)
        {
            var start = VietnamStart(from);
            query = query.Where(x => x.FineDate >= start);
        }
        if (s.To is { } to)
        {
            var end = VietnamStart(to.AddDays(1));
            query = query.Where(x => x.FineDate < end);
        }
        if (s.Term is { } term)
        {
            var key = term.ToUpperInvariant();
            var number = key.StartsWith("PT", StringComparison.Ordinal) && long.TryParse(key[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => x.Number == number || x.CardNo.StartsWith(key)
                || Db.Set<PatronReplica>().Any(r => r.ReaderPublicId == x.ReaderPublicId && r.FullName.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<FineTicket> Order(IQueryable<FineTicket> query) => query.OrderByDescending(x => x.FineDate).ThenByDescending(x => x.Id);

    /// <summary>Tổng phải thu / đã thu / còn lại trên toàn bộ kết quả lọc (không chỉ trang đang xem).</summary>
    public async Task<FineTicketTotals> TotalsAsync(FineTicketSearch search, CancellationToken ct)
    {
        var rows = await Query(search).Select(x => new { x.Total, x.Discount, x.Paid }).ToListAsync(ct); // cộng ngoài SQL: SQLite (test) không SUM decimal
        var receivable = rows.Sum(x => x.Total - x.Discount);
        var received = rows.Sum(x => x.Paid);
        return new FineTicketTotals(receivable, received, receivable - received);
    }

    /// <summary>Tiền phạt bạn đọc còn phải nộp (mọi phiếu) — hiện ở quầy mượn trả.</summary>
    public async Task<decimal> UnpaidAsync(Guid readerPublicId, CancellationToken ct) =>
        (await Set.AsNoTracking().Where(x => x.ReaderPublicId == readerPublicId).Select(x => x.Remaining).ToListAsync(ct)).Where(x => x > 0).Sum();

    public async Task<FineTicketDetail> DetailAsync(Guid publicId, CancellationToken ct)
    {
        var ticket = await Set.AsNoTracking().Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == publicId, ct)
            ?? throw new NotFoundException(EntityName, publicId);
        return await DetailAsync(ticket, null, ct);
    }

    /// <summary>
    /// Gom vào phiếu đang mở của bạn đọc (chưa có thì lập): lượt đang mượn quá hạn → dòng "Quá hạn" (số ngày × tiền phạt/ngày của
    /// chính sách); lượt cán bộ tích chọn chưa quá hạn → dòng "Mất tài liệu" với số tiền gợi ý của lý do. Lượt đã có dòng trong
    /// phiếu này thì bỏ qua (monolith: BuildForReader).
    /// </summary>
    public async Task<FineTicketDetail> BuildForReaderAsync(BuildFineTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reader = await RequireReaderAsync(request.CardNo, ct);
        var reasons = await Db.Set<FineReason>().ToListAsync(ct);
        if (reasons.Count == 0 && await fineReasons.AddDefaultsAsync(ct) > 0) reasons = await Db.Set<FineReason>().ToListAsync(ct); // đơn vị có trước bản này
        var now = clock.GetUtcNow();
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.ReaderPublicId == reader.ReaderPublicId && t.Status == FineTicketStatus.Open, ct);
        var created = ticket is null;
        ticket ??= await NewTicketAsync(reader, ct);

        var selected = request.LoanIds ?? [];
        var fined = ticket.Lines.Where(l => l.LoanPublicId is not null).Select(l => l.LoanPublicId!.Value).ToHashSet();
        var candidates = await Db.Set<Loan>().Where(x => x.ReaderPublicId == reader.ReaderPublicId && x.ReturnedAt == null
                && (x.DueAt < now || selected.Contains(x.PublicId)))
            .OrderBy(x => x.DueAt).ToListAsync(ct);
        candidates = [.. candidates.Where(l => !fined.Contains(l.PublicId))];

        var overdue = reasons.FirstOrDefault(r => r.Code == FineReason.OverdueCode)
            ?? throw new BusinessRuleException("FINE_REASON_MISSING", "Thiếu lý do phạt \"Quá hạn\" (QUAHAN) — thêm trong danh mục lý do phạt.");
        var lost = reasons.FirstOrDefault(r => r.Code == FineReason.LostCode) ?? reasons.FirstOrDefault(r => r.ItemStatus == Loan.LostStatus);
        var policies = await Db.Set<LoanPolicy>().AsNoTracking().ToListAsync(ct);
        foreach (var loan in candidates)
        {
            if (loan.IsOverdue(now))
            {
                var days = loan.OverdueDays(now);
                var rate = LoanPolicyResource.Resolve(policies, reader.ReaderTypeId, request.CircPlaceId ?? loan.CircPlaceId).FinePerDay;
                ticket.AddLine(overdue, days * rate, loan, null, null, days);
            }
            else
            {
                var reason = lost ?? throw new BusinessRuleException("FINE_REASON_MISSING", "Thiếu lý do phạt \"Mất tài liệu\" — thêm trong danh mục lý do phạt.");
                ticket.AddLine(reason, reason.Amount, loan, null, null, 0);
            }
        }
        if (ticket.Lines.Count > MaxLines) throw new BusinessRuleException("FINE_LINES_LIMIT", $"Một phiếu phạt tối đa {MaxLines} dòng.");
        if (created || candidates.Count > 0)
        {
            await Record(ticket, created ? CrudChange.Added : CrudChange.Updated,
                $"Lập phiếu phạt {ticket.Code} — thẻ {reader.CardNo}: thêm {candidates.Count} tài liệu, tổng {Money(ticket.Total)}", ct);
            await Db.SaveChangesAsync(ct);
        }
        return await DetailAsync(ticket, policies, ct);
    }

    /// <summary>
    /// Lưu phiếu (monolith: FineTicket/Save): sửa/thêm/xoá dòng (chỉ khi phiếu đang mở), giảm trừ, đã nộp, ghi chú, trạng thái. Dòng có
    /// lý do đổi trạng thái bản sách (Mất) mà lượt mượn còn mở → đóng lượt (không hoàn tác khi xoá dòng).
    /// </summary>
    public async Task<FineTicketDetail> SaveAsync(Guid publicId, SaveFineTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        var reasons = await Db.Set<FineReason>().ToDictionaryAsync(r => r.Code, ct);
        FineReason Reason(string? code) => reasons.TryGetValue((code ?? "").Trim().ToUpperInvariant(), out var r)
            ? r
            : throw new BusinessRuleException("FINE_REASON_INVALID", "Dòng phạt phải chọn lý do phạt hợp lệ.");

        var reopened = ticket.Status == FineTicketStatus.Done && request.Status == FineTicketStatus.Open;
        if (reopened) ticket.Settle(null, ticket.ManualAmount, ticket.Discount, ticket.Paid, ticket.Note, FineTicketStatus.Open);

        foreach (var id in request.DeletedLineIds ?? [])
            if (ticket.Lines.FirstOrDefault(l => l.Id == id) is { } line) ticket.RemoveLine(line);

        foreach (var change in request.Lines ?? [])
        {
            if (change.Id is { } id)
            {
                var line = ticket.Lines.FirstOrDefault(l => l.Id == id) ?? throw new NotFoundException("Dòng phạt", id);
                var reason = Reason(change.ReasonCode);
                if (line.ReasonId == reason.Id && line.Amount == change.Amount) continue;
                if (!ticket.IsOpen) throw new BusinessRuleException("FINE_TICKET_DONE", $"Phiếu {ticket.Code} đã hoàn thành, không sửa dòng phạt được.");
                ticket.ChangeLine(line, reason, change.Amount);
            }
            else
            {
                await AddManualLineAsync(ticket, Reason(change.ReasonCode), change, ct);
            }
        }
        if (ticket.Lines.Count > MaxLines) throw new BusinessRuleException("FINE_LINES_LIMIT", $"Một phiếu phạt tối đa {MaxLines} dòng.");

        // Dòng lý do "Mất" mà lượt mượn còn mở (kể cả dòng tạo sẵn khi gom phiếu) → đóng lượt, báo holdings — như monolith mỗi lần lưu.
        var now = clock.GetUtcNow();
        var lostLoans = new List<string>();
        var lostReasons = reasons.Values.Where(r => r.ItemStatus == Loan.LostStatus).Select(r => r.Id).ToHashSet();
        foreach (var line in ticket.Lines.Where(l => l.LoanPublicId is not null && lostReasons.Contains(l.ReasonId)))
        {
            var loan = await Db.Set<Loan>().FirstOrDefaultAsync(x => x.PublicId == line.LoanPublicId && x.ReturnedAt == null, ct);
            if (loan is null) continue;
            loan.CloseAsLost(now, actor.Id);
            await loans.PublishAsync(loan, ct);
            lostLoans.Add(loan.Barcode);
        }

        ticket.Settle(request.FineDate, request.ManualAmount ?? ticket.ManualAmount, request.Discount, request.Paid, request.Note, request.Status);
        await Record(ticket, CrudChange.Updated,
            $"Lưu phiếu phạt {ticket.Code} — tổng {Money(ticket.Total)}, giảm {Money(ticket.Discount)}, đã nộp {Money(ticket.Paid)}"
            + (ticket.IsOpen ? "" : ", hoàn thành") + (lostLoans.Count > 0 ? $"; báo mất {string.Join(", ", lostLoans)}" : ""), ct);
        await Db.SaveChangesAsync(ct);
        return await DetailAsync(ticket, null, ct);
    }

    private async Task<FineLine> AddManualLineAsync(FineTicket ticket, FineReason reason, FineLineChange change, CancellationToken ct)
    {
        var barcode = change.Barcode?.Trim();
        if (string.IsNullOrEmpty(barcode)) return ticket.AddLine(reason, change.Amount, null, null, null, 0);

        var key = ItemReplica.Key(barcode);
        if (ticket.Lines.Any(l => l.ReasonId == reason.Id && l.Barcode is not null && ItemReplica.Key(l.Barcode) == key))
            throw new BusinessRuleException("FINE_LINE_EXISTS", $"ĐKCB {barcode} đã có dòng phạt cùng lý do trong phiếu.");
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToUpper() sang upper() của SQL
        var loan = await Db.Set<Loan>().Where(x => x.ReaderPublicId == ticket.ReaderPublicId && x.ReturnedAt == null && x.Barcode.ToUpper() == key)
            .FirstOrDefaultAsync(ct);
#pragma warning restore CA1862, CA1304, CA1311
        if (loan is not null) return ticket.AddLine(reason, change.Amount, loan, null, null, loan.OverdueDays(clock.GetUtcNow()));
        var item = await replicas.ItemAsync(barcode, ct) ?? throw new BusinessRuleException("ITEM_NOT_FOUND", $"Không tìm thấy ĐKCB \"{barcode}\".");
        return ticket.AddLine(reason, change.Amount, null, item.Barcode, item.Mfn, 0);
    }

    private async Task<FineTicket> NewTicketAsync(PatronReplica reader, CancellationToken ct)
    {
        var all = Set.IgnoreQueryFilters(["SoftDelete"]); // số phiếu đã xoá không cấp lại
        var number = (await all.MaxAsync(x => (long?)x.Number, ct) ?? 0) + 1;
        var round = await all.CountAsync(x => x.ReaderPublicId == reader.ReaderPublicId, ct) + 1;
        var ticket = FineTicket.Open(reader, number, round, clock.GetUtcNow());
        Set.Add(ticket);
        return ticket;
    }

    private async Task<FineTicketDetail> DetailAsync(FineTicket ticket, List<LoanPolicy>? policies, CancellationToken ct)
    {
        var dto = await Set.AsNoTracking().Where(x => x.Id == ticket.Id).Select(Projection).FirstAsync(ct);
        var reasons = await Db.Set<FineReason>().IgnoreQueryFilters(["SoftDelete"]).AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var loanIds = ticket.Lines.Where(l => l.LoanPublicId is not null).Select(l => l.LoanPublicId!.Value).ToList();
        var loanInfo = await Db.Set<Loan>().AsNoTracking().Where(x => loanIds.Contains(x.PublicId))
            .Select(x => new { x.PublicId, x.DueAt, Open = x.ReturnedAt == null }).ToDictionaryAsync(x => x.PublicId, ct);
        var mfns = ticket.Lines.Where(l => l.Mfn is not null).Select(l => l.Mfn!.Value).Distinct().ToList();
        try
        {
            await replicas.EnsureBibsAsync(mfns, ct);
        }
        catch (HttpRequestException)
        {
            // catalog đang down — hiển thị không có nhan đề
        }
        var titles = await Db.Set<BibSnapshot>().AsNoTracking().Where(b => mfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, b => b.Title, ct);
        var lines = ticket.Lines.OrderBy(l => l.Id).Select(l => new FineLineDto(
            l.Id, l.ReasonId, l.ReasonCode, reasons.GetValueOrDefault(l.ReasonId), l.Amount, l.LoanPublicId, l.Barcode, l.Mfn,
            l.Mfn is { } m ? titles.GetValueOrDefault(m) : null, l.OverdueDays,
            l.LoanPublicId is { } id && loanInfo.TryGetValue(id, out var info) ? info.DueAt : null,
            l.LoanPublicId is { } id2 && loanInfo.TryGetValue(id2, out var info2) && info2.Open)).ToList();

        policies ??= await Db.Set<LoanPolicy>().AsNoTracking().ToListAsync(ct);
        var readerType = await Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == ticket.ReaderPublicId).Select(r => r.ReaderTypeId).FirstOrDefaultAsync(ct);
        return new FineTicketDetail(dto, lines, LoanPolicyResource.Resolve(policies, readerType, null).FinePerDay);
    }

    private async Task<PatronReplica> RequireReaderAsync(string? cardNo, CancellationToken ct) =>
        await replicas.ReaderAsync(cardNo ?? "", ct)
        ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{cardNo?.Trim()}'.", 404);

    private Task Record(FineTicket ticket, CrudChange change, string summary, CancellationToken ct)
    {
        if (ticket.PublicId == Guid.Empty) ticket.PublicId = Guid.CreateVersion7();
        return AuditSink?.RecordAsync(new CrudAuditEntry(nameof(FineTicket), EntityName, ticket.PublicId, change, summary), ct) ?? Task.CompletedTask;
    }

    private static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    private static DateTimeOffset VietnamStart(DateOnly date) => Loan.VietnamStart(date);
}
