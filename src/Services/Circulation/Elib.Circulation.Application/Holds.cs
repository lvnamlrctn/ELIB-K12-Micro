using System.Globalization;
using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>Mã mẫu tin của lưu thông (notification giữ nội dung mẫu; mã giữ như monolith EMAIL_PRINT_*).</summary>
public static class CirculationTemplates
{
    public const string DueSoon = "PRINT_DUE_SOON";
    public const string Overdue = "PRINT_OVERDUE";
    public const string HoldReady = "PRINT_HOLD_READY";
    public const string HoldExpired = "PRINT_HOLD_EXPIRED";
}

/// <summary>Gửi tin cho bạn đọc qua notification (NotificationRequested, outbox) — địa chỉ lấy từ bản sao bạn đọc.</summary>
public sealed class ReaderNotifier(IPublishEndpoint publisher, ITenantContext tenant)
{
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

    public static string Date(DateTimeOffset at) => Loan.LocalDate(at).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}

/// <summary>
/// Hàng chờ đặt mượn dùng chung cho quầy, màn đặt mượn và job: bản nào đang giữ cho ai, giữ bản trống cho người đặt sớm nhất,
/// đóng đặt mượn khi người đặt mượn được sách.
/// </summary>
public sealed class HoldAllocator(ICrudDbContext db, ReaderNotifier notifier, TimeProvider clock)
{
    /// <summary>Đặt mượn đang giữ bản sách này (Ready), nếu có.</summary>
    public Task<Hold?> HeldAsync(Guid itemPublicId, CancellationToken ct) =>
        db.Set<Hold>().FirstOrDefaultAsync(h => h.ItemPublicId == itemPublicId && h.Status == HoldStatus.Ready, ct);

    /// <summary>Bạn đọc mượn một bản của biểu ghi đang đặt → đặt mượn hoàn thành (bản đang giữ cho chính bạn đọc hoặc bản khác).</summary>
    public async Task FulfilAsync(PatronReplica reader, ItemReplica item, CancellationToken ct)
    {
        var holds = await db.Set<Hold>().Where(h => h.ReaderPublicId == reader.ReaderPublicId && h.Mfn == item.Mfn
                && (h.Status == HoldStatus.Waiting || h.Status == HoldStatus.Ready))
            .ToListAsync(ct);
        var now = clock.GetUtcNow();
        foreach (var hold in holds)
        {
            var heldOther = hold.Status == HoldStatus.Ready && hold.ItemPublicId != item.ItemPublicId ? hold.ItemPublicId : null;
            hold.Fulfil(now, item);
            // Bản đã giữ cho bạn đọc nhưng bạn đọc mượn bản khác → bản giữ chuyển cho người kế tiếp.
            if (heldOther is { } other && await db.Set<ItemReplica>().FirstOrDefaultAsync(i => i.ItemPublicId == other, ct) is { } freed)
                await AssignNextAsync(freed, ct);
        }
    }

    /// <summary>
    /// Bản sách vừa rảnh (trả, đặt mượn hết hạn/huỷ): giữ cho đặt mượn đang chờ sớm nhất của biểu ghi và báo bạn đọc. Bản không sẵn
    /// sàng, đang có người mượn hoặc đã giữ cho người khác thì thôi. Trả về đặt mượn vừa được giữ bản.
    /// <paramref name="returningLoan"/>: lượt vừa trả nhưng chưa lưu (cùng transaction) — không tính là đang mượn.
    /// </summary>
    public async Task<Hold?> AssignNextAsync(ItemReplica item, CancellationToken ct, Guid? returningLoan = null)
    {
        if (item.Deleted || item.Status != ItemReplica.Available) return null;
        if (await db.Set<Loan>().AnyAsync(l => l.ItemPublicId == item.ItemPublicId && l.ReturnedAt == null && l.PublicId != returningLoan, ct)) return null;
        if (db.Set<Hold>().Local.Any(h => h.ItemPublicId == item.ItemPublicId && h.Status == HoldStatus.Ready)
            || await db.Set<Hold>().AnyAsync(h => h.ItemPublicId == item.ItemPublicId && h.Status == HoldStatus.Ready, ct)) return null;
        var next = await db.Set<Hold>().Where(h => h.Mfn == item.Mfn && h.Status == HoldStatus.Waiting)
            .OrderBy(h => h.RequestedAt).ThenBy(h => h.Id).FirstOrDefaultAsync(ct);
        if (next is null) return null;
        await AssignAsync(next, item, ct);
        return next;
    }

