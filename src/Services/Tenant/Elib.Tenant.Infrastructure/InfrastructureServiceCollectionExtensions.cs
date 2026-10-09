using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.Tenant.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Tenant.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "tenant";

    public static IServiceCollection AddTenantInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TenantDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:TenantDb (đặt qua biến môi trường ConnectionStrings__TenantDb).");

        services.AddElibPostgres<TenantDbContext>(connectionString);
        services.AddScoped<ITenantDb>(sp => sp.GetRequiredService<TenantDbContext>());
        services.AddCrudDbContext<TenantDbContext>(ServiceName);
        services.AddElibMessaging<TenantDbContext>(ServiceName, configuration, x =>
        {
            x.AddConsumer<TenantSeededConsumer>();
            x.AddPermissionCacheInvalidation();
        });

        services.Configure<ProvisioningOptions>(configuration.GetSection(ProvisioningOptions.SectionName));
        services.Configure<BrandingOptions>(configuration.GetSection(BrandingOptions.SectionName));
        services.AddScoped<IModuleLicenseSource, TenantLicenseSource>();
        services.AddHostedService<ProvisioningSweeper>();
        services.AddHealthChecks().AddDbContextCheck<TenantDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
