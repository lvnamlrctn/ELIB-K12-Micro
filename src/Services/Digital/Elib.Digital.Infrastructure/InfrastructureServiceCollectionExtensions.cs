using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Digital.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "digital";

    public static IServiceCollection AddDigitalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DigitalDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:DigitalDb (đặt qua biến môi trường ConnectionStrings__DigitalDb).");

        services.AddElibPostgres<DigitalDbContext>(connectionString);
        services.AddCrudDbContext<DigitalDbContext>(ServiceName);
        services.AddTenantReplica<DigitalDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<DigitalDbContext>(configuration);
        services.AddScoped<ITenantSeeder, DigitalTenantSeeder>();

        // Bản sao chưa có bạn đọc → hỏi patron qua /internal (service token).
        services.AddElibServiceTokens();
        AddSource(services, configuration, HttpReplicaSources.Patron, "Patron:Url");
        services.AddScoped<IReplicaSources, HttpReplicaSources>();

        services.AddElibMessaging<DigitalDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<DigitalDbContext>();
            x.AddConsumer<ReaderChangedConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<DigitalDbContext>("db", tags: [ElibHealthTags.Ready]);
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