    /// <summary>Bản sẵn sàng của biểu ghi chưa ai mượn/giữ — giữ ngay cho đặt mượn mới.</summary>
    public async Task<ItemReplica?> FreeItemAsync(long mfn, CancellationToken ct)
    {
        var busy = db.Set<Loan>().Where(l => l.ReturnedAt == null).Select(l => l.ItemPublicId)
            .Concat(db.Set<Hold>().Where(h => h.Status == HoldStatus.Ready && h.ItemPublicId != null).Select(h => h.ItemPublicId!.Value));
        return await db.Set<ItemReplica>().Where(i => i.Mfn == mfn && !i.Deleted && i.Status == ItemReplica.Available && !busy.Contains(i.ItemPublicId))
            .OrderBy(i => i.BarcodeKey).FirstOrDefaultAsync(ct);
    }

    public async Task AssignAsync(Hold hold, ItemReplica item, CancellationToken ct)
    {
        var reader = await db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == hold.ReaderPublicId, ct);
        var policies = await db.Set<LoanPolicy>().AsNoTracking().ToListAsync(ct);
        hold.Assign(item, clock.GetUtcNow(), LoanPolicyResource.Resolve(policies, reader?.ReaderTypeId, hold.CircPlaceId).HoldDays);
        if (hold.PublicId == Guid.Empty) hold.PublicId = Guid.CreateVersion7();
        var title = await db.Set<BibSnapshot>().Where(b => b.Mfn == hold.Mfn).Select(b => b.Title).FirstOrDefaultAsync(ct);
        await notifier.NotifyAsync(reader, CirculationTemplates.HoldReady, new Dictionary<string, string>
        {
            ["title"] = title ?? "",
            ["barcode"] = item.Barcode,
            ["expires_at"] = ReaderNotifier.Date(hold.ExpiresAt!.Value),
        }, $"hold-ready:{hold.PublicId}:{item.ItemPublicId}", ct);
    }
}

/// <summary>Danh sách đặt mượn (monolith: CirculationRequest/Search). HoldStatus: 1 chờ sách, 2 đang giữ sách, 3 đã mượn, 4 huỷ, 5 hết hạn.</summary>
public sealed class HoldSearch : CrudSearch
{
    public string? CardNo { get; set; }
    public int? HoldStatus { get; set; }
    public long? Mfn { get; set; }
}

public sealed record HoldDto(
    long Id, Guid PublicId, Guid ReaderPublicId, string CardNo, string? ReaderName, long Mfn, string? Title, string? Author, long? CircPlaceId,
    int Status, DateTimeOffset RequestedAt, Guid? ItemPublicId, string? Barcode, DateTimeOffset? ReadyAt, DateTimeOffset? ExpiresAt,
    DateTimeOffset? ClosedAt, string? Note, int? QueuePosition);

/// <summary>Đặt mượn hộ bạn đọc tại quầy theo MFN hoặc theo một số ĐKCB của biểu ghi.</summary>
public sealed record PlaceHoldRequest(string CardNo, long? Mfn = null, string? Barcode = null, long? CircPlaceId = null, string? Note = null);

public sealed record CancelHoldRequest(Guid HoldId, string? Reason = null);

