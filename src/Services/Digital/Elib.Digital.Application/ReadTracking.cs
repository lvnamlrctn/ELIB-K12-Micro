using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record ReadBeatRequest(Guid EbookPublicId, Guid? FilePublicId, Guid SessionKey, int Page, int PageCount, int ActiveSeconds, string? Device);

public sealed record ReadProgressDto(Guid EbookPublicId, int LastPage, int MaxPage, int PageCount, int Percent, DateTimeOffset LastReadAt);

/// <summary>
/// Trình đọc ghi nhịp đọc (bạn đọc tự phục vụ, lấy từ claim): lượt đọc + tiến độ "đọc tiếp". Chỉ ghi cho tài liệu mà bạn đọc đọc được
/// (công khai, hoặc đang mượn còn hạn) — cùng điều kiện cấp URL file.
/// </summary>
public sealed class ReaderReading(ICrudDbContext db, EbookLoanResource loans, TimeProvider clock)
{
    public async Task<ReadProgressDto> BeatAsync(Guid readerPublicId, ReadBeatRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await db.Set<EbookItem>().AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.EbookPublicId && x.Status == IHasStatus.Active && !x.HiddenByTenant, ct)
            ?? throw new NotFoundException("Tài liệu số", request.EbookPublicId);
        if (!item.Free && !await loans.ValidateAccessAsync(item.Id, readerPublicId, ct))
            throw new BusinessRuleException("EBOOK_NOT_BORROWED", "Bạn chưa mượn tài liệu này hoặc đã hết hạn mượn.", 403);
        long? fileId = request.FilePublicId is { } filePublicId
            ? await db.Set<EbookFile>().Where(f => f.PublicId == filePublicId && f.EbookItemId == item.Id).Select(f => (long?)f.Id).FirstOrDefaultAsync(ct)
            : null;

        var now = clock.GetUtcNow();
        var session = await db.Set<EbookReadSession>().FirstOrDefaultAsync(s => s.SessionKey == request.SessionKey && s.ReaderPublicId == readerPublicId, ct);
        if (session is null)
            db.Set<EbookReadSession>().Add(session = EbookReadSession.Start(item.Id, fileId, readerPublicId, request.SessionKey, now));
        else if (session.EbookItemId != item.Id)
            throw new BusinessRuleException("READ_SESSION_MISMATCH", "Mã lượt đọc đã dùng cho tài liệu khác.");
        session.Beat(now, request.Page, request.PageCount, request.ActiveSeconds, request.Device);

        var progress = await db.Set<EbookReadProgress>().FirstOrDefaultAsync(p => p.EbookItemId == item.Id && p.ReaderPublicId == readerPublicId, ct);
        if (progress is null) db.Set<EbookReadProgress>().Add(progress = EbookReadProgress.Create(item.Id, readerPublicId));
        progress.Update(request.Page, request.PageCount, now);
        await db.SaveChangesAsync(ct);
        return new ReadProgressDto(item.PublicId, progress.LastPage, progress.MaxPage, progress.PageCount, progress.Percent, progress.LastReadAt);
    }

    /// <summary>Tiến độ đọc của bạn đọc với một tài liệu; null nếu chưa đọc.</summary>
    public async Task<ReadProgressDto?> ProgressAsync(Guid readerPublicId, Guid ebookPublicId, CancellationToken ct)
    {
        var row = await db.Set<EbookReadProgress>().AsNoTracking().Where(p => p.ReaderPublicId == readerPublicId)
            .Join(db.Set<EbookItem>().AsNoTracking().Where(i => i.PublicId == ebookPublicId), p => p.EbookItemId, i => i.Id, (p, i) => new { p, i.PublicId })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new ReadProgressDto(row.PublicId, row.p.LastPage, row.p.MaxPage, row.p.PageCount, row.p.Percent, row.p.LastReadAt);
    }
}

public sealed record ReadingSearch(Guid? EbookPublicId, string? Keyword, DateOnly? From, DateOnly? To, int PageIndex = 1, int PageSize = 20);

