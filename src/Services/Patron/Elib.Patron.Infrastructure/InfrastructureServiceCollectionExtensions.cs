using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Patron.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "patron";

    public static IServiceCollection AddPatronInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PatronDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:PatronDb (đặt qua biến môi trường ConnectionStrings__PatronDb).");

        services.AddElibPostgres<PatronDbContext>(connectionString);
        services.AddCrudDbContext<PatronDbContext>(ServiceName);
        services.AddTenantReplica<PatronDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<PatronDbContext>(configuration);
        services.AddScoped<ITenantSeeder, PatronTenantSeeder>();
        services.AddElibMessaging<PatronDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<PatronDbContext>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<PatronDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
