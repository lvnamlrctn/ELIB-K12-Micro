using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Circulation;
using Elib.Contracts.Events.Holdings;
using Elib.Contracts.Events.Payment;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>Phát <see cref="LoanChanged"/> (outbox, cùng transaction với thay đổi lượt mượn) — dùng chung cho quầy, phiếu phạt, job.</summary>
public sealed class LoanPublisher(IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor, CrudAuditContext? auditContext = null)
{
    public Task PublishAsync(Loan loan, CancellationToken ct, bool reopened = false)
    {
        ArgumentNullException.ThrowIfNull(loan);
        if (loan.PublicId == Guid.Empty) loan.PublicId = Guid.CreateVersion7(); // lượt mới: interceptor chỉ gán khi còn trống
        return publisher.Publish(ToEvent(loan) with { Reopened = reopened, ActorName = auditContext?.Capture().ActorName }, ct);
    }

    public LoanChanged ToEvent(Loan loan) =>
        new()
        {
            CloseKind = loan.CloseKind,
            CloseReason = loan.CloseReason,
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
            (FineReason.TechFeeCode, "Xử lý kỹ thuật", null),
            (FineReason.ShippingFeeCode, "Vận chuyển", null),
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

public sealed record FineTicketDetail(FineTicketDto Ticket, IReadOnlyList<FineLineDto> Lines, decimal FinePerDay,
    IReadOnlyList<FinePaymentDto>? Payments = null, IReadOnlyList<ChangeRequestDto>? PendingRequests = null);

public sealed record FineTicketTotals(decimal Receivable, decimal Received, decimal Remaining);

/// <summary>Phiếu phạt thủ công (monolith: FineTicket/Create) — không có dòng tài liệu.</summary>
public sealed record FineTicketCreateRequest(string CardNo, decimal Amount, string? Note = null, DateTimeOffset? FineDate = null);

/// <summary>Gom tài liệu đang mượn quá hạn (+ lượt cán bộ tích chọn, ví dụ mất tài liệu) vào phiếu đang mở của bạn đọc.</summary>
public sealed record BuildFineTicketRequest(string CardNo, long? CircPlaceId = null, IReadOnlyList<Guid>? LoanIds = null);

/// <summary>Dòng sửa (Id có) hoặc thêm mới (Id trống — theo ĐKCB hoặc khoản tự do).</summary>
public sealed record FineLineChange(long? Id, string ReasonCode, decimal Amount, string? Barcode = null);

/// <summary>
/// Lưu phiếu. <c>Paid</c> là tổng đã nộp mong muốn: tăng = ghi thêm một khoản thu tiền mặt; GIẢM không được (dùng
/// <see cref="AdjustFinePaymentRequest"/>). <c>Reason</c> bắt buộc khi đổi giảm trừ, sửa/xoá dòng phạt, mở lại phiếu hoặc báo mất tài liệu.
/// </summary>
public sealed record SaveFineTicketRequest(
    int Status, decimal Discount, decimal Paid, string? Note = null, decimal? ManualAmount = null, DateTimeOffset? FineDate = null,
    IReadOnlyList<FineLineChange>? Lines = null, IReadOnlyList<long>? DeletedLineIds = null, string? Reason = null);

/// <summary>Hoàn tiền / điều chỉnh giảm số đã thu: khoản âm có lý do, không sửa khoản cũ. Vượt thẩm quyền thì chờ duyệt.</summary>
public sealed record AdjustFinePaymentRequest(decimal Amount, string Reason, string? Note = null);

public sealed record FinePaymentDto(long Id, decimal Amount, string Method, string? Note, string? Reason, string? ByName, DateTimeOffset PaidAt);

/// <summary>Kết quả lưu: đã áp dụng (<see cref="Detail"/>) hoặc chờ duyệt (<see cref="Pending"/>, HTTP 202, chưa có gì thay đổi).</summary>
public sealed record FineSaveOutcome(FineTicketDetail? Detail, PendingApproval? Pending);

/// <summary>Các loại thao tác phiếu phạt cân nhắc phê duyệt (quyền FINES:approve; ngưỡng mặc định, đơn vị chỉnh được).</summary>
public static class FineApprovals
{
    public const string Discount = "FINE_DISCOUNT";
    public const string Reopen = "FINE_REOPEN";
    public const string Adjust = "FINE_ADJUST";

    /// <summary>Đề xuất của hệ thống khi kho tìm thấy lại sách đã mất do mượn: hoàn dòng phạt "Mất tài liệu" của phiếu (chờ người có FINES:approve duyệt).</summary>
    public const string LostRefund = "FINE_LOST_REFUND";

    public static readonly ApprovalKind[] Kinds =
    [
        new(Discount, "FINES", nameof(FineTicket), "Phiếu phạt", "Miễn/giảm tiền phạt",
            "Giảm trừ vượt ngưỡng số tiền hoặc tỉ lệ so với tổng phạt của phiếu.", Amount: 100_000m, Percent: 20m),
        new(Reopen, "FINES", nameof(FineTicket), "Phiếu phạt", "Mở lại phiếu phạt đã hoàn thành", "Phiếu đã hoàn thành được mở lại để sửa."),
        new(Adjust, "FINES", nameof(FineTicket), "Phiếu phạt", "Hoàn tiền / điều chỉnh giảm số đã thu",
            "Khoản điều chỉnh âm làm giảm số tiền đã thu của phiếu."),
        new(LostRefund, "FINES", nameof(FineTicket), "Phiếu phạt", "Hoàn dòng phạt mất khi tìm thấy sách",
            "Kho tìm thấy lại bản sách mà bạn đọc làm mất: hệ thống đề xuất bỏ dòng phạt mất (và hoàn phần đã thu thừa); người duyệt quyết định."),
    ];
}

/// <summary>
/// Phiếu phạt (monolith: CFineTicketController + FineTicketService, quyền FINES). Dòng quá hạn tính theo ngày × tiền phạt mỗi ngày của
/// chính sách; dòng có lý do "Mất" đóng lượt mượn đang mở và báo holdings đổi trạng thái bản sách (LoanChanged.ClosedItemStatus).
/// </summary>
public sealed class FineTicketResource(
    ICrudDbContext db, Replicas replicas, LoanPublisher loans, FineReasonResource fineReasons, ICurrentActor actor, TimeProvider clock,
    ApprovalService approvals, IPublishEndpoint publisher, ITenantContext tenantContext, ICirculationSettings settings, CrudAuditContext? auditContext = null)
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
        ticket.Settle(request.FineDate, request.Amount, 0, request.Note, FineTicketStatus.Open);
        return ticket;
    }