/// <summary>
/// Đặt mượn (monolith: CirculationRequestController + MyLibrary/Hold, quyền REQUEST_BOOKS). Cán bộ đặt hộ bạn đọc (bạn đọc tự đặt qua
/// OPAC khi có đăng nhập bạn đọc). Duyệt/từ chối của monolith thay bằng hàng chờ: còn bản thì giữ ngay, hết thì chờ bản được trả.
/// </summary>
public sealed class HoldResource(ICrudDbContext db, Replicas replicas, HoldAllocator queue, TimeProvider clock)
    : CrudResource<HoldResource, Hold, HoldSearch, PlaceHoldRequest, HoldDto>(db)
{
    protected override string EntityName => "Đặt mượn";

    protected override string? Describe(Hold entity) => $"MFN {entity.Mfn} — thẻ {entity.CardNo}";

    protected override Expression<Func<Hold, HoldDto>> Projection => x => new HoldDto(
        x.Id, x.PublicId, x.ReaderPublicId, x.CardNo,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.Mfn,
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.Mfn).Select(b => b.Title).FirstOrDefault(),
        Db.Set<BibSnapshot>().Where(b => b.Mfn == x.Mfn).Select(b => b.Author).FirstOrDefault(),
        x.CircPlaceId, x.Status, x.RequestedAt, x.ItemPublicId, x.Barcode, x.ReadyAt, x.ExpiresAt, x.ClosedAt, x.Note,
        x.Status == HoldStatus.Waiting
            ? Db.Set<Hold>().Count(o => o.Mfn == x.Mfn && o.Status == HoldStatus.Waiting && (o.RequestedAt < x.RequestedAt || (o.RequestedAt == x.RequestedAt && o.Id <= x.Id)))
            : null);

    protected override Hold Create(PlaceHoldRequest request) => throw new NotSupportedException("Đặt mượn tạo qua PlaceAsync.");

    protected override void Update(Hold entity, PlaceHoldRequest request) => throw new NotSupportedException("Đặt mượn không sửa được — huỷ rồi đặt lại.");

    protected override IQueryable<Hold> Filter(IQueryable<Hold> query, HoldSearch s)
    {
        if (!string.IsNullOrWhiteSpace(s.CardNo))
        {
            var card = PatronReplica.Key(s.CardNo);
            query = query.Where(x => x.CardNo == card);
        }
        if (s.HoldStatus is { } status) query = query.Where(x => x.Status == status);
        if (s.Mfn is { } mfn) query = query.Where(x => x.Mfn == mfn);
        if (s.Term is { } term)
        {
            var key = term.ToUpperInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower()/ToUpper() sang SQL
            query = query.Where(x => x.CardNo.StartsWith(key) || (x.Barcode != null && x.Barcode.ToUpper().StartsWith(key))
                || Db.Set<BibSnapshot>().Any(b => b.Mfn == x.Mfn && b.Title.ToLower().Contains(term))
                || Db.Set<PatronReplica>().Any(r => r.ReaderPublicId == x.ReaderPublicId && r.FullName.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<Hold> Order(IQueryable<Hold> query) =>
        query.OrderBy(x => x.Status == HoldStatus.Ready ? 0 : x.Status == HoldStatus.Waiting ? 1 : 2).ThenByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id);

    /// <summary>
    /// Đặt mượn: bạn đọc còn hiệu lực, chưa đặt/đang mượn cùng biểu ghi, chưa vượt số đặt mượn của chính sách. Còn bản sẵn sàng
    /// chưa ai giữ → giữ ngay và báo bạn đọc; không thì xếp hàng.
    /// </summary>
    public async Task<HoldDto> PlaceAsync(PlaceHoldRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reader = await replicas.ReaderAsync(request.CardNo ?? "", ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{request.CardNo?.Trim()}'.", 404);
        if (reader.Status != IHasStatus.Active) throw new BusinessRuleException("READER_CANNOT_HOLD", "Thẻ bạn đọc đang bị khoá.");
        if (reader.ExpireDate is { } expire && expire < Loan.LocalDate(clock.GetUtcNow()))
            throw new BusinessRuleException("READER_CANNOT_HOLD", $"Thẻ bạn đọc đã hết hạn ngày {expire:dd/MM/yyyy}.");

        long mfn;
        if (request.Mfn is { } m) mfn = m;
        else if (!string.IsNullOrWhiteSpace(request.Barcode))
            mfn = (await replicas.ItemAsync(request.Barcode, ct) ?? throw new BusinessRuleException("ITEM_NOT_FOUND", $"Không tìm thấy ĐKCB \"{request.Barcode.Trim()}\".", 404)).Mfn;
        else throw new BusinessRuleException("HOLD_TARGET_REQUIRED", "Nhập MFN hoặc số ĐKCB của tài liệu cần đặt mượn.");
        await replicas.EnsureBibsAsync([mfn], ct);
        if (!await Db.Set<ItemReplica>().AnyAsync(i => i.Mfn == mfn && !i.Deleted, ct))
            throw new BusinessRuleException("HOLD_NO_COPY", $"Biểu ghi MFN {mfn} không có bản sách nào để đặt mượn.");

        if (await Set.AnyAsync(h => h.ReaderPublicId == reader.ReaderPublicId && h.Mfn == mfn && (h.Status == HoldStatus.Waiting || h.Status == HoldStatus.Ready), ct))
            throw new ConflictException("HOLD_EXISTS", "Bạn đọc đã có đặt mượn đang chờ cho tài liệu này.");
        if (await Db.Set<Loan>().AnyAsync(l => l.ReaderPublicId == reader.ReaderPublicId && l.Mfn == mfn && l.ReturnedAt == null, ct))
            throw new BusinessRuleException("HOLD_ALREADY_BORROWED", "Bạn đọc đang mượn tài liệu này.");
        var policy = LoanPolicyResource.Resolve(await Db.Set<LoanPolicy>().AsNoTracking().ToListAsync(ct), reader.ReaderTypeId, request.CircPlaceId);
        if (policy.MaxHolds is { } max)
        {
            var active = await Set.CountAsync(h => h.ReaderPublicId == reader.ReaderPublicId && (h.Status == HoldStatus.Waiting || h.Status == HoldStatus.Ready), ct);
            if (active >= max)
                throw new BusinessRuleException("HOLD_LIMIT", max == 0 ? "Chính sách lưu thông không cho đặt mượn." : $"Bạn đọc đã đặt mượn đủ {max} tài liệu theo chính sách.");
        }

        var hold = Hold.Place(reader, mfn, request.CircPlaceId, clock.GetUtcNow(), request.Note);
        Set.Add(hold);
        if (await queue.FreeItemAsync(mfn, ct) is { } item) await queue.AssignAsync(hold, item, ct);
        await Record(hold, CrudChange.Added, $"Đặt mượn MFN {mfn} — thẻ {reader.CardNo}" + (hold.Barcode is { } b ? $", giữ bản {b}" : ", chờ sách"), ct);
        await Db.SaveChangesAsync(ct);
        return await GetAsync(hold.Id, ct);
    }

    /// <summary>Huỷ đặt mượn; bản đang giữ chuyển cho người đặt kế tiếp.</summary>
    public async Task<HoldDto> CancelAsync(CancelHoldRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hold = await Set.FirstOrDefaultAsync(h => h.PublicId == request.HoldId, ct) ?? throw new NotFoundException(EntityName, request.HoldId);
        var held = hold.Status == HoldStatus.Ready ? hold.ItemPublicId : null;
        hold.Cancel(clock.GetUtcNow(), request.Reason);
        Hold? next = null;
        if (held is { } itemId && await Db.Set<ItemReplica>().FirstOrDefaultAsync(i => i.ItemPublicId == itemId, ct) is { } item)
            next = await queue.AssignNextAsync(item, ct);
        await Record(hold, CrudChange.Updated, $"Huỷ đặt mượn MFN {hold.Mfn} — thẻ {hold.CardNo}" + (next is null ? "" : $"; bản {next.Barcode} giữ cho thẻ {next.CardNo}"), ct);
        await Db.SaveChangesAsync(ct);
        return await GetAsync(hold.Id, ct);
    }

    /// <summary>Đặt mượn còn hiệu lực của bạn đọc — hiện ở quầy.</summary>
    public async Task<IReadOnlyList<HoldDto>> ActiveForAsync(Guid readerPublicId, CancellationToken ct) =>
        await Set.AsNoTracking().Where(h => h.ReaderPublicId == readerPublicId && (h.Status == HoldStatus.Waiting || h.Status == HoldStatus.Ready))
            .OrderBy(h => h.RequestedAt).Select(Projection).ToListAsync(ct);

    private Task Record(Hold hold, CrudChange change, string summary, CancellationToken ct)
    {
        if (hold.PublicId == Guid.Empty) hold.PublicId = Guid.CreateVersion7();
        return AuditSink?.RecordAsync(new CrudAuditEntry(nameof(Hold), EntityName, hold.PublicId, change, summary), ct) ?? Task.CompletedTask;
    }
}

public sealed record CirculationJobResult(int DueSoon, int Overdue, int ExpiredHolds, int AssignedHolds = 0);

/// <summary>
/// Việc định kỳ của một đơn vị (monolith: DueSoonReminderJob, BookRequestExpiryJob) — chạy trong ngữ cảnh đơn vị:
/// nhắc sách sắp đến hạn (còn 1–2 ngày), báo quá hạn (một lần, chỉ lượt quá hạn trong 7 ngày gần nhất), huỷ giữ chỗ hết hạn và
/// chuyển bản cho người kế tiếp, giữ bản trống (bản mới xếp giá, lần giữ bị lỡ) cho đặt mượn đang chờ. Mỗi lượt chỉ nhắc một lần.
/// </summary>
public sealed class CirculationJobs(ICrudDbContext db, ReaderNotifier notifier, HoldAllocator queue, TimeProvider clock)
{
    public const int OverdueLookbackDays = 7;
    private const int Batch = 500;

    public async Task<CirculationJobResult> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = Loan.LocalDate(now);
        var startOfToday = VietnamStart(today);
        var tomorrow = VietnamStart(today.AddDays(1));
        var afterTwoDays = VietnamStart(today.AddDays(3));
        var lookback = VietnamStart(today.AddDays(-OverdueLookbackDays));
        var dueSoon = await RemindAsync(
            l => l.ReturnedAt == null && l.DueSoonNotifiedOn == null && l.DueAt >= tomorrow && l.DueAt < afterTwoDays && l.LoanedAt < startOfToday,
            CirculationTemplates.DueSoon, l => l.MarkDueSoonNotified(today), "loan-due-soon", ct);
        var overdue = await RemindAsync(
            l => l.ReturnedAt == null && l.OverdueNotifiedOn == null && l.DueAt < startOfToday && l.DueAt >= lookback,
            CirculationTemplates.Overdue, l => l.MarkOverdueNotified(today), "loan-overdue", ct);
        var expired = await ExpireHoldsAsync(now, ct);
        var assigned = await AssignWaitingAsync(ct);
        return new CirculationJobResult(dueSoon, overdue, expired, assigned);
    }

    private async Task<int> AssignWaitingAsync(CancellationToken ct)
    {
        var mfns = await db.Set<Hold>().Where(h => h.Status == HoldStatus.Waiting).Select(h => h.Mfn).Distinct().OrderBy(m => m).Take(Batch).ToListAsync(ct);
        var assigned = 0;
        foreach (var mfn in mfns)
        {
            var waiting = await db.Set<Hold>().Where(h => h.Mfn == mfn && h.Status == HoldStatus.Waiting).OrderBy(h => h.RequestedAt).ThenBy(h => h.Id).ToListAsync(ct);
            foreach (var hold in waiting)
            {
                if (await queue.FreeItemAsync(mfn, ct) is not { } item) break;
                await queue.AssignAsync(hold, item, ct);
                await db.SaveChangesAsync(ct); // từng bản: FreeItemAsync đọc DB để không giữ một bản cho hai người
                assigned++;
            }
        }
        return assigned;
    }

    private async Task<int> RemindAsync(Expression<Func<Loan, bool>> due, string template, Action<Loan> mark, string key, CancellationToken ct)
    {
        var loans = await db.Set<Loan>().Where(due).OrderBy(l => l.Id).Take(Batch).ToListAsync(ct);
        if (loans.Count == 0) return 0;
        var readerIds = loans.Select(l => l.ReaderPublicId).Distinct().ToList();
        var readers = await db.Set<PatronReplica>().AsNoTracking().Where(r => readerIds.Contains(r.ReaderPublicId)).ToDictionaryAsync(r => r.ReaderPublicId, ct);
        var mfns = loans.Select(l => l.Mfn).Distinct().ToList();
        var titles = await db.Set<BibSnapshot>().AsNoTracking().Where(b => mfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, b => b.Title, ct);
        foreach (var loan in loans)
        {
            mark(loan); // không lặp lại kể cả khi bạn đọc chưa có email
            await notifier.NotifyAsync(readers.GetValueOrDefault(loan.ReaderPublicId), template, new Dictionary<string, string>
            {
                ["title"] = titles.GetValueOrDefault(loan.Mfn) ?? "",
                ["barcode"] = loan.Barcode,
                ["due_date"] = ReaderNotifier.Date(loan.DueAt),
            }, $"{key}:{loan.PublicId}", ct);
        }
        await db.SaveChangesAsync(ct);
        return loans.Count;
    }

    private async Task<int> ExpireHoldsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.Set<Hold>().Where(h => h.Status == HoldStatus.Ready && h.ExpiresAt < now).OrderBy(h => h.Id).Take(Batch).ToListAsync(ct);
        if (expired.Count == 0) return 0;
        foreach (var hold in expired)
        {
            hold.Expire(now);
            var reader = await db.Set<PatronReplica>().AsNoTracking().FirstOrDefaultAsync(r => r.ReaderPublicId == hold.ReaderPublicId, ct);
            var title = await db.Set<BibSnapshot>().Where(b => b.Mfn == hold.Mfn).Select(b => b.Title).FirstOrDefaultAsync(ct);
            await notifier.NotifyAsync(reader, CirculationTemplates.HoldExpired, new Dictionary<string, string>
            {
                ["title"] = title ?? "",
                ["barcode"] = hold.Barcode ?? "",
                ["expires_at"] = ReaderNotifier.Date(hold.ExpiresAt!.Value),
            }, $"hold-expired:{hold.PublicId}", ct);
        }
        await db.SaveChangesAsync(ct);
        foreach (var hold in expired)
            if (hold.ItemPublicId is { } id && await db.Set<ItemReplica>().FirstOrDefaultAsync(i => i.ItemPublicId == id, ct) is { } item)
                await queue.AssignNextAsync(item, ct);
        await db.SaveChangesAsync(ct);
        return expired.Count;
    }

    private static DateTimeOffset VietnamStart(DateOnly date) => Loan.VietnamStart(date);
}
