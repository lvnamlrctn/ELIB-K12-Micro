using Elib.Audit.Application;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Audit.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "audit";

    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AuditDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:AuditDb (đặt qua biến môi trường ConnectionStrings__AuditDb).");

        services.AddElibPostgres<AuditDbContext>(connectionString);
        services.AddScoped<IAuditDb>(sp => sp.GetRequiredService<AuditDbContext>());
        services.AddTenantReplica<AuditDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<AuditDbContext>(configuration);
        services.AddElibMessaging<AuditDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<AuditDbContext>();
            x.AddConsumer<AuditRecordedConsumer>();
            x.AddConsumer<SystemAuditRecordedConsumer>();
            x.AddPermissionCacheInvalidation();
        });

        services.Configure<AuditOptions>(configuration.GetSection(AuditOptions.SectionName));
        services.AddSingleton<AuditRetentionService>();
        services.AddHostedService(sp => sp.GetRequiredService<AuditRetentionService>());
        services.AddHealthChecks().AddDbContextCheck<AuditDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
