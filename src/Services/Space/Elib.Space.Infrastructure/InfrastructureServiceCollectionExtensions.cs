using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Space.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Space.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "space";

    public static IServiceCollection AddSpaceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SpaceDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:SpaceDb (đặt qua biến môi trường ConnectionStrings__SpaceDb).");

        services.Configure<SpaceOptions>(configuration.GetSection(SpaceOptions.SectionName));
        services.AddElibPostgres<SpaceDbContext>(connectionString);
        services.AddCrudDbContext<SpaceDbContext>(ServiceName);
        services.AddTenantReplica<SpaceDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<SpaceDbContext>(configuration);
        services.AddScoped<ITenantSeeder, SpaceTenantSeeder>();

        // Bản sao bạn đọc chưa có → hỏi patron qua /internal (service token).
        services.AddElibServiceTokens();
        AddSource(services, configuration, HttpReplicaSources.Patron, "Patron:Url");
        services.AddScoped<IReplicaSources, HttpReplicaSources>();

        services.AddElibMessaging<SpaceDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<SpaceDbContext>();
            x.AddConsumer<ReaderChangedConsumer>();
            x.AddConsumer<StoreChangedConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddSingleton<SpaceBookingExpiryJob>();
        services.AddHostedService(sp => sp.GetRequiredService<SpaceBookingExpiryJob>());
        services.AddHealthChecks().AddDbContextCheck<SpaceDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }

    private static void AddSource(IServiceCollection services, IConfiguration configuration, string name, string key)
    {
        var url = configuration[key];
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException($"Thiếu {key} (địa chỉ nội bộ của service).");
        services.AddHttpClient(name, c =>
            {
                c.BaseAddress = new Uri(url.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
    }
}
