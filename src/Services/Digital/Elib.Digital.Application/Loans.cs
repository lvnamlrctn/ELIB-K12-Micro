using System.Globalization;
using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

/// <summary>Mã mẫu tin của tài liệu số (notification giữ nội dung mẫu — xem Notification's DefaultTemplates).</summary>
public static class EbookTemplates
{
    public const string HoldReady = "EBOOK_HOLD_READY";
    public const string LoanExpired = "EBOOK_LOAN_EXPIRED";
    public const string SubmissionApproved = "EBOOK_SUBMISSION_APPROVED";
    public const string SubmissionRejected = "EBOOK_SUBMISSION_REJECTED";
    public const string ReviewApproved = "EBOOK_REVIEW_APPROVED";
    public const string ReviewRejected = "EBOOK_REVIEW_REJECTED";
}

internal static class EbookDates
{
    public static string Text(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>Gửi tin cho bạn đọc qua notification (NotificationRequested, outbox) — đúng khuôn Circulation's ReaderNotifier.</summary>
public sealed class EbookReaderNotifier(IPublishEndpoint publisher, ITenantContext tenant)
{
    /// <summary>Gửi cho người nhập email (đánh giá ẩn danh — không có hồ sơ bạn đọc).</summary>
    public Task NotifyEmailAsync(string? email, string? name, string templateCode, IReadOnlyDictionary<string, string> data, string deduplicationKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return Task.CompletedTask;
        return publisher.Publish(new NotificationRequested
        {
            TenantId = tenant.RequireTenantId(),
            TemplateCode = templateCode,
            Recipient = new NotificationRecipient(null, null, email, null, name),
            Data = new Dictionary<string, string>(data, StringComparer.Ordinal),
            DeduplicationKey = deduplicationKey,
        }, ct);
    }

    public Task NotifyAsync(PatronReplica? reader, string templateCode, IReadOnlyDictionary<string, string> data, string deduplicationKey, CancellationToken ct)
    {
        if (reader is null || (string.IsNullOrWhiteSpace(reader.Email) && string.IsNullOrWhiteSpace(reader.Phone))) return Task.CompletedTask;
        var values = new Dictionary<string, string>(data, StringComparer.Ordinal) { ["card_no"] = reader.CardNo };
        return publisher.Publish(new NotificationRequested
        {
            TenantId = tenant.RequireTenantId(),
            TemplateCode = templateCode,
            Recipient = new NotificationRecipient(null, null, reader.Email, reader.Phone, reader.FullName),
            Data = values,
            DeduplicationKey = deduplicationKey,
        }, ct);
    }
}

/// <summary>
/// Chính sách áp dụng khi mượn/đặt trước — đúng thứ tự ưu tiên của LRC (EbookItemLoanRepository/EbookItemReservationRepository),
/// không phải cách diễn giải riêng: PolicyDigital theo loại bạn đọc được ưu tiên trước PolicyDigitalByCollection.
/// </summary>
internal static class EbookPolicies
{
    /// <summary>LRC: IsReadAllowedByPolicyAsync — miễn phí hoặc không thuộc bộ sưu tập luôn được; bạn đọc chưa có loại luôn được;
    /// có chính sách theo (loại, bộ sưu tập) thì theo Read của chính sách đó, không có chính sách thì được.</summary>
    public static bool ReadAllowed(EbookItem item, long? readerTypeId, PolicyDigitalByCollection? byCollection) =>
        item.Free || item.CollectionId is null || readerTypeId is null || byCollection is null || byCollection.Read;

    /// <summary>LRC: GetEffectiveMaxDocumentAsync — PolicyDigital có dòng và có Maxdocument thì dùng ngay (0 cũng là giới hạn hợp lệ,
    /// chỉ null mới rơi xuống bước sau); không thì theo PolicyDigitalByCollection của (loại, bộ sưu tập). Null = không giới hạn.</summary>
    public static int? EffectiveMaxDocument(long? readerTypeId, long? collectionId, PolicyDigital? general, PolicyDigitalByCollection? byCollection)
    {
        if (readerTypeId is null) return null;
        if (general is not null && general.MaxDocument is { } g) return g;
        return collectionId is null ? null : byCollection?.MaxDocument;
    }

    /// <summary>LRC: GetEffectiveOfflineDaysAsync — tài liệu tự đặt (>0) ưu tiên; không có thì theo PolicyDigitalByCollection của
    /// (loại, bộ sưu tập), chỉ nhận giá trị &gt;0. Null = không giới hạn (mượn vĩnh viễn, không tự hết hạn).</summary>
    public static int? EffectiveOfflineDays(EbookItem item, long? readerTypeId, PolicyDigitalByCollection? byCollection)
    {
        if (item.OfflineDays is > 0) return item.OfflineDays;
        if (item.CollectionId is null || readerTypeId is null) return null;
        return byCollection?.OfflineDays is > 0 ? byCollection.OfflineDays : null;
    }

    /// <summary>LRC: GetEffectiveReservationExpireHoursAsync — chỉ theo PolicyDigital của loại bạn đọc; cột cùng tên ở
    /// PolicyDigitalByCollection không được dùng ở luồng này (LRC cũng không dùng, chỉ lưu). Null/≤0 = loại này không được đặt trước.</summary>
    public static int? EffectiveReservationExpireHours(long? readerTypeId, PolicyDigital? general) =>
        readerTypeId is null ? null : general?.ReservationExpireHours;
}

/// <summary>Chuyển bản trống cho người đặt trước sớm nhất khi có chỗ — dùng chung bởi Borrow/Return/Recall/Cancel/job hết hạn.</summary>
public sealed class EbookReservationAllocator(ICrudDbContext db, EbookReaderNotifier notifier, TimeProvider clock)
{
    public const int DefaultReadyHours = 48;

    /// <summary>LRC: PromoteNextIfSlotAvailableAsync. Hết hạn Ready quá giờ trước, rồi lặp thăng hạng Pending cũ nhất khi còn chỗ.</summary>
    public async Task<int> PromoteAsync(long ebookItemId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var overdue = await db.Set<EbookItemReservation>()
            .Where(r => r.EbookItemId == ebookItemId && r.Status == EbookReservationStatus.Ready && r.ReadyExpiresAt < now).ToListAsync(ct);
        foreach (var r in overdue) r.Expire(now);
        if (overdue.Count > 0) await db.SaveChangesAsync(ct);

        var item = await db.Set<EbookItem>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == ebookItemId, ct);
        if (item?.PrintCopies is not > 0) return 0;

        var promoted = 0;
        while (await OccupiedCountAsync(ebookItemId, now, ct) < item.PrintCopies.Value)
        {
            var next = await db.Set<EbookItemReservation>().Where(r => r.EbookItemId == ebookItemId && r.Status == EbookReservationStatus.Pending)
                .OrderBy(r => r.Id).FirstOrDefaultAsync(ct);
            if (next is null) break;

            var reader = await db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == next.ReaderPublicId, ct);
            var general = await GeneralPolicyAsync(reader?.ReaderTypeId, ct);
            var hours = EbookPolicies.EffectiveReservationExpireHours(reader?.ReaderTypeId, general);
            var window = hours is > 0 ? TimeSpan.FromHours(hours.Value) : TimeSpan.FromHours(DefaultReadyHours);
            now = clock.GetUtcNow();
            next.MakeReady(now, now.Add(window));

            // Publish TRƯỚC SaveChanges — NotificationRequested đi qua outbox, chỉ gửi được nếu cùng transaction lưu kèm thay đổi này.
            await notifier.NotifyAsync(reader, EbookTemplates.HoldReady, new Dictionary<string, string>
            {
                ["title"] = item.Title,
                ["ready_expires_at"] = EbookDates.Text(next.ReadyExpiresAt!.Value),
            }, $"ebook-hold-ready:{ebookItemId}:{next.Id}", ct);

            await db.SaveChangesAsync(ct); // từng dòng: đọc lại số chỗ đang chiếm để không giữ cùng 1 chỗ cho 2 người
            promoted++;
        }
        return promoted;
    }

