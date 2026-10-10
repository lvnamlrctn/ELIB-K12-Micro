using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Holdings.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddHoldingsApplication(this IServiceCollection services)
    {
        services.AddCrudResource<StoreTypeResource>();
        services.AddCrudResource<StoreResource>();
        services.AddCrudResource<ItemResource>();
        services.AddScoped<BibSnapshots>();
        return services;
    }
}
