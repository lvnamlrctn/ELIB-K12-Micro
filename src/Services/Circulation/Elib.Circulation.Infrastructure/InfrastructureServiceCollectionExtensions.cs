using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Circulation.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Circulation.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "circulation";

    public static IServiceCollection AddCirculationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CirculationDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:CirculationDb (đặt qua biến môi trường ConnectionStrings__CirculationDb).");

        services.AddElibPostgres<CirculationDbContext>(connectionString);
        services.AddCrudDbContext<CirculationDbContext>(ServiceName);
        services.AddTenantReplica<CirculationDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<CirculationDbContext>(configuration);
        services.AddScoped<ITenantSeeder, CirculationTenantSeeder>();

        // Bản sao chưa có bạn đọc/bản sách/biểu ghi → hỏi service gốc qua /internal (service token).
        services.AddElibServiceTokens();
        AddSource(services, configuration, HttpReplicaSources.Patron, "Patron:Url");
        AddSource(services, configuration, HttpReplicaSources.Holdings, "Holdings:Url");
        AddSource(services, configuration, HttpReplicaSources.Catalog, "Catalog:Url");
        services.AddScoped<IReplicaSources, HttpReplicaSources>();

        services.AddElibMessaging<CirculationDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<CirculationDbContext>();
            x.AddConsumer<ReaderChangedConsumer>();
            x.AddConsumer<ItemChangedConsumer>();
            x.AddConsumer<BibChangedConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddSingleton<CirculationJobScheduler>();
        services.AddHostedService(sp => sp.GetRequiredService<CirculationJobScheduler>());
        services.AddHealthChecks().AddDbContextCheck<CirculationDbContext>("db", tags: [ElibHealthTags.Ready]);
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
