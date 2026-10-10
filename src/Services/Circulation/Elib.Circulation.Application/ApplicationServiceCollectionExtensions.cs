using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Circulation.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCirculationApplication(this IServiceCollection services)
    {
        services.AddCrudResource<CircPlaceResource>();
        services.AddCrudResource<LoanPolicyResource>();
        services.AddCrudResource<LoanResource>();
        services.AddScoped<Replicas>();
        return services;
    }
}
