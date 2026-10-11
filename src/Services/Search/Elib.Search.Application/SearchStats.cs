using Elib.BuildingBlocks.Domain;
using Elib.Search.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Search.Application;

/// <summary>Bạn đọc mở kết quả thứ <see cref="Position"/> (đếm từ 1 trên toàn danh sách) của lượt tìm <see cref="QueryId"/>.</summary>
public sealed record OpacClickRequest(Guid QueryId, Guid BibPublicId, int Position);

public sealed record DailySearchStat(DateOnly Date, int Searches, int ZeroResults, int WithClicks);

/// <summary>Một câu tìm (gộp không dấu): số lượt, số kết quả trung bình, số lượt có mở kết quả.</summary>
public sealed record QueryStat(string Text, int Searches, int AvgTotal, int WithClicks);

/// <summary>
/// Chất lượng tìm kiếm trong khoảng ngày: tổng lượt, lượt không có kết quả, lượt bạn đọc mở ít nhất một kết quả (click-through),
/// vị trí trung bình của kết quả được mở đầu tiên (càng gần 1 thì xếp hạng càng tốt), câu tìm nhiều nhất và câu tìm không ra kết quả.
/// </summary>
public sealed record SearchStatsSummary(
    DateOnly From, DateOnly To, int Searches, int ZeroResults, int WithClicks, int AdvancedSearches, double? AvgClickPosition,
    IReadOnlyList<DailySearchStat> Days, IReadOnlyList<QueryStat> TopQueries, IReadOnlyList<QueryStat> TopZeroResults);

/// <summary>Thống kê chất lượng tìm kiếm OPAC (monolith không có — số liệu để thủ thư bổ sung tài liệu, sửa biên mục, từ khoá).</summary>
public sealed class SearchStats(ISearchDb db, TimeProvider clock)
{
    public const int MaxRangeDays = 366;
    private const int TopSize = 20;
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Ghi lượt tìm (trang 1 của câu hỏi mới có từ khoá) — trả mã lượt tìm cho trình duyệt; còn lại null.</summary>
    public async Task<Guid?> RecordAsync(OpacSearchRequest request, int total, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Page > 1 || request.Refine) return null;
        var advanced = new[] { request.Title, request.Author, request.Publisher, request.Keyword, request.Isbn, request.Ddc, request.Barcode }
            .Any(v => !string.IsNullOrWhiteSpace(v));
        var text = string.Join(" ", new[] { request.Q, request.Title, request.Author, request.Publisher, request.Keyword, request.Isbn, request.Ddc, request.Barcode }
            .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
        if (text.Length == 0) return null; // chỉ duyệt theo bộ lọc, không có câu tìm

        var query = new SearchQuery
        {
            QueryId = Guid.CreateVersion7(),
            At = clock.GetUtcNow(),
            Text = TextFold.Cut(string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), SearchQuery.MaxTextLength)!,
            TextFold = TextFold.Cut(TextFold.Fold(text), SearchQuery.MaxTextLength)!,
            Advanced = advanced,
            Total = total,
        };
        db.Set<SearchQuery>().Add(query);
        await db.SaveChangesAsync(ct);
        return query.QueryId;
    }

    /// <summary>Ghi lượt mở kết quả. Mã lượt tìm sai/quá hạn bị bỏ qua lặng lẽ (endpoint công khai, không báo lỗi để dò).</summary>
    public async Task RecordClickAsync(OpacClickRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = await db.Set<SearchQuery>().FirstOrDefaultAsync(q => q.QueryId == request.QueryId, ct);
        if (query is not null && query.RecordClick(request.Position, clock.GetUtcNow())) await db.SaveChangesAsync(ct);
    }

    public async Task<SearchStatsSummary> SummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(VietnamOffset).DateTime);
        var end = to ?? today;
        var start = from ?? end.AddDays(-29);
        if (start > end) throw new BusinessRuleException("STATS_RANGE_INVALID", "Từ ngày phải trước hoặc bằng đến ngày.");
        if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
            throw new BusinessRuleException("STATS_RANGE_TOO_LONG", $"Xem tối đa {MaxRangeDays} ngày mỗi lần.");

        var startAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();
        var endAt = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();
        var range = db.Set<SearchQuery>().AsNoTracking().Where(q => q.At >= startAt && q.At < endAt);

        // Theo ngày: gom ở bộ nhớ (cột nhỏ) — ngày theo giờ Việt Nam, không phụ thuộc hàm ngày giờ của CSDL.
        var rows = await range.Select(q => new { q.At, q.Total, q.Clicks, q.Advanced, q.FirstClickPosition }).ToListAsync(ct);
        var days = rows.GroupBy(r => DateOnly.FromDateTime(r.At.ToOffset(VietnamOffset).DateTime))
            .Select(g => new DailySearchStat(g.Key, g.Count(), g.Count(r => r.Total == 0), g.Count(r => r.Clicks > 0)))
            .OrderBy(d => d.Date).ToList();
        var positions = rows.Where(r => r.FirstClickPosition is not null).Select(r => (double)r.FirstClickPosition!.Value).ToList();

        return new SearchStatsSummary(start, end, rows.Count, rows.Count(r => r.Total == 0), rows.Count(r => r.Clicks > 0), rows.Count(r => r.Advanced),
            positions.Count > 0 ? Math.Round(positions.Average(), 1) : null, days,
            await TopAsync(range, ct), await TopAsync(range.Where(q => q.Total == 0), ct));
    }

    private static async Task<IReadOnlyList<QueryStat>> TopAsync(IQueryable<SearchQuery> range, CancellationToken ct)
    {
        var groups = await range.GroupBy(q => q.TextFold)
            .Select(g => new { g.Key, Count = g.Count(), AvgTotal = g.Average(q => (double)q.Total), WithClicks = g.Count(q => q.Clicks > 0) })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Key).Take(TopSize).ToListAsync(ct);
        if (groups.Count == 0) return [];

        // Hiện câu có dấu như bạn đọc gõ (lần gần nhất) thay cho chữ không dấu.
        var keys = groups.Select(g => g.Key).ToList();
        var samples = await range.Where(q => keys.Contains(q.TextFold)).OrderByDescending(q => q.At)
            .Select(q => new { q.TextFold, q.Text }).Take(2000).ToListAsync(ct);
        var display = samples.GroupBy(s => s.TextFold).ToDictionary(g => g.Key, g => g.First().Text);
        return [.. groups.Select(g => new QueryStat(display.GetValueOrDefault(g.Key, g.Key), g.Count, (int)Math.Round(g.AvgTotal), g.WithClicks))];
    }
}
