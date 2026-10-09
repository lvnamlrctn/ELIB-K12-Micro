using Elib.BuildingBlocks.TenantReplica;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Identity.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IdentityAudit>();
        services.AddScoped<SignInService>();
        services.AddScoped<PermissionQueries>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<RoleAdminService>();
        services.AddScoped<IdentityTenantSeeder>();
        services.AddScoped<ITenantSeeder>(sp => sp.GetRequiredService<IdentityTenantSeeder>());
        services.AddScoped<TenantBootstrapService>();
        services.AddScoped<ImpersonationService>();
        services.AddSingleton<CaptchaService>();
        services.AddScoped<LoginOtpService>();
        return services;
    }
}
