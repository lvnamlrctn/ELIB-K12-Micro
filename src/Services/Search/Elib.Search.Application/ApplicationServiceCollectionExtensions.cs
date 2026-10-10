using Microsoft.Extensions.DependencyInjection;

namespace Elib.Search.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSearchApplication(this IServiceCollection services)
    {
        services.AddScoped<SearchIndex>();
        services.AddScoped<IndexRebuilder>();
        services.AddScoped<OpacSearch>();
        return services;
    }
}