    public async Task<int> OccupiedCountAsync(long ebookItemId, DateTimeOffset now, CancellationToken ct) =>
        await db.Set<EbookItemLoan>().CountAsync(l => l.EbookItemId == ebookItemId && l.Status == EbookLoanStatus.Active
            && (l.ExpiresAt == null || l.ExpiresAt > now), ct)
        + await db.Set<EbookItemReservation>().CountAsync(r => r.EbookItemId == ebookItemId && r.Status == EbookReservationStatus.Ready
            && (r.ReadyExpiresAt == null || r.ReadyExpiresAt > now), ct);

    public async Task<PolicyDigital?> GeneralPolicyAsync(long? readerTypeId, CancellationToken ct) =>
        PolicyDigitalResource.Resolve(await db.Set<PolicyDigital>().AsNoTracking().ToListAsync(ct), readerTypeId);

    public async Task<PolicyDigitalByCollection?> ByCollectionPolicyAsync(long? readerTypeId, long? collectionId, CancellationToken ct) =>
        collectionId is { } c
            ? PolicyDigitalByCollectionResource.Resolve(await db.Set<PolicyDigitalByCollection>().AsNoTracking().ToListAsync(ct), readerTypeId, c)
            : null;
}

public sealed class EbookLoanSearch : CrudSearch
{
    public Guid? EbookItemPublicId { get; set; }
    public Guid? ReaderPublicId { get; set; }
    public int? LoanStatus { get; set; }
}

