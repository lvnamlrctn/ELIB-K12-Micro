using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Holdings.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Holdings.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "holdings";

    public static IServiceCollection AddHoldingsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HoldingsDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:HoldingsDb (đặt qua biến môi trường ConnectionStrings__HoldingsDb).");

        services.AddElibPostgres<HoldingsDbContext>(connectionString);
        services.AddCrudDbContext<HoldingsDbContext>(ServiceName);
        services.AddTenantReplica<HoldingsDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<HoldingsDbContext>(configuration);
        services.AddScoped<ITenantSeeder, HoldingsTenantSeeder>();

        // Biểu ghi chưa có trong bản sao → hỏi catalog qua /internal (service token).
        var catalogUrl = configuration["Catalog:Url"];
        if (string.IsNullOrWhiteSpace(catalogUrl))
            throw new InvalidOperationException("Thiếu Catalog:Url (địa chỉ nội bộ service catalog).");
        services.AddElibServiceTokens();
        services.AddHttpClient(HttpCatalogBibs.HttpClientName, c =>
            {
                c.BaseAddress = new Uri(catalogUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<ICatalogBibs, HttpCatalogBibs>();

        services.AddElibMessaging<HoldingsDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<HoldingsDbContext>();
            x.AddConsumer<BibChangedConsumer>();
            x.AddConsumer<LoanChangedConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<HoldingsDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
