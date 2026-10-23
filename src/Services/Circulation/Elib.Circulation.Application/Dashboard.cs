using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

public sealed record DailyActivityPoint(DateOnly Date, string Label, int Borrowed, int Returned);

public sealed record HourlyActivityPoint(int Hour, string Label, int Borrowed, int Returned);

public sealed record TopLoanedTitle(long Mfn, string Title, string? Author, int Count);

public sealed record OverdueBucket(string Label, int Count);

public sealed record StoreLoanCount(long? StoreId, string Name, int Count);

/// <summary>Đầu tài liệu chưa từng được mượn (đề xuất thanh lý): số bản còn trong kho.</summary>
public sealed record UnusedTitle(long Mfn, string Title, string? Author, string? StoreName, int Copies);

/// <summary>
/// Bảng điều khiển lưu thông của đơn vị (GET /api/dashboard): mượn/trả 30 ngày và theo giờ hôm nay, xu hướng 12 tháng, xếp hạng, quá hạn,
/// cơ cấu theo loại bạn đọc, tải theo kho, tài liệu chưa từng mượn. Mọi ngày giờ theo giờ Việt Nam.
/// <see cref="OverdueReaders"/>/<see cref="OverdueAging"/>: lượt đang mượn đã quá hạn (nhóm 1–7 / 8–30 / trên 30 ngày). <see cref="ActiveReaders"/>:
/// số bạn đọc khác nhau có mượn trong 12 tháng; <see cref="Loans12Months"/>: số lượt mượn 12 tháng (để tính vòng quay tài liệu).
/// </summary>
public sealed record CirculationDashboard(
    IReadOnlyList<DailyActivityPoint> Daily, int DailyBorrowed, int DailyReturned,
    IReadOnlyList<HourlyActivityPoint> Hourly, int HourlyBorrowed, int HourlyReturned,
    MonthlySeries Trend, IReadOnlyList<TopLoanedTitle> TopBorrowed,
    int OverdueReaders, int OverdueLoans, IReadOnlyList<OverdueBucket> OverdueAging,
    IReadOnlyList<NamedCount> BorrowerTypes, int ActiveReaders, int Loans12Months,
    IReadOnlyList<StoreLoanCount> StoreUsage, IReadOnlyList<UnusedTitle> Unused, int UnusedTotal);

public sealed class CirculationDashboardService(ICrudDbContext db, TimeProvider clock)
{
    private const int Top = 10;
    private const int UnusedTop = 20;
    private static readonly string[] OutOfStockStatuses = [Loan.LostStatus, "S", "X"];

    public async Task<CirculationDashboard> BuildAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DashboardSeries.Today(clock);
        var loans = db.Set<Loan>().AsNoTracking();
        var year = loans.Where(l => l.LoanedAt >= DashboardSeries.StartOf(DashboardSeries.MonthStart(clock, 11)));
        var from30 = DashboardSeries.StartOf(today.AddDays(-29));

        var borrowed30 = await loans.Where(l => l.LoanedAt >= from30).OrderByDescending(l => l.LoanedAt).Select(l => l.LoanedAt).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var returned30 = await loans.Where(l => l.ReturnedAt != null && l.ClosedItemStatus == null && l.ReturnedAt >= from30)
            .Select(l => l.ReturnedAt!.Value).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var borrowedDays = DashboardSeries.Days(borrowed30, clock);
        var returnedDays = DashboardSeries.Days(returned30, clock);
        var daily = borrowedDays.Select((b, i) => new DailyActivityPoint(b.Date, b.Label, b.Value, returnedDays[i].Value)).ToList();

        var borrowedHours = DashboardSeries.Hours(borrowed30, today);
        var returnedHours = DashboardSeries.Hours(returned30, today);
        var hourly = Enumerable.Range(0, 24).Select(h => new HourlyActivityPoint(h, $"{h:00}h", borrowedHours[h], returnedHours[h])).ToList();

        var yearStamps = await year.Select(l => l.LoanedAt).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var trend = DashboardSeries.Months(yearStamps, clock);

