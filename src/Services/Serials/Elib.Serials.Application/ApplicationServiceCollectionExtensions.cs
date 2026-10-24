using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Serials.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSerialsApplication(this IServiceCollection services)
    {
        services.AddCrudResource<PublicationTypeResource>();
        services.AddCrudResource<FrequencyResource>();
        services.AddCrudResource<PatternResource>();
        services.AddCrudResource<SubscriptionResource>();
        services.AddScoped<BibSnapshots>();
        services.AddScoped<IssueService>();
        services.AddScoped<BindingService>();
        services.AddScoped<SerialsExcel>();
        services.AddScoped<SerialReports>();
        services.AddScoped<SerialsDashboardService>();
        return services;
    }
}