    protected override void Update(FineTicket entity, FineTicketCreateRequest request) => throw new NotSupportedException("Phiếu phạt sửa qua SaveAsync.");

    protected override Task OnSavingAsync(FineTicket entity, CrudChange change, CancellationToken ct) => change switch
    {
        CrudChange.Added => PublishChargeAsync(entity, ct),
        CrudChange.Deleted => PublishWaivedAsync(entity, ct),
        _ => Task.CompletedTask,
    };

    /// <summary>Báo dịch vụ thanh toán số còn phải thu của phiếu (0 = không còn gì phải thu). Gọi trước SaveChanges để event vào outbox cùng transaction.</summary>
    private async Task PublishChargeAsync(FineTicket ticket, CancellationToken ct, string? justRecordedCode = null)
    {
        if (ticket.PublicId == Guid.Empty) ticket.PublicId = Guid.CreateVersion7();
        var lastQr = justRecordedCode ?? (ticket.Id == 0 ? null : await LastQrCodeAsync(ticket.Id, ct));
        await publisher.Publish(new ChargeIssued
        {
            TenantId = tenantContext.RequireTenantId(), Actor = new EventActor(actor.Id, actor.Kind), SourceService = "circulation", SourceId = ticket.PublicId,
            Kind = "FINE_TICKET", ReaderPublicId = ticket.ReaderPublicId, CardNo = ticket.CardNo, Label = $"Phiếu phạt {ticket.Code}",
            Amount = Math.Max(0, Math.Round(ticket.Remaining, 0, MidpointRounding.AwayFromZero)), Version = ticket.Version, LastPaymentCode = lastQr,
        }, ct);
    }

    /// <summary>Mã giao dịch QR gần nhất đã ghi vào sổ của phiếu — payment dùng để biết nguồn đã phản ánh khoản đã thu.</summary>
    private Task<string?> LastQrCodeAsync(long ticketId, CancellationToken ct) =>
        Db.Set<FinePayment>().AsNoTracking().Where(p => p.FineTicketId == ticketId && p.Method == FinePaymentMethod.Qr && p.ExternalRef != null)
            .OrderByDescending(p => p.Id).Select(p => p.ExternalRef).FirstOrDefaultAsync(ct);

    private Task PublishWaivedAsync(FineTicket ticket, CancellationToken ct) => publisher.Publish(new ChargeWaived
    {
        TenantId = tenantContext.RequireTenantId(), Actor = new EventActor(actor.Id, actor.Kind), SourceService = "circulation", SourceId = ticket.PublicId,
        Version = ticket.Version,
    }, ct);

    protected override async Task EnsureDeletableAsync(FineTicket entity, CancellationToken ct)
    {
        if (!entity.IsOpen || entity.Paid > 0)
            throw new BusinessRuleException("FINE_TICKET_LOCKED", $"Phiếu {entity.Code} đã hoàn thành hoặc đã thu tiền — không xoá được (đã thu tiền thì dùng \"Điều chỉnh\").");
        if (await Db.Set<FinePayment>().AnyAsync(p => p.FineTicketId == entity.Id, ct))
            throw new BusinessRuleException("FINE_TICKET_LOCKED", $"Phiếu {entity.Code} đã có khoản thu/điều chỉnh trong sổ thanh toán — không xoá được.");
        ChangeReason.Require(null, auditContext?.Capture().Reason, "xoá phiếu phạt");
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
        var method = (await settings.GetAsync(ct)).OverdueMethod;
        foreach (var loan in candidates)
        {
            if (loan.IsOverdue(now))
            {
                var days = loan.OverdueDays(now, method);
                if (days == 0) continue; // quá hạn theo lịch nhưng toàn ngày được miễn đếm (vd. cuối tuần) — chưa phạt
                var rate = LoanPolicyResource.Resolve(policies, reader.ReaderTypeId, request.CircPlaceId ?? loan.CircPlaceId).FinePerDay;
                ticket.AddLine(overdue, days * rate, loan, null, null, days);
            }
            else
            {
                var reason = lost ?? throw new BusinessRuleException("FINE_REASON_MISSING", "Thiếu lý do phạt \"Mất tài liệu\" — thêm trong danh mục lý do phạt.");
                ticket.AddLine(reason, reason.Amount, loan, null, null, 0);
            }
        }
        AddDocumentFees(ticket, reasons, LoanPolicyResource.Resolve(policies, reader.ReaderTypeId, request.CircPlaceId));
        if (ticket.Lines.Count > MaxLines) throw new BusinessRuleException("FINE_LINES_LIMIT", $"Một phiếu phạt tối đa {MaxLines} dòng.");
        if (created || candidates.Count > 0)
        {
            await Record(ticket, created ? CrudChange.Added : CrudChange.Updated,
                $"Lập phiếu phạt {ticket.Code} — thẻ {reader.CardNo}: thêm {candidates.Count} tài liệu, tổng {Money(ticket.Total)}", ct);
            await PublishChargeAsync(ticket, ct);
            await Db.SaveChangesAsync(ct);
        }
        return await DetailAsync(ticket, policies, ct);
    }

