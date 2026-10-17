using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddDigitalApplication(this IServiceCollection services)
    {
        services.AddScoped<Replicas>();
        services.AddCrudResource<EbookCollectionResource>();
        services.AddCrudResource<EbookSubjectResource>();
        services.AddCrudResource<EbookTopicResource>();
        services.AddCrudResource<EbookDigTypeResource>();
        services.AddCrudResource<PolicyDigitalResource>();
        services.AddCrudResource<PolicyDigitalByCollectionResource>();
        services.AddCrudResource<EbookItemResource>();
        services.AddCrudResource<EbookFileResource>();
        services.AddScoped<OpacItems>();
        services.AddScoped<OpacCatalogs>();
        return services;
    }
}
