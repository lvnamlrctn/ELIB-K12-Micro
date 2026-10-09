using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.BuildingBlocks.Tenancy;

public static class TenancyServiceCollectionExtensions
{
    public static IServiceCollection AddElibTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TenancyOptions>(configuration.GetSection(TenancyOptions.SectionName));
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<CurrentActor>();
        services.AddScoped<ICurrentActor>(sp => sp.GetRequiredService<CurrentActor>());
        return services;
    }

    /// <summary>Đặt sau UseAuthentication() và trước UseAuthorization().</summary>
    public static IApplicationBuilder UseElibTenancy(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