public sealed record EbookLoanDto(
    long Id, Guid PublicId, long EbookItemId, Guid EbookItemPublicId, string? Title, Guid ReaderPublicId, string? CardNo, string? ReaderName,
    DateTimeOffset CheckedOutAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastAccessAt, int Status, DateTimeOffset? RecalledAt, long? RecalledBy,
    string? RecallReason)
{
    /// <summary>Bạn đọc tự xem (OPAC) — không lộ ai thu hồi.</summary>
    public EbookLoanDto ForReader() => this with { CardNo = null, ReaderName = null, RecalledBy = null };
}

/// <summary>Request của resource — không dùng (mượn/trả/thu hồi đều qua phương thức nghiệp vụ riêng), chỉ để khớp CrudResource.</summary>
public sealed record EbookLoanOpUnused;

public sealed record RecallLoanRequest(Guid LoanPublicId, string Reason);
public sealed record RecallAllRequest(Guid EbookItemPublicId, string Reason);

/// <summary>
/// Mượn/trả/thu hồi tài liệu số (LRC: EbookItemLoanRepository, quyền DIGITAL_LOANS). Không có Add/Update/Delete chung — chỉ map
/// Search/GetById (tra cứu) và các thao tác nghiệp vụ riêng, như Circulation's LoanResource.
/// </summary>
public sealed class EbookLoanResource(ICrudDbContext db, TimeProvider clock, EbookReservationAllocator allocator)
    : CrudResource<EbookLoanResource, EbookItemLoan, EbookLoanSearch, EbookLoanOpUnused, EbookLoanDto>(db)
{
    protected override string EntityName => "Lượt mượn tài liệu số";

    protected override string? Describe(EbookItemLoan entity) =>
        $"tài liệu #{entity.EbookItemId} — bạn đọc {entity.ReaderPublicId}";

    protected override Expression<Func<EbookItemLoan, EbookLoanDto>> Projection => x => new EbookLoanDto(
        x.Id, x.PublicId, x.EbookItemId, Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.PublicId).FirstOrDefault(),
        Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.Title).FirstOrDefault(), x.ReaderPublicId,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.CardNo).FirstOrDefault(),
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.CheckedOutAt, x.ExpiresAt, x.LastAccessAt, x.Status, x.RecalledAt, x.RecalledBy, x.RecallReason);

    protected override EbookItemLoan Create(EbookLoanOpUnused request) => throw new NotSupportedException("Lượt mượn tạo qua BorrowAsync.");

    protected override void Update(EbookItemLoan entity, EbookLoanOpUnused request) => throw new NotSupportedException("Lượt mượn sửa qua Return/Recall.");

    protected override IQueryable<EbookItemLoan> Filter(IQueryable<EbookItemLoan> query, EbookLoanSearch search)
    {
        if (search.EbookItemPublicId is { } itemPublicId)
        {
            var itemId = Db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId).Select(i => i.Id).FirstOrDefault();
            query = query.Where(x => x.EbookItemId == itemId);
        }
        if (search.ReaderPublicId is { } reader) query = query.Where(x => x.ReaderPublicId == reader);
        if (search.LoanStatus is { } status) query = query.Where(x => x.Status == status);
        return query;
    }

    protected override IOrderedQueryable<EbookItemLoan> Order(IQueryable<EbookItemLoan> query) =>
        query.OrderByDescending(x => x.CheckedOutAt).ThenByDescending(x => x.Id);

    /// <summary>
    /// Mượn hoặc mở lại lượt đang mượn (LRC: CheckoutOrResumeAsync). Thứ tự đúng LRC: chính sách đọc → lượt đang mở (mở lại hoặc
    /// báo hết hạn offline) → thăng hạng tự chữa → bỏ qua kiểm sức chứa nếu đã có Ready của chính mình → sức chứa PrintCopies →
    /// MaxDocument hiệu lực → tạo lượt, hoàn tất đặt trước đang giữ (nếu có).
    /// </summary>
    public async Task<EbookLoanDto> BorrowAsync(Guid itemPublicId, Guid readerPublicId, CancellationToken ct)
    {
        var item = await Db.Set<EbookItem>().FirstOrDefaultAsync(x => x.PublicId == itemPublicId && x.Status == IHasStatus.Active, ct)
            ?? throw new NotFoundException("Tài liệu số", itemPublicId);
        var reader = await Db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == readerPublicId, ct);
        var byCollection = await allocator.ByCollectionPolicyAsync(reader?.ReaderTypeId, item.CollectionId, ct);
        if (!EbookPolicies.ReadAllowed(item, reader?.ReaderTypeId, byCollection))
            throw new BusinessRuleException("EBOOK_READ_DENIED", "Không được phép đọc tài liệu này theo chính sách của thư viện.", 403);

        var now = clock.GetUtcNow();
        var existing = await Set.FirstOrDefaultAsync(l => l.EbookItemId == item.Id && l.ReaderPublicId == readerPublicId && l.Status == EbookLoanStatus.Active, ct);
        if (existing is not null)
        {
            if (existing.IsExpired(now))
                throw new BusinessRuleException("EBOOK_LOAN_EXPIRED", "Đã hết hạn mượn offline, vui lòng mượn lại.", 410);
            existing.RecordAccess(now);
            await Db.SaveChangesAsync(ct);
            return (await ToDtosAsync([existing], ct))[0];
        }

        // Tự chữa: có thể một slot vừa trống (vd. admin tăng PrintCopies) nhưng chưa có hành động Return/Cancel/Recall nào kích hoạt thăng hạng.
        await allocator.PromoteAsync(item.Id, ct);

        var readyReservation = await Db.Set<EbookItemReservation>().FirstOrDefaultAsync(r => r.EbookItemId == item.Id
            && r.ReaderPublicId == readerPublicId && r.Status == EbookReservationStatus.Ready
            && (r.ReadyExpiresAt == null || r.ReadyExpiresAt > now), ct);

        if (readyReservation is null && item.PrintCopies is > 0)
        {
            var occupied = await allocator.OccupiedCountAsync(item.Id, now, ct);
            if (occupied >= item.PrintCopies.Value)
                throw new BusinessRuleException("EBOOK_AT_CAPACITY",
                    $"Tài liệu đã đạt giới hạn {item.PrintCopies.Value} bản đọc đồng thời. Vui lòng đặt trước.", 429);
        }

        var general = await allocator.GeneralPolicyAsync(reader?.ReaderTypeId, ct);
        var maxDocument = EbookPolicies.EffectiveMaxDocument(reader?.ReaderTypeId, item.CollectionId, general, byCollection);
        if (maxDocument is > 0)
        {
            var activeCount = await Set.CountAsync(l => l.ReaderPublicId == readerPublicId && l.Status == EbookLoanStatus.Active
                && (l.ExpiresAt == null || l.ExpiresAt > now), ct);
            if (activeCount >= maxDocument.Value)
                throw new BusinessRuleException("EBOOK_MAX_DOCUMENTS",
                    $"Bạn đã đạt giới hạn {maxDocument.Value} tài liệu số được mượn đồng thời.", 429);
        }

        var offlineDays = EbookPolicies.EffectiveOfflineDays(item, reader?.ReaderTypeId, byCollection);
        var loan = EbookItemLoan.Open(item.Id, readerPublicId, now, offlineDays is { } days ? now.AddDays(days) : null);
        Set.Add(loan);
        readyReservation?.Fulfil(now);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    /// <summary>Bạn đọc tự trả qua OPAC. <paramref name="requireOwnerReaderId"/>: lượt không phải của mình thì coi như không có (404).</summary>
    public async Task<EbookLoanDto> ReturnAsync(Guid loanPublicId, CancellationToken ct, Guid? requireOwnerReaderId = null)
    {
        var loan = await Set.FirstOrDefaultAsync(l => l.PublicId == loanPublicId, ct) ?? throw new NotFoundException(EntityName, loanPublicId);
        if (requireOwnerReaderId is { } owner && loan.ReaderPublicId != owner) throw new NotFoundException(EntityName, loanPublicId);
        loan.Return(clock.GetUtcNow());
        await Db.SaveChangesAsync(ct);
        await allocator.PromoteAsync(loan.EbookItemId, ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    /// <summary>Nhân viên thu hồi một lượt mượn — lý do bắt buộc.</summary>
    public async Task<EbookLoanDto> RecallAsync(RecallLoanRequest request, long? staffId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loan = await Set.FirstOrDefaultAsync(l => l.PublicId == request.LoanPublicId, ct) ?? throw new NotFoundException(EntityName, request.LoanPublicId);
        loan.Recall(clock.GetUtcNow(), staffId, request.Reason);
        await Record(loan, CrudChange.Updated, $"Thu hồi lượt mượn tài liệu #{loan.EbookItemId} — bạn đọc {loan.ReaderPublicId}. Lý do: {request.Reason}", ct);
        await Db.SaveChangesAsync(ct);
        await allocator.PromoteAsync(loan.EbookItemId, ct);
        return (await ToDtosAsync([loan], ct))[0];
    }

    /// <summary>Thu hồi mọi lượt đang mượn của một tài liệu số — lý do bắt buộc. Trả về số lượt đã thu hồi.</summary>
    public async Task<int> RecallAllAsync(RecallAllRequest request, long? staffId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var itemId = await Db.Set<EbookItem>().Where(i => i.PublicId == request.EbookItemPublicId).Select(i => i.Id).FirstOrDefaultAsync(ct);
        if (itemId == 0) throw new NotFoundException("Tài liệu số", request.EbookItemPublicId);
        var now = clock.GetUtcNow();
        var loans = await Set.Where(l => l.EbookItemId == itemId && l.Status == EbookLoanStatus.Active).ToListAsync(ct);
        foreach (var loan in loans) loan.Recall(now, staffId, request.Reason);
        if (loans.Count > 0)
        {
            await Record(null, CrudChange.Updated, $"Thu hồi {loans.Count} lượt mượn tài liệu #{itemId}. Lý do: {request.Reason}", ct);
            await Db.SaveChangesAsync(ct);
        }
        await allocator.PromoteAsync(itemId, ct);
        return loans.Count;
    }

    /// <summary>Lượt mượn còn hiệu lực của bạn đọc cho tài liệu — gate xin URL file (LRC: ValidateAccessAsync). Kiểm cả chính sách
    /// đọc hiện tại (thư viện đổi chính sách sau khi đã mượn vẫn chặn đọc).</summary>
    public async Task<bool> ValidateAccessAsync(long ebookItemId, Guid readerPublicId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var loan = await Set.AsNoTracking().Where(l => l.EbookItemId == ebookItemId && l.ReaderPublicId == readerPublicId)
            .OrderByDescending(l => l.Status == EbookLoanStatus.Active).ThenByDescending(l => l.CheckedOutAt).FirstOrDefaultAsync(ct);
        if (loan is null || !loan.IsOpen || loan.IsExpired(now)) return false;
        var item = await Db.Set<EbookItem>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == ebookItemId, ct);
        if (item is null) return false;
        var reader = await Db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == readerPublicId, ct);
        var byCollection = await allocator.ByCollectionPolicyAsync(reader?.ReaderTypeId, item.CollectionId, ct);
        return EbookPolicies.ReadAllowed(item, reader?.ReaderTypeId, byCollection);
    }

    /// <summary>Lượt mượn còn hiệu lực của chính mình (OPAC "Tài liệu số đang mượn").</summary>
    public async Task<IReadOnlyList<EbookLoanDto>> ActiveForAsync(Guid readerPublicId, CancellationToken ct) =>
        await Set.AsNoTracking().Where(l => l.ReaderPublicId == readerPublicId && l.Status == EbookLoanStatus.Active)
            .OrderBy(l => l.ExpiresAt).Select(Projection).ToListAsync(ct);

    private async Task<IReadOnlyList<EbookLoanDto>> ToDtosAsync(List<EbookItemLoan> loans, CancellationToken ct)
    {
        var ids = loans.Select(l => l.Id).ToList();
        return await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).Select(Projection).ToListAsync(ct);
    }

    private Task Record(EbookItemLoan? loan, CrudChange change, string summary, CancellationToken ct) =>
        AuditSink?.RecordAsync(new CrudAuditEntry(nameof(EbookItemLoan), EntityName, loan?.PublicId ?? Guid.Empty, change, summary), ct)
        ?? Task.CompletedTask;
}

