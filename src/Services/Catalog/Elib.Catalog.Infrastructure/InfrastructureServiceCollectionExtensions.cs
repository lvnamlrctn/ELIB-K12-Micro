using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Catalog.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "catalog";

    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CatalogDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:CatalogDb (đặt qua biến môi trường ConnectionStrings__CatalogDb).");

        services.AddElibPostgres<CatalogDbContext>(connectionString);
        services.AddCrudDbContext<CatalogDbContext>(ServiceName);
        services.AddTenantReplica<CatalogDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<CatalogDbContext>(configuration);
        services.AddScoped<ITenantSeeder, CatalogTenantSeeder>();
        services.AddElibMessaging<CatalogDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<CatalogDbContext>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
