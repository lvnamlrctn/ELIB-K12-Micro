using System.Globalization;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Circulation.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Elib.Circulation.Infrastructure;

/// <summary>
/// Tham số lưu thông của đơn vị đọc từ service tenant (GET /internal/tenants/{id}/parameters?service=circulation, service token), cache 60 giây
/// theo đơn vị (đổi tham số có hiệu lực sau tối đa một phút). Tenant không trả lời → dùng mặc định và ghi cảnh báo, không cache.
/// </summary>
public sealed partial class HttpCirculationSettings(IHttpClientFactory httpClients, IMemoryCache cache, ITenantContext tenant, ILogger<HttpCirculationSettings> logger)
    : ICirculationSettings
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<CirculationSettings> GetAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var key = "circulation-settings:" + tenantId.ToString(CultureInfo.InvariantCulture);
        if (cache.TryGetValue(key, out CirculationSettings? cached) && cached is not null) return cached;
        try
        {
            var values = await httpClients.CreateClient(TenantReplicaBootstrapper<CirculationDbContext>.HttpClientName).GetFromJsonAsync<Dictionary<string, string?>>(
                new Uri($"internal/tenants/{tenantId.ToString(CultureInfo.InvariantCulture)}/parameters?service=circulation", UriKind.Relative), ct) ?? [];
            var settings = CirculationSettings.From(values);
            cache.Set(key, settings, Ttl);
            return settings;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            LogUnavailable(logger, ex, tenantId);
            return CirculationSettings.Default;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không đọc được tham số lưu thông của đơn vị {TenantId} — dùng mặc định")]
    private static partial void LogUnavailable(ILogger logger, Exception exception, long tenantId);
}
