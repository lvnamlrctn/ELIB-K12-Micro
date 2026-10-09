using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace Elib.Gateway;

/// <summary>Đơn vị theo tên miền, kèm module đang hiệu lực (do service tenant tính theo múi giờ đơn vị).</summary>
public sealed record TenantInfo(long TenantId, string Code, string Subdomain, string Status, IReadOnlyList<string> Modules)
{
    public bool IsActive => Status == "Active";
}

public interface ITenantDirectory
{
    /// <summary>null = không có đơn vị nào dùng tên miền này.</summary>
    Task<TenantInfo?> FindByHostAsync(string host, CancellationToken cancellationToken);
}

/// <summary>
/// Hỏi service tenant (/internal/tenants/by-host) bằng service token, cache ngắn.
/// Đổi license/khoá đơn vị có hiệu lực ở gateway sau tối đa <see cref="GatewayOptions.TenantCacheSeconds"/> giây.
/// </summary>
public sealed class HttpTenantDirectory(IHttpClientFactory httpClients, HybridCache cache, Microsoft.Extensions.Options.IOptions<GatewayOptions> options) : ITenantDirectory
{
    public const string HttpClientName = "elib-tenant";

    public async Task<TenantInfo?> FindByHostAsync(string host, CancellationToken cancellationToken)
    {
        var label = host.Split('.')[0].ToLowerInvariant();
        var seconds = options.Value.TenantCacheSeconds;
        var found = await cache.GetOrCreateAsync(
            $"gw-tenant:{label}",
            (httpClients, label),
            static async (state, ct) =>
            {
                using var response = await state.httpClients.CreateClient(HttpClientName)
                    .GetAsync(new Uri($"internal/tenants/by-host/{Uri.EscapeDataString(state.label)}", UriKind.Relative), ct);
                if (response.StatusCode == HttpStatusCode.NotFound) return new CachedTenant(null);
                response.EnsureSuccessStatusCode();
                return new CachedTenant(await response.Content.ReadFromJsonAsync<TenantInfo>(ct));
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromSeconds(seconds),
                LocalCacheExpiration = TimeSpan.FromSeconds(seconds),
            },
            cancellationToken: cancellationToken);
        return found.Tenant;
    }

    /// <summary>Bọc để cache được cả kết quả "không tồn tại" (tránh dội service tenant bằng host rác).</summary>
    public sealed record CachedTenant(TenantInfo? Tenant);
}