public sealed record ReadingSessionRow(
    Guid PublicId, Guid EbookPublicId, string EbookTitle, Guid ReaderPublicId, string? CardNo, string? FullName, DateTimeOffset StartedAt, DateTimeOffset LastSeenAt,
    int LastPage, int MaxPage, int PageCount, int ActiveSeconds, string? Device);

public sealed record ReadingSessionPage(int TotalCount, IReadOnlyList<ReadingSessionRow> Items);

public sealed record ReadingStatsRequest(DateOnly? From, DateOnly? To);

public sealed record ReadingTotals(int Sessions, int Readers, int Ebooks, long ActiveSeconds, int Views, int Downloads);

public sealed record ReadingTopEbook(Guid EbookPublicId, string Title, int Sessions, int Readers, long ActiveSeconds);

public sealed record ReadingTopReader(Guid ReaderPublicId, string? CardNo, string? FullName, int Sessions, long ActiveSeconds);

public sealed record ReadingDay(DateOnly Date, int Sessions, long ActiveSeconds);

public sealed record ReadingStats(ReadingTotals Totals, IReadOnlyList<ReadingTopEbook> TopEbooks, IReadOnlyList<ReadingTopReader> TopReaders, IReadOnlyList<ReadingDay> Daily);

/// <summary>Theo dõi đọc cho thủ thư (LRC: ReadingTrackingController — Search, Statistics). Ngày tính theo giờ Việt Nam.</summary>
public sealed class ReadingTracking(ICrudDbContext db)
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);
    public const int MaxRangeDays = 366;
    private const int MaxPageSize = 100;

    public async Task<ReadingSessionPage> SearchAsync(ReadingSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = Sessions(search.From, search.To);
        if (search.EbookPublicId is { } ebookPublicId)
        {
            var itemId = db.Set<EbookItem>().Where(i => i.PublicId == ebookPublicId).Select(i => i.Id).FirstOrDefault();
            query = query.Where(s => s.EbookItemId == itemId);
        }
        if (search.Keyword?.Trim().ToLowerInvariant() is { Length: > 0 } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            var readers = db.Set<PatronReplica>().Where(r => r.CardNo.ToLower().Contains(term) || r.FullName.ToLower().Contains(term)).Select(r => r.ReaderPublicId);
            var items = db.Set<EbookItem>().Where(i => i.Title.ToLower().Contains(term)).Select(i => i.Id);
#pragma warning restore CA1862, CA1304, CA1311
            query = query.Where(s => readers.Contains(s.ReaderPublicId) || items.Contains(s.EbookItemId));
        }

        var size = Math.Clamp(search.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, search.PageIndex);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(s => s.StartedAt).ThenByDescending(s => s.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var itemIds = rows.Select(r => r.EbookItemId).Distinct().ToList();
        var readerIds = rows.Select(r => r.ReaderPublicId).Distinct().ToList();
        var titles = await db.Set<EbookItem>().AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => new { i.PublicId, i.Title }, ct);
        var people = await db.Set<PatronReplica>().AsNoTracking().Where(r => readerIds.Contains(r.ReaderPublicId)).ToDictionaryAsync(r => r.ReaderPublicId, ct);
        return new ReadingSessionPage(total, [.. rows.Select(r => new ReadingSessionRow(
            r.PublicId, titles.GetValueOrDefault(r.EbookItemId)?.PublicId ?? Guid.Empty, titles.GetValueOrDefault(r.EbookItemId)?.Title ?? "", r.ReaderPublicId,
            people.GetValueOrDefault(r.ReaderPublicId)?.CardNo, people.GetValueOrDefault(r.ReaderPublicId)?.FullName, r.StartedAt, r.LastSeenAt, r.LastPage, r.MaxPage,
            r.PageCount, r.ActiveSeconds, r.Device))]);
    }

    public async Task<ReadingStats> StatisticsAsync(ReadingStatsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var to = request.To ?? DateOnly.FromDateTime(DateTime.UtcNow.Add(Vietnam));
        var from = request.From ?? to.AddDays(-29);
        if (to < from) throw new BusinessRuleException("RANGE_INVALID", "Khoảng ngày không hợp lệ.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays) throw new BusinessRuleException("RANGE_TOO_LONG", $"Chỉ thống kê tối đa {MaxRangeDays} ngày một lần.");

        var sessions = Sessions(from, to);
        var totals = await sessions.GroupBy(_ => 1).Select(g => new
        {
            Sessions = g.Count(), Readers = g.Select(s => s.ReaderPublicId).Distinct().Count(), Ebooks = g.Select(s => s.EbookItemId).Distinct().Count(),
            Seconds = g.Sum(s => (long)s.ActiveSeconds),
        }).FirstOrDefaultAsync(ct);
        var (start, end) = Range(from, to);
        var access = db.Set<EbookAccessLog>().AsNoTracking().Where(a => a.At >= start && a.At < end);
        var views = await access.CountAsync(a => a.Kind == EbookAccessLog.View, ct);
        var downloads = await access.CountAsync(a => a.Kind == EbookAccessLog.Download, ct);

        var topItems = await sessions.GroupBy(s => s.EbookItemId).Select(g => new
        {
            ItemId = g.Key, Sessions = g.Count(), Readers = g.Select(s => s.ReaderPublicId).Distinct().Count(), Seconds = g.Sum(s => (long)s.ActiveSeconds),
        }).OrderByDescending(x => x.Seconds).ThenByDescending(x => x.Sessions).Take(10).ToListAsync(ct);
        var itemIds = topItems.Select(x => x.ItemId).ToList();
        var titles = await db.Set<EbookItem>().AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => new { i.PublicId, i.Title }, ct);

        var topReaders = await sessions.GroupBy(s => s.ReaderPublicId).Select(g => new { ReaderId = g.Key, Sessions = g.Count(), Seconds = g.Sum(s => (long)s.ActiveSeconds) })
            .OrderByDescending(x => x.Seconds).ThenByDescending(x => x.Sessions).Take(10).ToListAsync(ct);
        var readerIds = topReaders.Select(x => x.ReaderId).ToList();
        var people = await db.Set<PatronReplica>().AsNoTracking().Where(r => readerIds.Contains(r.ReaderPublicId)).ToDictionaryAsync(r => r.ReaderPublicId, ct);

        var perDay = (await sessions.Select(s => new { s.StartedAt, s.ActiveSeconds }).ToListAsync(ct))
            .GroupBy(s => DateOnly.FromDateTime(s.StartedAt.ToOffset(Vietnam).DateTime))
            .ToDictionary(g => g.Key, g => (Sessions: g.Count(), Seconds: g.Sum(s => (long)s.ActiveSeconds)));
        var daily = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i))
            .Select(d => new ReadingDay(d, perDay.GetValueOrDefault(d).Sessions, perDay.GetValueOrDefault(d).Seconds)).ToList();

        return new ReadingStats(
            new ReadingTotals(totals?.Sessions ?? 0, totals?.Readers ?? 0, totals?.Ebooks ?? 0, totals?.Seconds ?? 0, views, downloads),
            [.. topItems.Select(x => new ReadingTopEbook(titles.GetValueOrDefault(x.ItemId)?.PublicId ?? Guid.Empty, titles.GetValueOrDefault(x.ItemId)?.Title ?? "", x.Sessions, x.Readers, x.Seconds))],
            [.. topReaders.Select(x => new ReadingTopReader(x.ReaderId, people.GetValueOrDefault(x.ReaderId)?.CardNo, people.GetValueOrDefault(x.ReaderId)?.FullName, x.Sessions, x.Seconds))],
            daily);
    }

    private IQueryable<EbookReadSession> Sessions(DateOnly? from, DateOnly? to)
    {
        var query = db.Set<EbookReadSession>().AsNoTracking();
        if (from is { } f) query = query.Where(s => s.StartedAt >= StartOfDay(f));
        if (to is { } t) query = query.Where(s => s.StartedAt < StartOfDay(t.AddDays(1)));
        return query;
    }

    /// <summary>0 giờ giờ Việt Nam của ngày, đổi sang UTC (cột timestamptz chỉ nhận UTC).</summary>
    private static DateTimeOffset StartOfDay(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Vietnam).ToUniversalTime();

    private static (DateTimeOffset Start, DateTimeOffset End) Range(DateOnly from, DateOnly to) => (StartOfDay(from), StartOfDay(to.AddDays(1)));
}
