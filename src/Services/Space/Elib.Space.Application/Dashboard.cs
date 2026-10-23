using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

/// <summary>Không gian đang hoạt động (không tính đã khoá): Occupied = có lượt đặt đã check-in chưa check-out; Free = còn lại.</summary>
public sealed record OccupancyNow(int TotalSpaces, int Occupied, int Free);

/// <summary>
/// 30 ngày gần nhất theo giờ bắt đầu: Total = lượt đã có kết cục (hoàn tất, đang dùng hoặc không tới), NoShow = lượt không tới.
/// <see cref="RatePercent"/> = null khi chưa có lượt nào (khác 0% thật).
/// </summary>
public sealed record NoShowStat(int Total, int NoShow, double? RatePercent);

public sealed record SpaceDashboard(OccupancyNow OccupancyNow, NoShowStat NoShow30, int PendingRequests);

public sealed class SpaceDashboardService(ICrudDbContext db, TimeProvider clock)
{
    public async Task<SpaceDashboard> BuildAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var spaces = await db.Set<BookableSpace>().AsNoTracking().Where(s => s.Status == IHasStatus.Active && !s.Maintenance).Select(s => s.Id).ToListAsync(ct);
        var inUse = await db.Set<RoomBooking>().AsNoTracking().Where(b => b.Status == RoomBookingStatus.CheckedIn && b.CheckedOutAt == null)
            .Select(b => b.SpaceId).Distinct().ToListAsync(ct);
        var occupied = inUse.Count(spaces.Contains);

        var from = DashboardSeries.StartOf(DashboardSeries.Today(clock).AddDays(-29));
        var outcomes = await db.Set<RoomBooking>().AsNoTracking().Where(b => b.StartAt >= from && b.StartAt <= now
                && (b.Status == RoomBookingStatus.Completed || b.Status == RoomBookingStatus.CheckedIn || b.Status == RoomBookingStatus.NoShow))
            .GroupBy(b => b.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var total = outcomes.Sum(o => o.Count);
        var noShow = outcomes.Where(o => o.Status == RoomBookingStatus.NoShow).Sum(o => o.Count);
        var pending = await db.Set<RoomBooking>().AsNoTracking().CountAsync(b => b.Status == RoomBookingStatus.Pending, ct);
        return new SpaceDashboard(new OccupancyNow(spaces.Count, occupied, spaces.Count - occupied),
            new NoShowStat(total, noShow, total == 0 ? null : Math.Round(100.0 * noShow / total, 1)), pending);
    }
}
