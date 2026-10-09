using Elib.BuildingBlocks.Authorization;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.BuildingBlocks.TenantReplica;

/// <summary>
/// License đọc từ bản sao trong DB của service, cache ngắn (xoá ngay khi consumer cập nhật bản sao).
/// Đơn vị không ở trạng thái Active → không có module nào (fail-closed).
/// </summary>
public sealed class ReplicaModuleLicenseSource<TDbContext>(TDbContext db, HybridCache cache, TimeProvider clock) : IModuleLicenseSource
    where TDbContext : DbContext
{
    private static readonly HybridCacheEntryOptions Options = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };

    public async Task<bool> IsLicensedAsync(long tenantId, string moduleCode, CancellationToken cancellationToken)
    {
        var modules = await cache.GetOrCreateAsync(
            $"tenant-modules:{tenantId}",
            (db, tenantId, clock),
            static async (state, ct) => await LoadAsync(state.db, state.tenantId, state.clock, ct),
            Options,
            tags: [ReplicaWriter.CacheTag(tenantId)],
            cancellationToken: cancellationToken);

        return modules.Contains(moduleCode.Trim().ToUpperInvariant(), StringComparer.Ordinal);
    }

    private static async Task<string[]> LoadAsync(TDbContext db, long tenantId, TimeProvider clock, CancellationToken ct)
    {
        var replica = await db.Set<TenantReplicaRecord>().AsNoTracking().Include(t => t.Modules)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (replica is null || replica.Status != "Active") return [];

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(replica.TimeZone, out var tz) ? tz : TimeZoneInfo.Utc;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        return replica.Modules.Where(m => m.IsEffectiveOn(today)).Select(m => m.ModuleCode).ToArray();
    }
}

public static class TenantReplicaServiceCollectionExtensions
{
    /// <summary>
    /// Bản sao đơn vị + license cho service: đăng ký <see cref="IModuleLicenseSource"/>. Consumer đăng ký qua
    /// <see cref="AddTenantReplicaConsumers{TDbContext}"/> trong callback của AddElibMessaging. DbContext phải gọi
    /// <c>modelBuilder.AddTenantReplica()</c>. Seeder đăng ký bằng <c>services.AddScoped&lt;ITenantSeeder, ...&gt;()</c>.
    /// </summary>
    public static IServiceCollection AddTenantReplica<TDbContext>(this IServiceCollection services, string serviceName)
        where TDbContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        services.AddSingleton(new TenantReplicaServiceName(serviceName));
        services.AddHybridCache();
        services.AddScoped<IModuleLicenseSource, ReplicaModuleLicenseSource<TDbContext>>();
        return services;
    }

    public static IBusRegistrationConfigurator AddTenantReplicaConsumers<TDbContext>(this IBusRegistrationConfigurator bus)
        where TDbContext : DbContext
    {
        bus.AddConsumer<TenantProvisionedConsumer<TDbContext>>();
        bus.AddConsumer<TenantUpdatedConsumer<TDbContext>>();
        bus.AddConsumer<ModuleLicenseChangedConsumer<TDbContext>>();
        return bus;
    }
}
