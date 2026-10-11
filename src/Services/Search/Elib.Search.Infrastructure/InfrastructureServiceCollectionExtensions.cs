using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Search.Application;
using Elib.Search.Infrastructure.Z3950;
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
        services.AddCrudDbContext<SearchDbContext>(ServiceName);
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

        // Tra cứu liên thư viện: Z39.50 (TCP) / SRU (HTTP) ra máy chủ ngoài — chỉ địa chỉ công khai (Z3950:AllowPrivateNetworks).
        services.Configure<Z3950Options>(configuration.GetSection(Z3950Options.SectionName));
        var allowPrivate = configuration.GetValue<bool>($"{Z3950Options.SectionName}:{nameof(Z3950Options.AllowPrivateNetworks)}");
        services.AddHttpClient(Z3950Client.SruHttpClient, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("ELIB-K12-Z3950/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectCallback = (context, ct) => Z3950Client.ConnectGuardedAsync(context, allowPrivate, ct),
                MaxAutomaticRedirections = 3,
            });
        services.AddScoped<IZ3950Client, Z3950Client>();

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
