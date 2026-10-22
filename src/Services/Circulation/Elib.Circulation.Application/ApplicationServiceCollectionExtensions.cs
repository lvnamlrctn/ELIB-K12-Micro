using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application.Sip2;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Circulation.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCirculationApplication(this IServiceCollection services)
    {
        services.AddCrudResource<CircPlaceResource>();
        services.AddCrudResource<LoanPolicyResource>();
        services.AddCrudResource<LoanResource>();
        services.AddCrudResource<FineReasonResource>();
        services.AddCrudResource<FineTicketResource>();
        services.AddApprovals([.. FineApprovals.Kinds, .. LoanApprovals.Kinds]);
        services.AddScoped<IChangeRequestHandler, FineChangeHandler>();
        services.AddScoped<IChangeRequestHandler, LoanChangeHandler>();
        services.AddCrudResource<HoldResource>();
        services.AddCrudResource<PhotocopyResource>();
        services.AddCrudResource<VisitResource>();
        services.AddScoped<CirculationReports>();
        services.AddScoped<PlatformCirculationStatsService>();
        services.AddScoped<Replicas>();
        services.AddScoped<LoanPublisher>();
        services.AddScoped<ReaderNotifier>();
        services.AddScoped<HoldAllocator>();
        services.AddScoped<CirculationJobs>();
        services.AddCrudResource<Sip2StationResource>();
        services.AddScoped<Sip2LogQueries>();
        services.AddScoped<Sip2Authenticator>();
        services.AddScoped<Sip2Service>();
        return services;
    }
}
