using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

/// <summary>Bạn đọc hiện tại: Locked = bị khoá; Expired = thẻ hết hạn (theo ngày Việt Nam); Active = chưa khoá và chưa hết hạn.</summary>
public sealed record ReaderCounts(int Total, int Locked, int Expired, int Active);

/// <summary>Bảng điều khiển bạn đọc của đơn vị (GET /api/dashboard): <see cref="Added12"/> = bạn đọc mới 12 tháng theo ngày tạo hồ sơ, <see cref="ByType"/> = top 8 loại bạn đọc.</summary>
public sealed record PatronDashboard(ReaderCounts Readers, MonthlySeries Added12, IReadOnlyList<NamedCount> ByType);

public sealed class PatronDashboardService(ICrudDbContext db, TimeProvider clock)
{
    private const int TopTypes = 8;

    public async Task<PatronDashboard> BuildAsync(CancellationToken ct)
    {
        var today = DashboardSeries.Today(clock);
        var readers = db.Set<Reader>().AsNoTracking();
        var total = await readers.CountAsync(ct);
        var locked = await readers.CountAsync(r => r.Status != IHasStatus.Active, ct);
        var expired = await readers.CountAsync(r => r.ExpireDate != null && r.ExpireDate < today, ct);
        var active = await readers.CountAsync(r => r.Status == IHasStatus.Active && (r.ExpireDate == null || r.ExpireDate >= today), ct);

        var from = DashboardSeries.StartOf(DashboardSeries.MonthStart(clock, 11));
        var stamps = await readers.Where(r => r.CreatedAt >= from).Select(r => r.CreatedAt).Take(DashboardSeries.RowCap).ToListAsync(ct);

        var rows = await readers.GroupBy(r => r.ReaderTypeId).Select(g => new { TypeId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.TypeId).Take(TopTypes).ToListAsync(ct);
        var ids = rows.Where(x => x.TypeId != null).Select(x => x.TypeId!.Value).ToList();
        var names = await db.Set<ReaderType>().AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var byType = rows.Select(x => new NamedCount(x.TypeId is { } id && names.TryGetValue(id, out var n) ? n : "", x.Count)).ToList();
        return new PatronDashboard(new ReaderCounts(total, locked, expired, active), DashboardSeries.Months(stamps, clock), byType);
    }
}
