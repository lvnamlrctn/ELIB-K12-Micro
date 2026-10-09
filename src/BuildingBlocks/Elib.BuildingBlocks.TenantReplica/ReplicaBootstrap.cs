using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.TenantReplica;

public sealed class TenantReplicaBootstrapOptions
{
    public const string SectionName = "TenantReplica";

    /// <summary>URL nội bộ của service tenant (ví dụ http://tenant:8080). Trống = không tự dựng bản sao (test, service tenant).</summary>
    public string TenantUrl { get; set; } = "";
}

/// <summary>
/// Service triển khai SAU khi đã có đơn vị (ví dụ thêm catalog vào hệ đang chạy) chưa từng nhận TenantProvisioned/TenantUpdated:
/// lúc khởi động, nếu bản sao đơn vị còn trống thì lấy trạng thái mọi đơn vị từ GET /internal/tenants/replicas (service token),
/// ghi bản sao theo đúng quy tắc SourceVersion như khi nhận event, rồi chạy seeder (idempotent) cho từng đơn vị đang hoạt động.
/// Bản sao đã có dữ liệu thì không làm gì — event qua bus giữ đồng bộ.
/// </summary>
public sealed partial class TenantReplicaBootstrapper<TDbContext>(
    IServiceScopeFactory scopes, IHttpClientFactory httpClients, TimeProvider clock, ILogger<TenantReplicaBootstrapper<TDbContext>> logger)
    : BackgroundService
    where TDbContext : DbContext
{
    public const string HttpClientName = "elib-tenant-replicas";

    /// <summary>Service trả enum dạng chuỗi ("Active") — đọc được cả chuỗi lẫn số.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly TimeSpan[] Retries = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var count = await RunOnceAsync(stoppingToken);
                if (count > 0) LogBootstrapped(logger, count);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // tenant/identity chưa lên (khởi động cùng lúc) — thử lại vài lần rồi thôi; event vẫn đồng bộ đơn vị mới.
                if (attempt >= Retries.Length)
                {
                    LogGaveUp(logger, ex);
                    return;
                }
                LogRetry(logger, ex, Retries[attempt].TotalSeconds);
                await Task.Delay(Retries[attempt], clock, stoppingToken);
            }
        }
    }

    /// <summary>Dựng bản sao nếu còn trống. Trả về số đơn vị đã ghi (0 = bản sao đã có, không làm gì).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using (var probe = scopes.CreateScope())
        {
            if (await probe.ServiceProvider.GetRequiredService<TDbContext>().Set<TenantReplicaRecord>().AnyAsync(ct)) return 0;
        }

        var snapshots = await httpClients.CreateClient(HttpClientName)
            .GetFromJsonAsync<List<TenantReplicaSnapshot>>(new Uri("internal/tenants/replicas", UriKind.Relative), Json, ct) ?? [];

        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var now = clock.GetUtcNow();
            foreach (var snapshot in snapshots)
            {
                await ReplicaWriter.ApplyTenantAsync(db, snapshot.Tenant, now, ct);
                await db.SaveChangesAsync(ct); // bản sao mới phải có trong DB trước khi ghi license (tra lại theo TenantId)
                await ReplicaWriter.ApplyLicensesAsync(db, snapshot.Licenses, now, ct);
                await db.SaveChangesAsync(ct);
            }
            var cache = scope.ServiceProvider.GetRequiredService<HybridCache>();
            foreach (var snapshot in snapshots) await ReplicaWriter.InvalidateAsync(cache, snapshot.Tenant.TenantId, ct);
        }

        foreach (var snapshot in snapshots.Where(s => s.Tenant.Status == TenantStatus.Active))
        {
            using var scope = scopes.CreateScope();
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(snapshot.Tenant.TenantId);
            var provisioned = new TenantProvisioned
            {
                TenantId = snapshot.Tenant.TenantId,
                Code = snapshot.Tenant.Code,
                Name = snapshot.Tenant.Name,
                Subdomain = snapshot.Tenant.Subdomain,
                TimeZone = snapshot.Tenant.TimeZone,
                ParentOrgId = snapshot.Tenant.ParentOrgId,
                Modules = snapshot.Licenses.Modules,
            };
            try
            {
                foreach (var seeder in scope.ServiceProvider.GetServices<ITenantSeeder>()) await seeder.SeedAsync(provisioned, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSeedFailed(logger, ex, snapshot.Tenant.TenantId); // đơn vị khác vẫn seed tiếp; quản trị chạy lại "khôi phục mặc định"
            }
        }
        return snapshots.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã dựng bản sao {Count} đơn vị từ service tenant (service mới triển khai)")]
    private static partial void LogBootstrapped(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chưa dựng được bản sao đơn vị — thử lại sau {Seconds} giây")]
    private static partial void LogRetry(ILogger logger, Exception exception, double seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Không dựng được bản sao đơn vị từ service tenant — dùng \"Đồng bộ lại\" ở màn Đơn vị")]
    private static partial void LogGaveUp(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Seed dữ liệu mặc định cho đơn vị {TenantId} khi dựng bản sao thất bại")]
    private static partial void LogSeedFailed(ILogger logger, Exception exception, long tenantId);
}

public static class TenantReplicaBootstrapServiceCollectionExtensions
{
    /// <summary>
    /// Tự dựng bản sao đơn vị khi service khởi động với bản sao trống (cấu hình TenantReplica:TenantUrl; trống = tắt).
    /// Cần service token: gọi sau <c>AddElibIdentityClient</c> (ServiceClient:*).
    /// </summary>
    public static IServiceCollection AddTenantReplicaBootstrap<TDbContext>(this IServiceCollection services, IConfiguration configuration)
        where TDbContext : DbContext
    {
        var options = configuration.GetSection(TenantReplicaBootstrapOptions.SectionName).Get<TenantReplicaBootstrapOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(options.TenantUrl)) return services;

        services.AddElibServiceTokens();
        services.AddHttpClient(TenantReplicaBootstrapper<TDbContext>.HttpClientName, c =>
            {
                c.BaseAddress = new Uri(options.TenantUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddHostedService<TenantReplicaBootstrapper<TDbContext>>();
        return services;
    }
}