    /// <summary>
    /// Lưu phiếu (monolith: FineTicket/Save): sửa/thêm/xoá dòng (chỉ khi phiếu đang mở), giảm trừ, tiền thu thêm, ghi chú, trạng thái.
    /// Dòng có lý do đổi trạng thái bản sách (Mất) mà lượt mượn còn mở → đóng lượt (không hoàn tác khi xoá dòng).
    /// Thao tác quan trọng bắt buộc có lý do và lưu trước/sau vào nhật ký; vượt thẩm quyền (giảm nhiều, mở lại phiếu) thì KHÔNG áp dụng
    /// gì cả mà tạo yêu cầu chờ người có quyền FINES:approve duyệt.
    /// </summary>
    public async Task<FineSaveOutcome> SaveAsync(Guid publicId, SaveFineTicketRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        return await SaveCoreAsync(ticket, request, approved: false, approvalNote: null, ct);
    }

    /// <summary>Chạy lại một lần lưu đã được duyệt (người duyệt là người thao tác; bỏ qua kiểm tra thẩm quyền).</summary>
    public async Task ApplyApprovedSaveAsync(Guid publicId, SaveFineTicketRequest request, string requestNote, CancellationToken ct)
    {
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        await SaveCoreAsync(ticket, request, approved: true, requestNote, ct);
    }

    private async Task<FineSaveOutcome> SaveCoreAsync(FineTicket ticket, SaveFineTicketRequest request, bool approved, string? approvalNote, CancellationToken ct)
    {
        var reasons = await Db.Set<FineReason>().ToDictionaryAsync(r => r.Code, ct);
        FineReason Reason(string? code) => reasons.TryGetValue((code ?? "").Trim().ToUpperInvariant(), out var r)
            ? r
            : throw new BusinessRuleException("FINE_REASON_INVALID", "Dòng phạt phải chọn lý do phạt hợp lệ.");

        // 1. Phân loại thao tác (chưa đổi gì).
        var reopened = ticket.Status == FineTicketStatus.Done && request.Status == FineTicketStatus.Open;
        var discountChanged = request.Discount != ticket.Discount;
        var paidDelta = request.Paid - ticket.Paid;
        if (paidDelta < 0)
            throw new BusinessRuleException("FINE_PAID_DECREASE", "Không giảm trực tiếp tiền đã nộp — dùng \"Điều chỉnh\" (hoàn tiền) kèm lý do.");
        var deletedIds = (request.DeletedLineIds ?? []).Where(id => ticket.Lines.Any(l => l.Id == id)).ToList();
        var edited = new List<string>();
        foreach (var change in request.Lines ?? [])
            if (change.Id is { } id && ticket.Lines.FirstOrDefault(l => l.Id == id) is { } line
                && (line.ReasonId != Reason(change.ReasonCode).Id || line.Amount != change.Amount))
                edited.Add(line.ReasonCode);
        var addedLines = (request.Lines ?? []).Count(c => c.Id is null);

        // Số còn phải thu (tổng − giảm trừ) giảm bao nhiêu = phần được miễn/giảm, dù bằng cách nào: tăng giảm trừ, hạ số tiền dòng,
        // xoá dòng hay hạ số tiền phiếu thủ công. Phí tự thêm sau đó chỉ làm tổng tăng nên bỏ qua.
        var amounts = ticket.Lines.Where(l => !deletedIds.Contains(l.Id)).ToDictionary(l => l.Id, l => l.Amount);
        foreach (var change in request.Lines ?? [])
            if (change.Id is { } lineId && amounts.ContainsKey(lineId)) amounts[lineId] = change.Amount;
        var newLines = (request.Lines ?? []).Where(c => c.Id is null).ToList();
        var totalAfter = amounts.Count + newLines.Count > 0 ? amounts.Values.Sum() + newLines.Sum(c => c.Amount) : request.ManualAmount ?? ticket.ManualAmount ?? 0;
        var owedBefore = ticket.Total - ticket.Discount;
        var waived = owedBefore - (totalAfter - request.Discount);

        // Dòng lý do "Mất" sắp đóng lượt mượn đang mở nào (tính trước khi đổi gì để biết có cần lý do/duyệt không).
        var lostCodes = reasons.Values.Where(r => r.ItemStatus == Loan.LostStatus).Select(r => r.Code).ToHashSet();
        static string CodeOf(string? c) => (c ?? "").Trim().ToUpperInvariant();
        var lostLoanIds = new HashSet<Guid>();
        foreach (var line in ticket.Lines.Where(l => !deletedIds.Contains(l.Id) && l.LoanPublicId is not null))
        {
            var code = (request.Lines ?? []).FirstOrDefault(c => c.Id == line.Id)?.ReasonCode is { } changed ? CodeOf(changed) : line.ReasonCode;
            if (lostCodes.Contains(code)) lostLoanIds.Add(line.LoanPublicId!.Value);
        }
        foreach (var c in newLines.Where(c => !string.IsNullOrWhiteSpace(c.Barcode) && lostCodes.Contains(CodeOf(c.ReasonCode))))
        {
            var key = ItemReplica.Key(c.Barcode!);
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToUpper() sang upper() của SQL
            var found = await Db.Set<Loan>().Where(x => x.ReaderPublicId == ticket.ReaderPublicId && x.ReturnedAt == null && x.Barcode.ToUpper() == key)
                .Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct);
#pragma warning restore CA1862, CA1304, CA1311
            if (found is { } loanId) lostLoanIds.Add(loanId);
        }
        var closing = lostLoanIds.Count == 0 ? 0 : await Db.Set<Loan>().CountAsync(x => lostLoanIds.Contains(x.PublicId) && x.ReturnedAt == null, ct);
        var sensitive = reopened || discountChanged || waived > 0 || deletedIds.Count > 0 || edited.Count > 0 || closing > 0;
        var headerReason = auditContext?.Capture().Reason;
        var reason = ChangeReason.Optional(request.Reason, headerReason);
        if (sensitive) reason = ChangeReason.Require(request.Reason, headerReason, "thay đổi phiếu phạt (giảm trừ, sửa/xoá dòng, mở lại phiếu)");

