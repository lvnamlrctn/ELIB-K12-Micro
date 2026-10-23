using Elib.BuildingBlocks.Crud;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Portal.Application;

public sealed record VisitTotals(long Total, long Today, long Last7Days, long Last30Days);

/// <summary>Bảng điều khiển cổng thông tin (GET /api/dashboard): tổng lượt truy cập và xu hướng 12 tháng (cộng Hits của từng ngày theo tháng, giờ Việt Nam).</summary>
public sealed record PortalDashboard(VisitTotals Visits, MonthlySeries Trend);

public sealed class PortalDashboardService(ICrudDbContext db, TimeProvider clock)
{
    public async Task<PortalDashboard> BuildAsync(CancellationToken ct)
    {
        var today = DashboardSeries.Today(clock);
        var days = await db.Set<CounterDay>().AsNoTracking().Select(c => new { c.Day, c.Hits }).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var visits = new VisitTotals(days.Sum(d => d.Hits), days.Where(d => d.Day == today).Sum(d => d.Hits),
            days.Where(d => d.Day > today.AddDays(-7)).Sum(d => d.Hits), days.Where(d => d.Day > today.AddDays(-30)).Sum(d => d.Hits));

        const int months = 12;
        var first = DashboardSeries.MonthStart(clock, months - 1);
        var counts = new int[months];
        foreach (var d in days)
        {
            var index = (d.Day.Year - first.Year) * 12 + d.Day.Month - first.Month;
            if (index >= 0 && index < months) counts[index] = (int)Math.Min(int.MaxValue, counts[index] + d.Hits);
        }
        return new PortalDashboard(visits, DashboardSeries.Months(counts, first));
    }
}
