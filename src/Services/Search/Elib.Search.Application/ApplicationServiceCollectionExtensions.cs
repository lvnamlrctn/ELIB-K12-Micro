using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Search.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSearchApplication(this IServiceCollection services)
    {
        services.AddScoped<SearchIndex>();
        services.AddScoped<IndexRebuilder>();
        services.AddScoped<OpacSearch>();
        services.AddScoped<SearchStats>();
        services.AddScoped<Z3950Search>();
        services.AddCrudResource<Z3950ServerResource>();
        return services;
    }
}
