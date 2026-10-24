using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Serials.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Serials.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "serials";

    public static IServiceCollection AddSerialsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SerialsDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:SerialsDb (đặt qua biến môi trường ConnectionStrings__SerialsDb).");

        services.AddElibPostgres<SerialsDbContext>(connectionString);
        services.AddCrudDbContext<SerialsDbContext>(ServiceName);
        services.AddTenantReplica<SerialsDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<SerialsDbContext>(configuration);
        services.AddScoped<ITenantSeeder, SerialsTenantSeeder>();
        services.AddScoped<BibReplicaBackfill>();

        // Biểu ghi chưa có trong bản sao → hỏi catalog; ĐKCB tập đóng → holdings; nhà cung cấp (tuỳ chọn) → acquisition (/internal, service token).
        services.AddElibServiceTokens();
        AddInternalClient(services, configuration, HttpCatalogBibs.HttpClientName, "Catalog:Url", TimeSpan.FromSeconds(30));
        AddInternalClient(services, configuration, HttpHoldingsSerialItems.HttpClientName, "Holdings:Url", TimeSpan.FromSeconds(30));
        AddInternalClient(services, configuration, HttpAcquisitionSuppliers.HttpClientName, "Acquisition:Url", TimeSpan.FromSeconds(10));
        services.AddScoped<ICatalogBibs, HttpCatalogBibs>();
        services.AddScoped<IHoldingsSerialItems, HttpHoldingsSerialItems>();
        services.AddScoped<ISupplierSource, HttpAcquisitionSuppliers>();

        services.AddElibMessaging<SerialsDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<SerialsDbContext>();
            x.AddConsumer<BibChangedConsumer>();
            x.AddConsumer<SerialsLicenseConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<SerialsDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }

    private static void AddInternalClient(IServiceCollection services, IConfiguration configuration, string name, string urlKey, TimeSpan timeout)
    {
        var url = configuration[urlKey];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException($"Thiếu {urlKey} (địa chỉ nội bộ của service).");
        services.AddHttpClient(name, c =>
            {
                c.BaseAddress = new Uri(url.TrimEnd('/') + "/");
                c.Timeout = timeout;
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
    }
}
