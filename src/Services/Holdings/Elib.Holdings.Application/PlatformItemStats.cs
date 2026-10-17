using Elib.BuildingBlocks.Crud;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>
/// Tài liệu in (bản sách — ĐKCB) của một đơn vị. Hiện tại: <see cref="Copies"/>/<see cref="Titles"/> là số bản / số đầu sách còn trong kho
/// (không tính mất, đã thanh lý — như báo cáo sách trong kho), và số bản theo trạng thái. Trong kỳ: <see cref="Added"/> (bản đăng ký theo ngày
/// đăng ký cá biệt).
/// </summary>
public sealed record ItemPlatformStats(long TenantId, int Titles, int Copies, int Added, int Available, int Unshelved, int OnLoan, int OutOfStore,
    int Lost, int Liquidated);

/// <summary>Thống kê tài liệu in toàn hệ thống — quản trị nền tảng (gateway /api/system/holdings/** → /api/system/**).</summary>
public sealed class PlatformItemStatsService(ICrudDbContext db)
{
    public async Task<IReadOnlyList<ItemPlatformStats>> ItemsAsync(PlatformStatsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (start, end) = request.Period();
        var items = request.Scope(db.Set<Item>().AsNoTracking());
        var inStock = items.Where(i => i.Status != ItemStatus.Lost && i.Status != ItemStatus.Liquidated);

        var copies = await PlatformStats.CountByTenantAsync(inStock, ct);
        // Đầu sách: cặp (đơn vị, biểu ghi) không trùng rồi mới đếm theo đơn vị.
        var titles = await inStock.Select(i => new { i.TenantId, i.BibId }).Distinct()
            .GroupBy(x => x.TenantId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var added = items;
        if (start is { } s) added = added.Where(i => i.CreatedAt >= s);
        if (end is { } e) added = added.Where(i => i.CreatedAt < e);
        var addedCounts = await PlatformStats.CountByTenantAsync(added, ct);
        var available = await PlatformStats.CountByTenantAsync(items.Where(i => i.Status == ItemStatus.Available && !i.OnLoan), ct);
        var unshelved = await PlatformStats.CountByTenantAsync(items.Where(i => i.Status == ItemStatus.Unshelved), ct);
        var onLoan = await PlatformStats.CountByTenantAsync(inStock.Where(i => i.OnLoan), ct);
        var outOfStore = await PlatformStats.CountByTenantAsync(items.Where(i => i.Status == ItemStatus.OutOfStore), ct);
        var lost = await PlatformStats.CountByTenantAsync(items.Where(i => i.Status == ItemStatus.Lost), ct);
        var liquidated = await PlatformStats.CountByTenantAsync(items.Where(i => i.Status == ItemStatus.Liquidated), ct);

        return [.. PlatformStats.Tenants(copies.Keys, addedCounts.Keys, lost.Keys, liquidated.Keys)
            .Select(t => new ItemPlatformStats(t, titles.GetValueOrDefault(t), copies.Of(t), addedCounts.Of(t), available.Of(t), unshelved.Of(t),
                onLoan.Of(t), outOfStore.Of(t), lost.Of(t), liquidated.Of(t)))];
    }
}
