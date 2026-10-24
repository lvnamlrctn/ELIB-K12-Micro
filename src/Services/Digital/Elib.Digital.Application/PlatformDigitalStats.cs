using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

/// <summary>
/// Tài liệu số của một đơn vị. Hiện tại (không theo kỳ): <see cref="Titles"/> (đầu tài liệu), <see cref="Published"/> (đang xuất bản), <see cref="Files"/> và
/// <see cref="StorageBytes"/> (tổng kích thước KHAI BÁO của file, byte — không phải dung lượng thật trên kho), <see cref="ActiveLoans"/> (đang mượn,
/// chưa hết hạn), <see cref="PendingReservations"/> (đặt trước còn hiệu lực). Trong kỳ: <see cref="Added"/> (theo ngày nộp, thiếu thì ngày tạo), <see cref="Views"/>,
/// <see cref="Downloads"/> (nhật ký truy cập), <see cref="ReadSessions"/>, <see cref="Readers"/> (bạn đọc khác nhau có phiên đọc), <see cref="Loans"/>, <see cref="Reservations"/>.
/// </summary>
public sealed record DigitalPlatformStats(long TenantId, int Titles, int Published, int Files, long StorageBytes, int ActiveLoans, int PendingReservations,
    int Added, int Views, int Downloads, int ReadSessions, int Readers, int Loans, int Reservations);

/// <summary>Thống kê tài liệu số toàn hệ thống — quản trị nền tảng (gateway /api/system/digital/** → /api/system/**).</summary>
public sealed class PlatformDigitalStatsService(ICrudDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<DigitalPlatformStats>> BuildAsync(PlatformStatsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (start, end) = request.Period();
        var now = clock.GetUtcNow();

        var items = request.Scope(db.Set<EbookItem>().AsNoTracking());
        var titles = await PlatformStats.CountByTenantAsync(items, ct);
        var published = await PlatformStats.CountByTenantAsync(items.Where(i => i.Status == IHasStatus.Active), ct);

        // Ngày bổ sung = ngày nộp, thiếu thì ngày tạo (như bảng điều khiển của đơn vị).
        var added = items;
        if (start is { } s) added = added.Where(i => (i.Submited ?? i.CreatedAt) >= s);
        if (end is { } e) added = added.Where(i => (i.Submited ?? i.CreatedAt) < e);
        var addedCounts = await PlatformStats.CountByTenantAsync(added, ct);

        // Dung lượng: cộng ở bộ nhớ (SUM double không dịch được trên SQLite của test).
        var fileRows = await request.Scope(db.Set<EbookFile>().AsNoTracking()).Select(f => new { f.TenantId, f.FileSize }).Take(DashboardSeries.RowCap * 5).ToListAsync(ct);
        var files = fileRows.GroupBy(f => f.TenantId).ToDictionary(g => g.Key, g => (Count: g.Count(), Bytes: (long)g.Sum(f => f.FileSize ?? 0)));

        var access = request.Scope(db.Set<EbookAccessLog>().AsNoTracking());
        if (start is { } s2) access = access.Where(a => a.At >= s2);
        if (end is { } e2) access = access.Where(a => a.At < e2);
        var views = await PlatformStats.CountByTenantAsync(access.Where(a => a.Kind == EbookAccessLog.View), ct);
        var downloads = await PlatformStats.CountByTenantAsync(access.Where(a => a.Kind == EbookAccessLog.Download), ct);

        var sessions = request.Scope(db.Set<EbookReadSession>().AsNoTracking());
        if (start is { } s3) sessions = sessions.Where(x => x.StartedAt >= s3);
        if (end is { } e3) sessions = sessions.Where(x => x.StartedAt < e3);
        var sessionCounts = await PlatformStats.CountByTenantAsync(sessions, ct);
        var readers = await sessions.Select(x => new { x.TenantId, x.ReaderPublicId }).Distinct()
            .GroupBy(x => x.TenantId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var allLoans = request.Scope(db.Set<EbookItemLoan>().AsNoTracking());
        var activeLoans = await PlatformStats.CountByTenantAsync(allLoans.Where(l => l.Status == EbookLoanStatus.Active && (l.ExpiresAt == null || l.ExpiresAt >= now)), ct);
        var loans = allLoans;
        if (start is { } s4) loans = loans.Where(l => l.CheckedOutAt >= s4);
        if (end is { } e4) loans = loans.Where(l => l.CheckedOutAt < e4);
        var loanCounts = await PlatformStats.CountByTenantAsync(loans, ct);

        var allReservations = request.Scope(db.Set<EbookItemReservation>().AsNoTracking());
        var pending = await PlatformStats.CountByTenantAsync(
            allReservations.Where(r => r.Status == EbookReservationStatus.Pending || r.Status == EbookReservationStatus.Ready), ct);
        var reservations = allReservations;
        if (start is { } s5) reservations = reservations.Where(r => r.RequestedAt >= s5);
        if (end is { } e5) reservations = reservations.Where(r => r.RequestedAt < e5);
        var reservationCounts = await PlatformStats.CountByTenantAsync(reservations, ct);

        return [.. PlatformStats.Tenants(titles.Keys, addedCounts.Keys, files.Keys, views.Keys, downloads.Keys, sessionCounts.Keys, loanCounts.Keys, reservationCounts.Keys, activeLoans.Keys, pending.Keys)
            .Select(t => new DigitalPlatformStats(t, titles.Of(t), published.Of(t), files.GetValueOrDefault(t).Count, files.GetValueOrDefault(t).Bytes, activeLoans.Of(t), pending.Of(t),
                addedCounts.Of(t), views.Of(t), downloads.Of(t), sessionCounts.Of(t), readers.GetValueOrDefault(t), loanCounts.Of(t), reservationCounts.Of(t)))];
    }
}
