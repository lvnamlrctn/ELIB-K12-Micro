using Elib.BuildingBlocks.Crud;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Một nhóm trong cơ cấu tài liệu. <see cref="Name"/> rỗng = chưa có loại; <see cref="Other"/> = gộp các nhóm nhỏ còn lại.</summary>
public sealed record DistributionItem(string Name, int Count, int Percent, bool Other = false);

/// <summary>Kho kèm số bản và sức chứa; <see cref="Percent"/> null khi kho chưa khai báo sức chứa.</summary>
public sealed record StoreFill(long StoreId, string Name, int Count, int? Capacity, int? Percent);

/// <summary>
/// Bảng điều khiển kho tài liệu in của đơn vị (GET /api/dashboard). <see cref="Titles"/>/<see cref="Copies"/>: đầu sách / bản còn trong kho (không tính
/// mất, đã thanh lý — như thống kê nền tảng). <see cref="PrintAdded"/>: bản đăng ký 12 tháng gần nhất (giờ Việt Nam).
/// </summary>
public sealed record HoldingsDashboard(
    int Titles, int Copies, int Unshelved, int OnLoan, int Lost, IReadOnlyList<DistributionItem> Distribution, int DistributionTotal,
    MonthlySeries PrintAdded, IReadOnlyList<StoreFill> Stores);

public sealed class HoldingsDashboardService(ICrudDbContext db, TimeProvider clock)
{
    private const int TopGroups = 8;

    public async Task<HoldingsDashboard> BuildAsync(CancellationToken ct)
    {
        var items = db.Set<Item>().AsNoTracking();
        var inStock = items.Where(i => i.Status != ItemStatus.Lost && i.Status != ItemStatus.Liquidated);

        var copies = await inStock.CountAsync(ct);
        var titles = await inStock.Select(i => i.BibId).Distinct().CountAsync(ct);
        var unshelved = await items.CountAsync(i => i.Status == ItemStatus.Unshelved, ct);
        var onLoan = await inStock.CountAsync(i => i.OnLoan, ct);
        var lost = await items.CountAsync(i => i.Status == ItemStatus.Lost, ct);

        // Cơ cấu theo loại biểu ghi: mỗi đầu sách còn bản trong kho tính một lần.
        var groups = await db.Set<BibSnapshot>().AsNoTracking()
            .Where(b => !b.Deleted && inStock.Any(i => i.BibPublicId == b.BibPublicId))
            .GroupBy(b => b.BibTypeName).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var total = groups.Sum(g => g.Count);
        var ordered = groups.OrderByDescending(g => g.Count).ThenBy(g => g.Key).ToList();
        int Pct(int n) => total == 0 ? 0 : (int)Math.Round(100.0 * n / total);
        var distribution = ordered.Take(TopGroups).Select(g => new DistributionItem(g.Key ?? "", g.Count, Pct(g.Count))).ToList();
        var rest = ordered.Skip(TopGroups).Sum(g => g.Count);
        if (rest > 0) distribution.Add(new DistributionItem("", rest, Pct(rest), Other: true));

        var from = DashboardSeries.StartOf(DashboardSeries.MonthStart(clock, 11));
        var added = DashboardSeries.Months(
            await items.Where(i => i.CreatedAt >= from).Select(i => i.CreatedAt).Take(DashboardSeries.RowCap).ToListAsync(ct), clock);

        // Số bản theo kho tính theo mọi bản gắn kho (như màn Kho: ItemCount).
        var counts = await items.Where(i => i.StoreId != null).GroupBy(i => i.StoreId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
        var countOf = counts.Where(c => c.Id != null).ToDictionary(c => c.Id!.Value, c => c.Count);
        var stores = (await db.Set<Store>().AsNoTracking().OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Capacity }).ToListAsync(ct))
            .Select(s => new StoreFill(s.Id, s.Name, countOf.GetValueOrDefault(s.Id), s.Capacity,
                s.Capacity is > 0 ? (int)Math.Round(100.0 * countOf.GetValueOrDefault(s.Id) / s.Capacity.Value) : null))
            .ToList();

        return new HoldingsDashboard(titles, copies, unshelved, onLoan, lost, distribution, total, added, stores);
    }
}
