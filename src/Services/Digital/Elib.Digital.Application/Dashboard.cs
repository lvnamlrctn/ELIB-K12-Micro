using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

/// <summary>Tài liệu số được đọc nhiều (số lượt đọc trực tuyến trong 30 ngày gần nhất).</summary>
public sealed record TopReadEbook(Guid EbookPublicId, string Title, string? Author, int Count);

public sealed record FormatShare(string Format, int Count, int Percent);

public sealed record DigitalStorage(long TotalBytes, int FileCount);

/// <summary>
/// Bảng điều khiển tài liệu số của đơn vị (GET /api/dashboard). <see cref="Ebooks"/>: tổng đầu tài liệu; <see cref="Published"/>: đang hoạt động.
/// <see cref="AddedTrend"/>: đầu tài liệu thêm mới theo tháng (ngày nộp, thiếu thì ngày tạo); <see cref="ReadsTrend"/>: lượt đọc trực tuyến theo tháng.
/// Dung lượng là tổng kích thước file đã khai báo ở tài liệu (không đối soát với kho lưu trữ).
/// </summary>
public sealed record DigitalDashboard(int Ebooks, int Published, long TotalViews, long TotalDownloads, MonthlySeries AddedTrend, MonthlySeries ReadsTrend,
    IReadOnlyList<TopReadEbook> TopRead, IReadOnlyList<FormatShare> FormatDistribution, DigitalStorage Storage);

public sealed class DigitalDashboardService(ICrudDbContext db, TimeProvider clock)
{
    private const int Top = 10;

    public async Task<DigitalDashboard> BuildAsync(CancellationToken ct)
    {
        var items = db.Set<EbookItem>().AsNoTracking();
        var total = await items.CountAsync(ct);
        var published = await items.CountAsync(i => i.Status == IHasStatus.Active, ct);
        var views = await items.SumAsync(i => (long)i.TotalView, ct);
        var downloads = await items.SumAsync(i => (long)i.TotalDownload, ct);

        var yearStart = DashboardSeries.StartOf(DashboardSeries.MonthStart(clock, 11));
        var added = await items.Select(i => i.Submited ?? i.CreatedAt).Where(at => at >= yearStart).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var sessions = db.Set<EbookReadSession>().AsNoTracking();
        var reads = await sessions.Where(s => s.StartedAt >= yearStart).Select(s => s.StartedAt).Take(DashboardSeries.RowCap).ToListAsync(ct);

        var since = DashboardSeries.StartOf(DashboardSeries.Today(clock).AddDays(-29));
        var topRows = await sessions.Where(s => s.StartedAt >= since).GroupBy(s => s.EbookItemId).Select(g => new { Id = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Id).Take(Top).ToListAsync(ct);
        var ids = topRows.Select(x => x.Id).ToList();
        var books = await items.Where(i => ids.Contains(i.Id)).Select(i => new { i.Id, i.PublicId, i.Title, i.Author }).ToDictionaryAsync(i => i.Id, ct);
        var topRead = topRows.Where(x => books.ContainsKey(x.Id)).Select(x => new TopReadEbook(books[x.Id].PublicId, books[x.Id].Title, books[x.Id].Author, x.Count)).ToList();

        var files = await db.Set<EbookFile>().AsNoTracking().Select(f => new { f.FileExt, f.FileSize }).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var formats = files.GroupBy(f => string.IsNullOrWhiteSpace(f.FileExt) ? "?" : f.FileExt.Trim().TrimStart('.').ToLowerInvariant())
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new FormatShare(g.Key, g.Count(), files.Count == 0 ? 0 : (int)Math.Round(100.0 * g.Count() / files.Count))).ToList();
        var bytes = (long)files.Sum(f => f.FileSize ?? 0);

        return new DigitalDashboard(total, published, views, downloads, DashboardSeries.Months(added, clock), DashboardSeries.Months(reads, clock),
            topRead, formats, new DigitalStorage(bytes, files.Count));
    }
}
