using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Circulation.Application;

/// <summary>
/// Mượn trả của một đơn vị. Trong kỳ: <see cref="Loans"/> (lượt mượn theo ngày mượn), <see cref="Returns"/> (lượt trả theo ngày trả, không tính
/// lượt đóng vì mất), <see cref="ReadersServed"/> (số bạn đọc khác nhau có mượn), <see cref="Lost"/> (lượt đóng vì mất tài liệu).
/// Hiện tại: <see cref="OnLoan"/> (đang mượn), <see cref="Overdue"/> (đang mượn đã quá hạn).
/// </summary>
public sealed record LoanPlatformStats(long TenantId, int Loans, int Returns, int ReadersServed, int Lost, int OnLoan, int Overdue);

/// <summary>
/// Phiếu phạt lập trong kỳ (theo ngày phạt) của một đơn vị. <see cref="Paid"/>/<see cref="Remaining"/> là số đã nộp / còn nợ của CÁC PHIẾU ĐÓ —
/// phiếu không lưu ngày nộp nên không có "tiền thu trong kỳ".
/// </summary>
public sealed record FinePlatformStats(long TenantId, int Tickets, decimal Total, decimal Discount, decimal Paid, decimal Remaining);

public sealed record CirculationPlatformStats(IReadOnlyList<LoanPlatformStats> Loans, IReadOnlyList<FinePlatformStats> Fines);

/// <summary>Thống kê mượn trả và phạt toàn hệ thống — quản trị nền tảng (gateway /api/system/circulation/** → /api/system/**).</summary>
public sealed class PlatformCirculationStatsService(ICrudDbContext db, TimeProvider clock)
{
    public async Task<CirculationPlatformStats> BuildAsync(PlatformStatsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (start, end) = request.Period();
        var now = clock.GetUtcNow();
        var loans = request.Scope(db.Set<Loan>().AsNoTracking());

        var borrowed = loans;
        if (start is { } s) borrowed = borrowed.Where(l => l.LoanedAt >= s);
        if (end is { } e) borrowed = borrowed.Where(l => l.LoanedAt < e);
        var closed = loans.Where(l => l.ReturnedAt != null);
        if (start is { } s2) closed = closed.Where(l => l.ReturnedAt >= s2);
        if (end is { } e2) closed = closed.Where(l => l.ReturnedAt < e2);

        var loanCounts = await PlatformStats.CountByTenantAsync(borrowed, ct);
        var returns = await PlatformStats.CountByTenantAsync(closed.Where(l => l.ClosedItemStatus == null), ct);
        var lost = await PlatformStats.CountByTenantAsync(closed.Where(l => l.ClosedItemStatus != null), ct);
        // Bạn đọc khác nhau: lấy cặp (đơn vị, bạn đọc) không trùng trước rồi mới đếm theo đơn vị.
        var served = await borrowed.Select(l => new { l.TenantId, l.ReaderPublicId }).Distinct()
            .GroupBy(x => x.TenantId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var open = loans.Where(l => l.ReturnedAt == null);
        var onLoan = await PlatformStats.CountByTenantAsync(open, ct);
        var overdue = await PlatformStats.CountByTenantAsync(open.Where(l => l.DueAt < now), ct);

        var tickets = request.Scope(db.Set<FineTicket>().AsNoTracking());
        if (start is { } s3) tickets = tickets.Where(t => t.FineDate >= s3);
        if (end is { } e3) tickets = tickets.Where(t => t.FineDate < e3);
        // Cộng tiền ở bộ nhớ: SUM decimal không dịch được trên SQLite (test); số phiếu phạt trong kỳ nhỏ.
        var fineRows = await tickets.Select(t => new { t.TenantId, t.Total, t.Discount, t.Paid, t.Remaining }).ToListAsync(ct);
        var fines = fineRows.GroupBy(f => f.TenantId).OrderBy(g => g.Key)
            .Select(g => new FinePlatformStats(g.Key, g.Count(), g.Sum(f => f.Total), g.Sum(f => f.Discount), g.Sum(f => f.Paid), g.Sum(f => f.Remaining)))
            .ToList();

        var loanStats = PlatformStats.Tenants(loanCounts.Keys, returns.Keys, lost.Keys, onLoan.Keys)
            .Select(t => new LoanPlatformStats(t, loanCounts.Of(t), returns.Of(t), served.GetValueOrDefault(t), lost.Of(t), onLoan.Of(t), overdue.Of(t)))
            .ToList();
        return new CirculationPlatformStats(loanStats, fines);
    }
}
