using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elib.Tenant.Application;

/// <summary>Theo dõi tiến độ khởi tạo đơn vị: nhận TenantSeeded và quét timeout (docs 03 §5.3).</summary>
public sealed class ProvisioningService(ITenantDb db, IPublishEndpoint publisher, TimeProvider clock, IOptions<ProvisioningOptions> options)
{
    public async Task HandleSeededAsync(TenantSeeded message, CancellationToken ct)
    {
        var tenant = await db.Tenants.Include(t => t.ProvisioningSteps).FirstOrDefaultAsync(t => t.Id == message.TenantId, ct);
        if (tenant is null) return;

        if (tenant.RecordSeeded(message.Service, message.Succeeded, message.Error, clock.GetUtcNow()))
            await publisher.Publish(EventFactory.Updated(tenant, EventActor.System), ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Chuyển các đơn vị chờ quá hạn sang ProvisioningFailed. Trả về số đơn vị bị đánh dấu.</summary>
    public async Task<int> SweepTimeoutsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // Lọc thời gian trong bộ nhớ: số đơn vị đang khởi tạo luôn nhỏ, và tránh khác biệt provider khi so sánh DateTimeOffset.
        var pending = await db.Tenants.Include(t => t.ProvisioningSteps)
            .Where(t => t.Status == TenantState.Provisioning).ToListAsync(ct);

        var timedOut = 0;
        foreach (var tenant in pending.Where(t => t.TimeOutProvisioning(now, options.Value.Timeout)))
        {
            await publisher.Publish(EventFactory.Updated(tenant, EventActor.System), ct);
            timedOut++;
        }
        if (timedOut > 0) await db.SaveChangesAsync(ct);
        return timedOut;
    }
}

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTenantApplication(this IServiceCollection services)
    {
        services.AddScoped<TenantAdminService>();
        services.AddScoped<PlatformAudit>();
        services.AddScoped<TenantQueries>();
        services.AddScoped<ProvisioningService>();
        services.AddScoped<ParameterQueries>();
        services.AddScoped<TenantDefaults>();

        services.AddCrudResource<SystemParameterResource>();
        services.AddCrudResource<OrgResource>();
        services.AddCrudResource<CurrencyResource>();
        services.AddCrudResource<NationalityResource>();
        services.AddCrudResource<EthnicityResource>();
        services.AddCrudResource<AcademicTitleResource>();
        services.AddCrudResource<DegreeResource>();
        services.AddCrudResource<PositionResource>();
        return services;
    }
}