public sealed class EbookReservationSearch : CrudSearch
{
    public Guid? EbookItemPublicId { get; set; }
    public Guid? ReaderPublicId { get; set; }
    public int? ReservationStatus { get; set; }
}

public sealed record EbookReservationDto(
    long Id, Guid PublicId, long EbookItemId, Guid EbookItemPublicId, string? Title, Guid ReaderPublicId, string? CardNo, string? ReaderName,
    DateTimeOffset RequestedAt, int Status, DateTimeOffset? ReadyAt, DateTimeOffset? ReadyExpiresAt, DateTimeOffset? FulfilledAt,
    DateTimeOffset? CancelledAt, int? QueuePosition)
{
    public EbookReservationDto ForReader() => this with { CardNo = null, ReaderName = null };
}

public sealed record EbookReservationOpUnused;

/// <summary>
/// Đặt trước tài liệu số (LRC: EbookItemReservationRepository, quyền DIGITAL_LOANS). Không có Add/Update/Delete chung — bạn đọc
/// tự đặt/huỷ qua OPAC (ReaderDigitalEndpoints), cán bộ chỉ tra cứu.
/// </summary>
public sealed class EbookReservationResource(ICrudDbContext db, TimeProvider clock, EbookReservationAllocator allocator)
    : CrudResource<EbookReservationResource, EbookItemReservation, EbookReservationSearch, EbookReservationOpUnused, EbookReservationDto>(db)
{
    protected override string EntityName => "Đặt trước tài liệu số";

    protected override string? Describe(EbookItemReservation entity) => $"tài liệu #{entity.EbookItemId} — bạn đọc {entity.ReaderPublicId}";

    protected override Expression<Func<EbookItemReservation, EbookReservationDto>> Projection => x => new EbookReservationDto(
        x.Id, x.PublicId, x.EbookItemId, Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.PublicId).FirstOrDefault(),
        Db.Set<EbookItem>().Where(i => i.Id == x.EbookItemId).Select(i => i.Title).FirstOrDefault(), x.ReaderPublicId,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.CardNo).FirstOrDefault(),
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.RequestedAt, x.Status, x.ReadyAt, x.ReadyExpiresAt, x.FulfilledAt, x.CancelledAt,
        x.Status == EbookReservationStatus.Pending
            ? Db.Set<EbookItemReservation>().Count(o => o.EbookItemId == x.EbookItemId && o.Status == EbookReservationStatus.Pending && o.Id <= x.Id)
            : null);

    protected override EbookItemReservation Create(EbookReservationOpUnused request) => throw new NotSupportedException("Đặt trước tạo qua ReserveAsync.");

    protected override void Update(EbookItemReservation entity, EbookReservationOpUnused request) =>
        throw new NotSupportedException("Đặt trước không sửa được — huỷ rồi đặt lại.");

    protected override IQueryable<EbookItemReservation> Filter(IQueryable<EbookItemReservation> query, EbookReservationSearch search)
    {
        if (search.EbookItemPublicId is { } itemPublicId)
        {
            var itemId = Db.Set<EbookItem>().Where(i => i.PublicId == itemPublicId).Select(i => i.Id).FirstOrDefault();
            query = query.Where(x => x.EbookItemId == itemId);
        }
        if (search.ReaderPublicId is { } reader) query = query.Where(x => x.ReaderPublicId == reader);
        if (search.ReservationStatus is { } status) query = query.Where(x => x.Status == status);
        return query;
    }

    protected override IOrderedQueryable<EbookItemReservation> Order(IQueryable<EbookItemReservation> query) =>
        query.OrderBy(x => x.Status == EbookReservationStatus.Ready ? 0 : x.Status == EbookReservationStatus.Pending ? 1 : 2)
            .ThenBy(x => x.RequestedAt).ThenBy(x => x.Id);

    /// <summary>
    /// Đặt trước (LRC: ReserveAsync). Chặn nếu đang mượn còn hiệu lực, đã có đặt trước đang mở, tài liệu không giới hạn bản đọc
    /// (nên mượn thẳng), còn chỗ trống (nên mượn thẳng), hoặc loại bạn đọc chưa được cấu hình quyền đặt trước.
    /// </summary>
    public async Task<EbookReservationDto> ReserveAsync(Guid itemPublicId, Guid readerPublicId, CancellationToken ct)
    {
        var item = await Db.Set<EbookItem>().FirstOrDefaultAsync(x => x.PublicId == itemPublicId && x.Status == IHasStatus.Active, ct)
            ?? throw new NotFoundException("Tài liệu số", itemPublicId);
        var now = clock.GetUtcNow();

        if (await Db.Set<EbookItemLoan>().AnyAsync(l => l.EbookItemId == item.Id && l.ReaderPublicId == readerPublicId
                && l.Status == EbookLoanStatus.Active && (l.ExpiresAt == null || l.ExpiresAt > now), ct))
            throw new BusinessRuleException("EBOOK_ALREADY_BORROWED", "Bạn đang mượn tài liệu này rồi.", 400);
        if (await Set.AnyAsync(r => r.EbookItemId == item.Id && r.ReaderPublicId == readerPublicId
                && (r.Status == EbookReservationStatus.Pending || r.Status == EbookReservationStatus.Ready), ct))
            throw new ConflictException("EBOOK_RESERVATION_EXISTS", "Bạn đã có một yêu cầu đặt trước đang chờ cho tài liệu này.");
        if (item.PrintCopies is not > 0)
            throw new BusinessRuleException("EBOOK_NO_LIMIT", "Tài liệu này không giới hạn số bản đọc đồng thời, không cần đặt trước.", 400);

        var occupied = await allocator.OccupiedCountAsync(item.Id, now, ct);
        if (occupied < item.PrintCopies.Value)
            throw new BusinessRuleException("EBOOK_SLOT_AVAILABLE", "Tài liệu vẫn còn bản trống, vui lòng mượn trực tiếp thay vì đặt trước.", 400);

        var reader = await Db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == readerPublicId, ct);
        var general = await allocator.GeneralPolicyAsync(reader?.ReaderTypeId, ct);
        var expireHours = EbookPolicies.EffectiveReservationExpireHours(reader?.ReaderTypeId, general);
        if (expireHours is not > 0)
            throw new BusinessRuleException("EBOOK_RESERVATION_NOT_ALLOWED", "Nhóm bạn đọc của bạn chưa được cấu hình quyền đặt trước tài liệu số.", 403);

        var reservation = EbookItemReservation.Place(item.Id, readerPublicId, now);
        Set.Add(reservation);
        await Db.SaveChangesAsync(ct);
        return (await ToDtoAsync(reservation, ct))!;
    }

    /// <summary><paramref name="requireOwnerReaderId"/>: đặt trước không phải của mình thì coi như không có (404).</summary>
    public async Task<EbookReservationDto> CancelAsync(Guid reservationPublicId, CancellationToken ct, Guid? requireOwnerReaderId = null)
    {
        var reservation = await Set.FirstOrDefaultAsync(r => r.PublicId == reservationPublicId, ct)
            ?? throw new NotFoundException(EntityName, reservationPublicId);
        if (requireOwnerReaderId is { } owner && reservation.ReaderPublicId != owner) throw new NotFoundException(EntityName, reservationPublicId);
        reservation.Cancel(clock.GetUtcNow());
        await Db.SaveChangesAsync(ct);
        await allocator.PromoteAsync(reservation.EbookItemId, ct);
        return (await ToDtoAsync(reservation, ct))!;
    }

    /// <summary>Đặt trước còn hiệu lực của chính mình (OPAC "Đặt trước của tôi").</summary>
    public async Task<IReadOnlyList<EbookReservationDto>> ActiveForAsync(Guid readerPublicId, CancellationToken ct) =>
        await Set.AsNoTracking().Where(r => r.ReaderPublicId == readerPublicId
                && (r.Status == EbookReservationStatus.Pending || r.Status == EbookReservationStatus.Ready))
            .OrderBy(r => r.RequestedAt).Select(Projection).ToListAsync(ct);

    private async Task<EbookReservationDto?> ToDtoAsync(EbookItemReservation reservation, CancellationToken ct) =>
        await Set.AsNoTracking().Where(x => x.Id == reservation.Id).Select(Projection).FirstOrDefaultAsync(ct);
}
