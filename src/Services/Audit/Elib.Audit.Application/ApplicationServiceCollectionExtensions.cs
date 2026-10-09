using Microsoft.Extensions.DependencyInjection;

namespace Elib.Audit.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAuditApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditQueries>();
        return services;
    }
}
