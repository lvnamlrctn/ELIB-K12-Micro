using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Patron.Application;

/// <summary>
/// Số liệu bạn đọc của một đơn vị. Hiện tại: <see cref="Total"/>, <see cref="Locked"/> (bị khoá), <see cref="Expired"/> (thẻ đã hết hạn).
/// Trong kỳ: <see cref="Added"/> (thêm mới theo ngày tạo hồ sơ).
/// </summary>
public sealed record ReaderPlatformStats(long TenantId, int Total, int Locked, int Expired, int Added);

/// <summary>Thống kê bạn đọc toàn hệ thống — quản trị nền tảng (gateway /api/system/patron/stats/** → /api/system/stats/**).</summary>
public sealed class PlatformReaderStatsService(ICrudDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<ReaderPlatformStats>> ReadersAsync(PlatformStatsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (start, end) = request.Period();
        var today = PlatformStatsRequest.Today(clock);
        var readers = request.Scope(db.Set<Reader>().AsNoTracking());

        var total = await PlatformStats.CountByTenantAsync(readers, ct);
        var locked = await PlatformStats.CountByTenantAsync(readers.Where(r => r.Status != IHasStatus.Active), ct);
        var expired = await PlatformStats.CountByTenantAsync(readers.Where(r => r.ExpireDate != null && r.ExpireDate < today), ct);
        var added = readers;
        if (start is { } s) added = added.Where(r => r.CreatedAt >= s);
        if (end is { } e) added = added.Where(r => r.CreatedAt < e);
        var addedCounts = await PlatformStats.CountByTenantAsync(added, ct);

        return [.. PlatformStats.Tenants(total.Keys, addedCounts.Keys)
            .Select(t => new ReaderPlatformStats(t, total.Of(t), locked.Of(t), expired.Of(t), addedCounts.Of(t)))];
    }
}
