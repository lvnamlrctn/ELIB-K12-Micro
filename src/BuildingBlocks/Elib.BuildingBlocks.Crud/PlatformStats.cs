using Elib.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.Crud;

/// <summary>
/// Thống kê toàn hệ thống của quản trị nền tảng (endpoint /api/system/stats của từng service, [RequireSystemContext]).
/// <see cref="TenantIds"/> rỗng = mọi đơn vị. <see cref="From"/>/<see cref="To"/>: khoảng ngày giờ Việt Nam cho số liệu "trong kỳ"; bỏ trống =
/// không giới hạn. Số liệu "hiện tại" (tổng bạn đọc, đang mượn…) không theo khoảng ngày.
/// </summary>
public sealed record PlatformStatsRequest(IReadOnlyList<long>? TenantIds = null, DateOnly? From = null, DateOnly? To = null)
{
    public const int MaxTenants = 1000;

    /// <summary>Đầu kỳ (0 giờ ngày From, giờ VN) và cuối kỳ (0 giờ ngày sau To), biểu diễn UTC — Npgsql chỉ nhận timestamptz offset 0.</summary>
    public (DateTimeOffset? Start, DateTimeOffset? End) Period()
    {
        if (From is { } f && To is { } t && f > t) throw new BusinessRuleException("DATE_RANGE_INVALID", "Từ ngày phải trước đến ngày.");
        return (From is { } from ? StartOf(from) : null, To is { } to ? StartOf(to.AddDays(1)) : null);
    }

    /// <summary>Hôm nay theo giờ Việt Nam.</summary>
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));

    /// <summary>
    /// Bảng của mọi đơn vị (bỏ bộ lọc đơn vị của EF — ngữ cảnh hệ thống thấy rỗng nếu giữ; RLS cho qua nhờ app.tenant_system), giới hạn ở
    /// <see cref="TenantIds"/> nếu có. Bộ lọc xoá mềm vẫn giữ.
    /// </summary>
    public IQueryable<T> Scope<T>(IQueryable<T> source) where T : class, ITenantOwned
    {
        var ids = (TenantIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (ids.Count > MaxTenants) throw new BusinessRuleException("TENANT_IDS_TOO_MANY", $"Chọn tối đa {MaxTenants} đơn vị.");
        var query = source.IgnoreQueryFilters(["Tenant"]); // ElibQueryFilters.Tenant (Persistence)
        return ids.Count == 0 ? query : query.Where(x => ids.Contains(x.TenantId));
    }

    private static DateTimeOffset StartOf(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
}

public static class PlatformStats
{
    /// <summary>Số dòng theo đơn vị — mỗi chỉ số một truy vấn GROUP BY tenant_id đơn giản (dịch được trên Postgres lẫn SQLite của test).</summary>
    public static async Task<Dictionary<long, int>> CountByTenantAsync<T>(IQueryable<T> query, CancellationToken ct) where T : class, ITenantOwned =>
        await query.GroupBy(x => x.TenantId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    /// <summary>Giá trị của đơn vị trong một chỉ số (đơn vị không có dòng nào = 0).</summary>
    public static int Of(this Dictionary<long, int> counts, long tenantId) => counts.GetValueOrDefault(tenantId);

    /// <summary>Mọi đơn vị có số liệu ở ít nhất một chỉ số, sắp theo id.</summary>
    public static IReadOnlyList<long> Tenants(params IEnumerable<long>[] keys) => [.. keys.SelectMany(k => k).Distinct().Order()];
}
