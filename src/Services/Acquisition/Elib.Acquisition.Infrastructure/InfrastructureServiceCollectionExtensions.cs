using Elib.Acquisition.Application;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Acquisition.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "acquisition";

    public static IServiceCollection AddAcquisitionInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AcquisitionDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:AcquisitionDb (đặt qua biến môi trường ConnectionStrings__AcquisitionDb).");

        services.AddElibPostgres<AcquisitionDbContext>(connectionString);
        services.AddCrudDbContext<AcquisitionDbContext>(ServiceName);
        services.AddTenantReplica<AcquisitionDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<AcquisitionDbContext>(configuration);
        services.AddScoped<ITenantSeeder, AcquisitionTenantSeeder>();
        services.AddScoped<BibReplicaBackfill>();

        // Biểu ghi chưa có trong bản sao → hỏi catalog; ĐKCB theo phiếu nhận → holdings (/internal, service token).
        services.AddElibServiceTokens();
        AddInternalClient(services, configuration, HttpCatalogBibs.HttpClientName, "Catalog:Url", TimeSpan.FromSeconds(30));
        AddInternalClient(services, configuration, HttpHoldingsReceiptItems.HttpClientName, "Holdings:Url", TimeSpan.FromSeconds(30));
        services.AddScoped<ICatalogBibs, HttpCatalogBibs>();
        services.AddScoped<IHoldingsReceiptItems, HttpHoldingsReceiptItems>();

        services.AddElibMessaging<AcquisitionDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<AcquisitionDbContext>();
            x.AddConsumer<BibChangedConsumer>();
            x.AddConsumer<AcquisitionLicenseConsumer>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<AcquisitionDbContext>("db", tags: [ElibHealthTags.Ready]);
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
