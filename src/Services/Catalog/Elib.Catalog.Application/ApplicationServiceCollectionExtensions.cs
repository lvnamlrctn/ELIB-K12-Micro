using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Catalog.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddCrudResource<BibTypeResource>();
        services.AddCrudResource<WorksheetResource>();
        services.AddCrudResource<BibResource>();
        return services;
    }
}