        var topRows = await year.GroupBy(l => l.Mfn).Select(g => new { Mfn = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Mfn).Take(Top).ToListAsync(ct);
        var topMfns = topRows.Select(x => x.Mfn).ToList();
        var titles = await db.Set<BibSnapshot>().AsNoTracking().Where(b => topMfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, ct);
        var top = topRows.Select(x => titles.TryGetValue(x.Mfn, out var b) ? new TopLoanedTitle(x.Mfn, b.Title, b.Author, x.Count) : new TopLoanedTitle(x.Mfn, $"#{x.Mfn}", null, x.Count)).ToList();

        var open = await loans.Where(l => l.ReturnedAt == null && l.DueAt < now).Select(l => new { l.ReaderPublicId, l.DueAt }).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var late = open.Select(l => Loan.LocalDate(now).DayNumber - Loan.LocalDate(l.DueAt).DayNumber).ToList();
        OverdueBucket[] aging =
        [
            new("1–7", late.Count(d => d is >= 1 and <= 7)),
            new("8–30", late.Count(d => d is >= 8 and <= 30)),
            new("30+", late.Count(d => d > 30)),
        ];

        var types = await (from l in year
                           join p in db.Set<PatronReplica>().AsNoTracking() on l.ReaderPublicId equals p.ReaderPublicId
                           group l by p.ReaderTypeName into g
                           select new { Name = g.Key, Count = g.Count() }).ToListAsync(ct);
        var borrowerTypes = types.OrderByDescending(t => t.Count).Select(t => new NamedCount(t.Name ?? "", t.Count)).ToList();

        var active = await year.Select(l => l.ReaderPublicId).Distinct().CountAsync(ct);
        var yearLoans = await year.CountAsync(ct);

        var storeRows = await year.Where(l => l.StoreId != null).GroupBy(l => l.StoreId).Select(g => new { Id = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Id).Take(Top).ToListAsync(ct);
        var storeIds = storeRows.Select(x => x.Id).ToList();
        var storeNames = (await db.Set<ItemReplica>().AsNoTracking().Where(i => i.StoreId != null && storeIds.Contains(i.StoreId) && i.StoreName != null)
            .Select(i => new { StoreId = i.StoreId!.Value, i.StoreName }).Distinct().ToListAsync(ct)).GroupBy(x => x.StoreId).ToDictionary(g => g.Key, g => g.First().StoreName!);
        var stores = storeRows.Select(x => new StoreLoanCount(x.Id, x.Id is { } id && storeNames.TryGetValue(id, out var name) ? name : $"#{x.Id}", x.Count)).ToList();

        var neverLoaned = db.Set<ItemReplica>().AsNoTracking()
            .Where(i => !i.Deleted && !OutOfStockStatuses.Contains(i.Status) && !loans.Any(l => l.Mfn == i.Mfn));
        var unusedTotal = await neverLoaned.GroupBy(i => i.Mfn).CountAsync(ct);
        var unusedRows = await neverLoaned.GroupBy(i => i.Mfn).Select(g => new { Mfn = g.Key, Copies = g.Count(), Store = g.Max(i => i.StoreName) })
            .OrderByDescending(x => x.Copies).ThenBy(x => x.Mfn).Take(UnusedTop).ToListAsync(ct);
        var unusedMfns = unusedRows.Select(x => x.Mfn).ToList();
        var unusedBibs = await db.Set<BibSnapshot>().AsNoTracking().Where(b => unusedMfns.Contains(b.Mfn)).ToDictionaryAsync(b => b.Mfn, ct);
        var unused = unusedRows.Select(x => unusedBibs.TryGetValue(x.Mfn, out var b)
            ? new UnusedTitle(x.Mfn, b.Title, b.Author, x.Store, x.Copies) : new UnusedTitle(x.Mfn, $"#{x.Mfn}", null, x.Store, x.Copies)).ToList();

        return new CirculationDashboard(daily, borrowedDays.Sum(d => d.Value), returnedDays.Sum(d => d.Value),
            hourly, borrowedHours.Sum(), returnedHours.Sum(), trend, top,
            open.Select(l => l.ReaderPublicId).Distinct().Count(), open.Count, aging,
            borrowerTypes, active, yearLoans, stores, unused, unusedTotal);
    }
}