        var before = Snapshot(ticket);

        // 2. Thao tác nào vượt thẩm quyền của người đang thao tác? (Người có quyền duyệt thì không loại nào vượt.) Có → chỉ lập yêu cầu, chưa đổi gì.
        // Khi duyệt, người duyệt phải đủ quyền cho MỌI loại trong lần lưu này — thiếu thì từ chối, không áp dụng nửa vời.
        var triggered = new List<(string Kind, string Name)>();
        if (reopened && await approvals.RequiresAsync(FineApprovals.Reopen, null, null, null, ct)) triggered.Add((FineApprovals.Reopen, "mở lại phiếu"));
        if (waived > 0 && await approvals.RequiresAsync(FineApprovals.Discount, waived, ApprovalService.Percent(waived, owedBefore), null, ct))
            triggered.Add((FineApprovals.Discount, "miễn/giảm"));
        if (closing > 0 && await approvals.RequiresAsync(LoanApprovals.CloseLost, null, null, closing, ct)) triggered.Add((LoanApprovals.CloseLost, "đóng lượt mượn vì mất"));
        if (triggered.Count > 0 && approved)
            throw new BusinessRuleException("APPROVAL_FORBIDDEN",
                $"Yêu cầu này còn gồm thao tác ({string.Join(", ", triggered.Select(t => t.Name))}) mà bạn không có quyền duyệt — cần người đủ quyền duyệt.", 403);
        if (triggered.Count > 0)
        {
            // Tiền thu thêm phải ghi nhận ngay bởi đúng người thu — không để chờ duyệt rồi bị gán cho người duyệt.
            if (paidDelta > 0)
                throw new BusinessRuleException("FINE_PAY_WITH_APPROVAL", "Ghi nhận khoản thu trước (lưu riêng), rồi mới gửi yêu cầu miễn/giảm, mở lại phiếu hoặc báo mất.");
            var after = before with { Status = request.Status, Total = totalAfter, Discount = request.Discount, Remaining = totalAfter - request.Discount - ticket.Paid, Note = request.Note?.Trim() };
            var parts = new[]
            {
                reopened ? "mở lại phiếu đã hoàn thành" : null,
                waived > 0 ? $"miễn/giảm {ApprovalService.Money(waived)} (còn phải thu {ApprovalService.Money(owedBefore)} → {ApprovalService.Money(totalAfter - request.Discount)})" : null,
                closing > 0 ? $"đóng {closing} lượt mượn vì mất tài liệu" : null,
                deletedIds.Count + edited.Count > 0 ? $"sửa/xoá {deletedIds.Count + edited.Count} dòng phạt" : null,
            };
            var summary = $"Phiếu {ticket.Code}: " + string.Join(", ", parts.Where(x => x is not null));
            var pending = await approvals.SubmitAsync(triggered[0].Kind, ticket.PublicId, $"{ticket.Code} — thẻ {ticket.CardNo}", summary, request with { Reason = reason },
                ToObject(before), ToObject(after), reason!, waived > 0 ? waived : null, closing > 0 ? closing : null, ticket.Version, ct);
            return new FineSaveOutcome(null, pending);
        }

        // 3. Áp dụng.
        if (reopened) ticket.Settle(null, ticket.ManualAmount, ticket.Discount, ticket.Note, FineTicketStatus.Open);

        foreach (var id in deletedIds)
            if (ticket.Lines.FirstOrDefault(l => l.Id == id) is { } line) ticket.RemoveLine(line);

