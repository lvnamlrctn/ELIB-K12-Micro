using System.Globalization;
using Microsoft.Extensions.Caching.Hybrid;

namespace Elib.BuildingBlocks.Authorization;

/// <summary>
/// Kiểm tra quyền qua cache 2 tầng (HybridCache: bộ nhớ + Redis nếu cấu hình) phía trước <see cref="IPermissionSource"/>.
/// Khoá cache gắn permission stamp nên token mang stamp mới tự lấy quyền mới; event PermissionChanged gọi
/// <see cref="InvalidateUserAsync"/> để xoá ngay.
/// </summary>
public interface IPermissionChecker
{
    Task<bool> HasAnyAsync(long? tenantId, long userId, string? permissionStamp, IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    Task InvalidateUserAsync(long userId, CancellationToken cancellationToken);

    Task InvalidateTenantAsync(long tenantId, CancellationToken cancellationToken);
}

public sealed class PermissionChecker(IPermissionSource source, HybridCache cache) : IPermissionChecker
{
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        // Ngắn: replica không nhận event PermissionChanged (queue cạnh tranh) và không có Redis vẫn chỉ cũ tối đa 5 phút.
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };

    public async Task<bool> HasAnyAsync(long? tenantId, long userId, string? permissionStamp, IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var userTag = UserTag(userId);
        var tenantKey = tenantId?.ToString(CultureInfo.InvariantCulture) ?? "sys";
        var granted = await cache.GetOrCreateAsync(
            $"perm:{tenantKey}:{userId.ToString(CultureInfo.InvariantCulture)}:{permissionStamp ?? "-"}",
            (source, tenantId, userId, permissionStamp),
            static async (state, ct) => (await state.source.GetEffectiveAsync(state.tenantId, state.userId, state.permissionStamp, ct)).ToArray(),
            EntryOptions,
            tags: tenantId is { } t ? [userTag, TenantTag(t)] : [userTag],
            cancellationToken: cancellationToken);

        return granted.Contains(PermissionCodes.All) || codes.Any(c => granted.Contains(c, StringComparer.Ordinal));
    }

    public Task InvalidateUserAsync(long userId, CancellationToken cancellationToken)
        => cache.RemoveByTagAsync(UserTag(userId), cancellationToken).AsTask();

    public Task InvalidateTenantAsync(long tenantId, CancellationToken cancellationToken)
        => cache.RemoveByTagAsync(TenantTag(tenantId), cancellationToken).AsTask();

    private static string TenantTag(long tenantId) => "perm-tenant:" + tenantId.ToString(CultureInfo.InvariantCulture);

    private static string UserTag(long userId) => "perm-user:" + userId.ToString(CultureInfo.InvariantCulture);
}
