using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddDigitalApplication(this IServiceCollection services)
    {
        services.AddScoped<Replicas>();
        return services;
    }
}