        foreach (var change in request.Lines ?? [])
        {
            if (change.Id is { } id)
            {
                var line = ticket.Lines.FirstOrDefault(l => l.Id == id) ?? throw new NotFoundException("Dòng phạt", id);
                var lineReason = Reason(change.ReasonCode);
                if (line.ReasonId == lineReason.Id && line.Amount == change.Amount) continue;
                if (!ticket.IsOpen) throw new BusinessRuleException("FINE_TICKET_DONE", $"Phiếu {ticket.Code} đã hoàn thành, không sửa dòng phạt được.");
                ticket.ChangeLine(line, lineReason, change.Amount);
            }
            else
            {
                await AddManualLineAsync(ticket, Reason(change.ReasonCode), change, ct);
            }
        }
        if (ticket.IsOpen)
        {
            var readerType = await Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == ticket.ReaderPublicId).Select(r => r.ReaderTypeId).FirstOrDefaultAsync(ct);
            AddDocumentFees(ticket, reasons.Values, LoanPolicyResource.Resolve(await Db.Set<LoanPolicy>().AsNoTracking().ToListAsync(ct), readerType, null));
        }
        if (ticket.Lines.Count > MaxLines) throw new BusinessRuleException("FINE_LINES_LIMIT", $"Một phiếu phạt tối đa {MaxLines} dòng.");

        // Dòng lý do "Mất" mà lượt mượn còn mở (kể cả dòng tạo sẵn khi gom phiếu) → đóng lượt, báo holdings — như monolith mỗi lần lưu.
        var now = clock.GetUtcNow();
        var lostLoans = new List<string>();
        var lostReasons = reasons.Values.Where(r => r.ItemStatus == Loan.LostStatus).Select(r => r.Id).ToHashSet();
        var toClose = new List<Loan>();
        foreach (var line in ticket.Lines.Where(l => l.LoanPublicId is not null && lostReasons.Contains(l.ReasonId)))
        {
            var loan = await Db.Set<Loan>().FirstOrDefaultAsync(x => x.PublicId == line.LoanPublicId && x.ReturnedAt == null, ct);
            if (loan is not null) toClose.Add(loan);
        }
        foreach (var loan in toClose)
        {
            loan.CloseAsLost(now, actor.Id, auditContext?.Capture().ActorName, reason);
            await loans.PublishAsync(loan, ct);
            lostLoans.Add(loan.Barcode);
        }

        var discountBefore = ticket.Discount;
        ticket.Settle(request.FineDate, request.ManualAmount ?? ticket.ManualAmount, request.Discount, request.Note, request.Status);
        if (paidDelta > 0) ticket.AddPayment(paidDelta, FinePaymentMethod.Cash, null, reason, auditContext?.Capture().ActorName, now);
        // Chỉ kiểm khi lần lưu này đụng tới tiền — phiếu cũ đã âm từ trước vẫn sửa được ghi chú.
        if ((paidDelta > 0 || discountBefore != ticket.Discount || addedLines > 0 || edited.Count > 0 || deletedIds.Count > 0) && ticket.Remaining < 0)
            throw new BusinessRuleException("FINE_OVERPAID", $"Đã nộp vượt số phải thu của phiếu {ticket.Code} (còn {ApprovalService.Money(ticket.Remaining)}).");

        await Record(ticket, CrudChange.Updated,
            $"Lưu phiếu phạt {ticket.Code} — tổng {Money(ticket.Total)}, giảm {Money(ticket.Discount)}, đã nộp {Money(ticket.Paid)}"
            + (ticket.IsOpen ? "" : ", hoàn thành") + (lostLoans.Count > 0 ? $"; báo mất {string.Join(", ", lostLoans)}" : "")
            + (approved ? $" (đã được duyệt{(string.IsNullOrWhiteSpace(approvalNote) ? "" : ": " + approvalNote)})" : ""),
            ct, CrudDiff.Compute(before, Snapshot(ticket)), reason);
        await PublishChargeAsync(ticket, ct);
        await Db.SaveChangesAsync(ct);
        return new FineSaveOutcome(await DetailAsync(ticket, null, ct), null);
    }

    /// <summary>Hoàn tiền / điều chỉnh giảm số đã thu: ghi một khoản âm có lý do (số cũ giữ nguyên). Chờ duyệt nếu người thao tác không có FINES:approve.</summary>
    public async Task<FineSaveOutcome> AdjustAsync(Guid publicId, AdjustFinePaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        return await AdjustCoreAsync(ticket, request, approved: false, ct);
    }

    public async Task ApplyApprovedAdjustAsync(Guid publicId, AdjustFinePaymentRequest request, CancellationToken ct)
    {
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);
        await AdjustCoreAsync(ticket, request, approved: true, ct);
    }

    private async Task<FineSaveOutcome> AdjustCoreAsync(FineTicket ticket, AdjustFinePaymentRequest request, bool approved, CancellationToken ct)
    {
        if (request.Amount >= 0) throw new BusinessRuleException("AMOUNT_INVALID", "Khoản điều chỉnh phải là số âm (hoàn tiền / giảm số đã thu).");
        if (-request.Amount > ticket.Paid)
            throw new BusinessRuleException("FINE_ADJUST_TOO_BIG", $"Phiếu {ticket.Code} mới thu {Money(ticket.Paid)} — không điều chỉnh âm hơn số đã thu.");
        var reason = ChangeReason.Require(request.Reason, auditContext?.Capture().Reason, "điều chỉnh số đã thu");
        var before = Snapshot(ticket);
        if (!approved && await approvals.RequiresAsync(FineApprovals.Adjust, -request.Amount, null, null, ct))
        {
            var after = before with { Paid = before.Paid + request.Amount, Remaining = before.Remaining - request.Amount };
            var summary = $"Phiếu {ticket.Code}: điều chỉnh {Money(request.Amount)} (đã thu {Money(ticket.Paid)} → {Money(ticket.Paid + request.Amount)})";
            var pending = await approvals.SubmitAsync(FineApprovals.Adjust, ticket.PublicId, $"{ticket.Code} — thẻ {ticket.CardNo}", summary, request with { Reason = reason },
                ToObject(before), ToObject(after), reason, -request.Amount, null, ticket.Version, ct);
            return new FineSaveOutcome(null, pending);
        }
        ticket.AddPayment(request.Amount, FinePaymentMethod.Adjustment, request.Note, reason, auditContext?.Capture().ActorName, clock.GetUtcNow());
        await Record(ticket, CrudChange.Reversed,
            $"Điều chỉnh phiếu phạt {ticket.Code}: {Money(request.Amount)} (đã thu còn {Money(ticket.Paid)})", ct, CrudDiff.Compute(before, Snapshot(ticket)), reason);
        await PublishChargeAsync(ticket, ct);
        await Db.SaveChangesAsync(ct);
        return new FineSaveOutcome(await DetailAsync(ticket, null, ct), null);
    }

    /// <summary>
    /// Tiền đã về qua cổng thanh toán (<see cref="PaymentSucceeded"/>): ghi MỘT khoản thu QR vào sổ (mã giao dịch là khoá chống trùng — event
    /// gửi lại không ghi hai lần), hoàn thành phiếu khi hết nợ. Ghi đủ số tiền thực nhận kể cả khi phiếu đã đổi (nhận dư thì còn lại âm, cán bộ hoàn bằng Điều chỉnh).
    /// </summary>
    public async Task<bool> ApplyPaymentAsync(PaymentSucceeded e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!string.Equals(e.SourceService, "circulation", StringComparison.OrdinalIgnoreCase) || e.Kind != "FINE_TICKET") return false;
        if (await Db.Set<FinePayment>().AnyAsync(p => p.ExternalRef == e.TransactionCode, ct)) return true;
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == e.SourceId, ct);
        if (ticket is null) return false;
        var before = Snapshot(ticket);
        var owed = Math.Max(0, ticket.Remaining);
        ticket.AddPayment(e.Amount, FinePaymentMethod.Qr, $"Mã giao dịch {e.TransactionCode} ({e.Provider})", e.Late ? "Tiền về sau khi mã QR hết hạn" : null, "Cổng thanh toán", e.PaidAt, e.TransactionCode);
        if (ticket.IsOpen && ticket.Remaining <= 0) ticket.Settle(null, ticket.ManualAmount, ticket.Discount, ticket.Note, FineTicketStatus.Done);
        await Record(ticket, CrudChange.Updated,
            $"Thu {Money(e.Amount)} qua {e.Provider} (mã {e.TransactionCode}) — phiếu {ticket.Code}" + (ticket.IsOpen ? "" : ", hoàn thành"), ct, CrudDiff.Compute(before, Snapshot(ticket)));
        await PublishChargeAsync(ticket, ct, e.TransactionCode);
        // Tiền thực nhận vượt số còn phải thu (quầy thu tiền mặt song song, phiếu vừa được giảm…): tiền đã vào sổ, nên lập đề xuất hoàn phần dư —
        // người có FINES:approve duyệt ở "Chờ phê duyệt"; không tự hoàn tiền.
        var excess = e.Amount - owed;
        if (excess > 0)
        {
            var after = Snapshot(ticket);
            var proposal = new AdjustFinePaymentRequest(-excess, $"Hoàn phần thu dư {Money(excess)} (mã {e.TransactionCode} về khi phiếu chỉ còn nợ {Money(owed)})");
            await approvals.SubmitAsync(FineApprovals.Adjust, ticket.PublicId, $"{ticket.Code} — thẻ {ticket.CardNo}",
                $"Phiếu {ticket.Code}: thu dư {Money(excess)} qua {e.Provider} — đề xuất hoàn", proposal, ToObject(before), ToObject(after with { Paid = after.Paid - excess, Remaining = after.Remaining + excess }),
                proposal.Reason, excess, null, ticket.Version, ct, requestedByName: "Cổng thanh toán", systemProposal: true);
        }
        await Db.SaveChangesAsync(ct);
        return true;
    }

    public const int MaxStatePage = 1000;

    /// <summary>
    /// Phiếu còn nợ theo id tăng dần sau <paramref name="afterId"/>, dưới dạng <see cref="ChargeIssued"/> — payment dựng khoản thu lần đầu / sau khi đơn vị mua
    /// phân hệ thanh toán (phiếu lập trước đó chưa từng phát sự kiện tới payment).
    /// </summary>
    public async Task<(IReadOnlyList<ChargeIssued> Items, long? Next)> OpenChargesAsync(long afterId, int take, CancellationToken ct)
    {
        var size = Math.Clamp(take, 1, MaxStatePage);
        var tickets = await Set.AsNoTracking().Where(t => t.Id > afterId && t.Remaining > 0).OrderBy(t => t.Id).Take(size).ToListAsync(ct);
        var tenantId = tenantContext.RequireTenantId();
        var ids = tickets.Select(t => t.Id).ToList();
        var codes = (await Db.Set<FinePayment>().AsNoTracking().Where(p => ids.Contains(p.FineTicketId) && p.Method == FinePaymentMethod.Qr && p.ExternalRef != null)
            .Select(p => new { p.FineTicketId, p.Id, p.ExternalRef }).ToListAsync(ct))
            .GroupBy(p => p.FineTicketId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First().ExternalRef);
        var items = tickets.Select(t => new ChargeIssued
        {
            TenantId = tenantId, SourceService = "circulation", SourceId = t.PublicId, Kind = "FINE_TICKET", ReaderPublicId = t.ReaderPublicId, CardNo = t.CardNo,
            Label = $"Phiếu phạt {t.Code}", Amount = Math.Round(t.Remaining, 0, MidpointRounding.AwayFromZero), Version = t.Version,
            LastPaymentCode = codes.GetValueOrDefault(t.Id),
        }).ToList();
        return (items, tickets.Count == size ? tickets[^1].Id : null);
    }

    public async Task<long?> VersionAsync(Guid publicId, CancellationToken ct) =>
        await Set.AsNoTracking().Where(t => t.PublicId == publicId).Select(t => (long?)t.Version).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Kho tìm thấy lại bản sách bạn đọc đã làm mất (<see cref="ItemLossReversed"/>): với mỗi dòng phạt "Mất" của lượt mượn đó, lập MỘT yêu cầu
    /// chờ duyệt (không tự hoàn tiền, không tự mở lượt mượn). Đã có yêu cầu chờ cho dòng đó thì bỏ qua (event gửi trùng).
    /// </summary>
    public async Task<int> ProposeLostRefundAsync(ItemLossReversed e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var loanIds = await Db.Set<Loan>().AsNoTracking().Where(l => l.ItemPublicId == e.ItemPublicId && l.CloseKind == Loan.CloseLost).Select(l => l.PublicId).ToListAsync(ct);
        if (loanIds.Count == 0) return 0;
        var lostReasonIds = await Db.Set<FineReason>().AsNoTracking().Where(r => r.ItemStatus == Loan.LostStatus).Select(r => r.Id).ToListAsync(ct);
        var lines = await Db.Set<FineLine>().AsNoTracking().Where(l => l.LoanPublicId != null && loanIds.Contains(l.LoanPublicId.Value) && lostReasonIds.Contains(l.ReasonId)).ToListAsync(ct);
        var created = 0;
        foreach (var line in lines)
        {
            var ticket = await Set.AsNoTracking().Include(t => t.Lines).FirstOrDefaultAsync(t => t.Id == line.FineTicketId, ct);
            if (ticket is null) continue;
            var marker = $"[dòng {line.Id}]";
            var pendingKind = FineApprovals.LostRefund;
            if (await Db.Set<ChangeRequest>().AnyAsync(r => r.TargetPublicId == ticket.PublicId && r.Kind == pendingKind && r.Status == ApprovalStatus.Pending && r.Summary.Contains(marker), ct)) continue;
            var before = Snapshot(ticket);
            var after = before with { Total = ticket.Total - line.Amount, Remaining = ticket.Total - line.Amount - ticket.Discount - ticket.Paid };
            await approvals.SubmitAsync(pendingKind, ticket.PublicId, $"{ticket.Code} — thẻ {ticket.CardNo}",
                $"Phiếu {ticket.Code}: tìm thấy lại ĐKCB {e.Barcode} → bỏ dòng phạt mất {ApprovalService.Money(line.Amount)} {marker}", new LostRefundPayloadDto(line.Id),
                ToObject(before), ToObject(after), e.Reason, line.Amount, null, ticket.Version, ct, requestedByName: e.ActorName ?? "Kho (tìm thấy lại sách)", systemProposal: true);
            created++;
        }
        return created;
    }

    /// <summary>
    /// Duyệt đề xuất hoàn dòng phạt mất: bỏ dòng khỏi phiếu (phiếu đã hoàn thành thì mở lại để sửa), bỏ phí xử lý/vận chuyển nếu không còn dòng mất,
    /// hoàn phần đã thu vượt số phải thu bằng khoản điều chỉnh âm, rồi hoàn thành lại nếu đủ tiền. Mọi bước ghi nhật ký có lý do.
    /// </summary>
    public async Task ApplyApprovedLostRefundAsync(Guid ticketPublicId, long lineId, string reason, CancellationToken ct)
    {
        var ticket = await Set.Include(t => t.Lines).FirstOrDefaultAsync(t => t.PublicId == ticketPublicId, ct) ?? throw new NotFoundException(EntityName, ticketPublicId);
        var line = ticket.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new ConflictException("FINE_LINE_GONE", "Dòng phạt mất đã không còn trên phiếu.");
        var before = Snapshot(ticket);
        var wasDone = !ticket.IsOpen;
        if (wasDone) ticket.Settle(null, ticket.ManualAmount, ticket.Discount, ticket.Note, FineTicketStatus.Open);
        ticket.RemoveLine(line);
        var reasons = await Db.Set<FineReason>().AsNoTracking().ToListAsync(ct);
        var lostIds = reasons.Where(r => r.ItemStatus == Loan.LostStatus).Select(r => r.Id).ToHashSet();
        if (!ticket.Lines.Any(l => lostIds.Contains(l.ReasonId)))
        {
            var feeIds = reasons.Where(r => r.Code is FineReason.TechFeeCode or FineReason.ShippingFeeCode).Select(r => r.Id).ToHashSet();
            foreach (var fee in ticket.Lines.Where(l => feeIds.Contains(l.ReasonId) && l.LoanPublicId is null).ToList()) ticket.RemoveLine(fee);
        }
        var discount = Math.Min(ticket.Discount, ticket.Total);
        var refund = Math.Max(0, ticket.Paid - (ticket.Total - discount));
        if (refund > 0) ticket.AddPayment(-refund, FinePaymentMethod.Adjustment, "Hoàn do tìm thấy lại tài liệu mất", reason, auditContext?.Capture().ActorName, clock.GetUtcNow());
        ticket.Settle(null, ticket.ManualAmount, discount, ticket.Note, wasDone && ticket.Total - discount - ticket.Paid <= 0 ? FineTicketStatus.Done : FineTicketStatus.Open);
        await Record(ticket, CrudChange.Reversed, $"Hoàn dòng phạt mất khi tìm thấy tài liệu — phiếu {ticket.Code}" + (refund > 0 ? $", hoàn {Money(refund)}" : ""), ct,
            CrudDiff.Compute(before, Snapshot(ticket)), reason);
        await PublishChargeAsync(ticket, ct);
        await Db.SaveChangesAsync(ct);
    }



    private sealed record FineSnapshot(int Status, decimal Total, decimal Discount, decimal Paid, decimal Remaining, string? Note, decimal? ManualAmount,
        IReadOnlyList<string> Lines);

    private static FineSnapshot Snapshot(FineTicket t) => new(t.Status, t.Total, t.Discount, t.Paid, t.Remaining, t.Note, t.ManualAmount,
        [.. t.Lines.OrderBy(l => l.Id).Select(l => $"{l.ReasonCode} {l.Amount:0.##}" + (l.Barcode is { } b ? $" [{b}]" : ""))]);

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    private static JsonObject ToObject(object snapshot) => JsonSerializer.SerializeToNode(snapshot, SnapshotJson)!.AsObject();

    /// <summary>
    /// Nợ tài liệu (phiếu có dòng lý do đổi bản sách sang "Mất") → cộng phí xử lý kỹ thuật + vận chuyển của chính sách, MỘT lần cho phiếu
    /// (ELIB-LRC: ComputeTotal khi OwesDocument). Phí bằng 0, lý do đã bị xoá khỏi danh mục, hoặc phiếu đã có dòng đó → bỏ qua.
    /// </summary>
    private static void AddDocumentFees(FineTicket ticket, IEnumerable<FineReason> reasons, LoanPolicy policy)
    {
        var all = reasons.ToList();
        var lost = all.Where(r => r.ItemStatus == Loan.LostStatus).Select(r => r.Id).ToHashSet();
        if (!ticket.IsOpen || !ticket.Lines.Any(l => lost.Contains(l.ReasonId))) return;
        foreach (var (code, amount) in new[] { (FineReason.TechFeeCode, policy.TechFee), (FineReason.ShippingFeeCode, policy.ShippingFee) })
        {
            if (amount <= 0 || all.FirstOrDefault(r => r.Code == code) is not { } reason || ticket.Lines.Any(l => l.ReasonId == reason.Id)) continue;
            ticket.AddLine(reason, amount, null, null, null, 0);
        }
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
        if (loan is not null) return ticket.AddLine(reason, change.Amount, loan, null, null, loan.OverdueDays(clock.GetUtcNow(), (await settings.GetAsync(ct)).OverdueMethod));
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
        var payments = await Db.Set<FinePayment>().AsNoTracking().Where(p => p.FineTicketId == ticket.Id).OrderBy(p => p.PaidAt).ThenBy(p => p.Id)
            .Select(p => new FinePaymentDto(p.Id, p.Amount, p.Method, p.Note, p.Reason, p.ByName, p.PaidAt)).ToListAsync(ct);
        return new FineTicketDetail(dto, lines, LoanPolicyResource.Resolve(policies, readerType, null).FinePerDay, payments, await approvals.PendingForAsync(ticket.PublicId, ct));
    }

    private async Task<PatronReplica> RequireReaderAsync(string? cardNo, CancellationToken ct) =>
        await replicas.ReaderAsync(cardNo ?? "", ct)
        ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{cardNo?.Trim()}'.", 404);

    private Task Record(FineTicket ticket, CrudChange change, string summary, CancellationToken ct, JsonObject? changes = null, string? reason = null)
    {
        if (ticket.PublicId == Guid.Empty) ticket.PublicId = Guid.CreateVersion7();
        return AuditSink?.RecordAsync(new CrudAuditEntry(nameof(FineTicket), EntityName, ticket.PublicId, change, summary, changes, reason), ct) ?? Task.CompletedTask;
    }

    private static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    private static DateTimeOffset VietnamStart(DateOnly date) => Loan.VietnamStart(date);
}

