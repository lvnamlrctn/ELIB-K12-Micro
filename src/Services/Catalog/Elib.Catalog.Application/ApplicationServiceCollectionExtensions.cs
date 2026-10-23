using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Catalog.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddCrudResource<BibTypeResource>();
        services.AddCrudResource<WorksheetResource>();
        services.AddScoped<DictionaryLearner>();
        services.AddScoped<CatalogDashboardService>();
        services.AddCrudResource<BibResource>();
        services.AddCrudResource<AuthorDictionaryResource>();
        services.AddCrudResource<PublisherDictionaryResource>();
        services.AddCrudResource<KeywordDictionaryResource>();
        services.AddCrudResource<ClassDictionaryResource>();
        services.AddCrudResource<IsbdConfigResource>();
        return services;
    }
}
