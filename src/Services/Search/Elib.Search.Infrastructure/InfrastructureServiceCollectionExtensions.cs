using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Search.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Search.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "search";

    public static IServiceCollection AddSearchInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SearchDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:SearchDb (đặt qua biến môi trường ConnectionStrings__SearchDb).");

        services.AddElibPostgres<SearchDbContext>(connectionString);
        services.AddScoped<ISearchDb>(sp => sp.GetRequiredService<SearchDbContext>());
        services.AddTenantReplica<SearchDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<SearchDbContext>(configuration);
        services.AddScoped<ITenantSeeder, SearchTenantSeeder>();
        services.AddSingleton<IndexRebuildRunner>();

        // Dựng chỉ mục: đọc trạng thái theo trang từ /internal của service gốc (service token).
        services.AddElibServiceTokens();
        AddSource(services, configuration, HttpSearchSources.Catalog, "Catalog:Url");
        AddSource(services, configuration, HttpSearchSources.Holdings, "Holdings:Url");
        AddSource(services, configuration, HttpSearchSources.Circulation, "Circulation:Url");
        services.AddScoped<ISearchSources, HttpSearchSources>();

        services.AddElibMessaging<SearchDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<SearchDbContext>();
            x.AddConsumer<BibChangedConsumer>();
            x.AddConsumer<ItemChangedConsumer>();
            x.AddConsumer<LoanChangedConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<SearchDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }

    private static void AddSource(IServiceCollection services, IConfiguration configuration, string name, string key)
    {
        var url = configuration[key];
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException($"Thiếu {key} (địa chỉ nội bộ của service).");
        services.AddHttpClient(name, c =>
            {
                c.BaseAddress = new Uri(url.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
    }
}