/// <summary>Nội dung yêu cầu hoàn dòng phạt mất (đọc lại lúc duyệt).</summary>
public sealed record LostRefundPayloadDto(long LineId);

/// <summary>Áp dụng yêu cầu phiếu phạt đã duyệt: chạy lại đúng thao tác gốc dưới danh nghĩa người duyệt.</summary>
public sealed class FineChangeHandler(FineTicketResource fines) : IChangeRequestHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool CanHandle(string kind) => kind.StartsWith("FINE_", StringComparison.Ordinal) || kind == LoanApprovals.CloseLost;

    public Task<long?> CurrentVersionAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Đề xuất hoàn dòng phạt mất tự kiểm lại dòng còn không lúc áp dụng — phiếu đổi vì việc khác (thu tiền…) không làm đề xuất hết hạn.
        return request.Kind == FineApprovals.LostRefund ? Task.FromResult<long?>(request.TargetVersion) : fines.VersionAsync(request.TargetPublicId, cancellationToken);
    }

    public async Task ApplyAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind == FineApprovals.LostRefund)
            await fines.ApplyApprovedLostRefundAsync(request.TargetPublicId, JsonSerializer.Deserialize<LostRefundPayloadDto>(request.PayloadJson, Json)!.LineId, request.Reason, cancellationToken);
        else if (request.Kind == FineApprovals.Adjust)
            await fines.ApplyApprovedAdjustAsync(request.TargetPublicId, JsonSerializer.Deserialize<AdjustFinePaymentRequest>(request.PayloadJson, Json)!, cancellationToken);
        else
            await fines.ApplyApprovedSaveAsync(request.TargetPublicId, JsonSerializer.Deserialize<SaveFineTicketRequest>(request.PayloadJson, Json)!, request.Reason,
                cancellationToken);
    }
}
