using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Patron.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPatronApplication(this IServiceCollection services)
    {
        services.AddCrudResource<ReaderTypeResource>();
        services.AddCrudResource<SchoolClassResource>();
        services.AddCrudResource<CourseResource>();
        services.AddCrudResource<ReaderGroupResource>();
        services.AddCrudResource<ReaderResource>();
        return services;
    }
}
