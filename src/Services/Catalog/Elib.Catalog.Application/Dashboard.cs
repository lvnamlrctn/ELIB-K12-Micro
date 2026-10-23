using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Catalog.Application;

/// <summary>
/// Bảng điều khiển biên mục của đơn vị (GET /api/dashboard): <see cref="Bibs"/> biểu ghi (chưa xoá), <see cref="Visible"/> đang hiện trên OPAC,
/// <see cref="NewByWeek"/> biểu ghi tạo mới 12 tuần gần nhất (mỗi cột là tuần bắt đầu từ thứ Hai, giờ Việt Nam), và số biểu ghi thiếu dữ liệu
/// quan trọng (tác giả, ký hiệu phân loại DDC, ảnh bìa).
/// </summary>
public sealed record CatalogDashboard(
    int Bibs, int Visible, IReadOnlyList<DayPoint> NewByWeek, int NewTotal, int MissingAuthor, int MissingDdc, int MissingCover);

public sealed class CatalogDashboardService(ICrudDbContext db, TimeProvider clock)
{
    private const int Weeks = 12;

    public async Task<CatalogDashboard> BuildAsync(CancellationToken ct)
    {
        var bibs = db.Set<Bib>().AsNoTracking();
        var today = DashboardSeries.Today(clock);
        var firstMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7) - 7 * (Weeks - 1));
        var from = DashboardSeries.StartOf(firstMonday);
        var stamps = await bibs.Where(b => b.CreatedAt >= from).Select(b => b.CreatedAt).Take(DashboardSeries.RowCap).ToListAsync(ct);
        var weeks = DashboardSeries.Weeks(stamps, clock, Weeks);

        return new CatalogDashboard(
            await bibs.CountAsync(ct), await bibs.CountAsync(b => b.Status == IHasStatus.Active, ct), weeks, weeks.Sum(w => w.Value),
            await bibs.CountAsync(b => b.Author == null || b.Author == "", ct),
            await bibs.CountAsync(b => b.Ddc == null || b.Ddc == "", ct),
            await bibs.CountAsync(b => b.CoverUrl == null || b.CoverUrl == "", ct));
    }
}
