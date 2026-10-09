using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.BuildingBlocks.Authorization;

public sealed class ElibAuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>URL của service identity (OIDC). Khoá ký lấy tự động qua /.well-known/openid-configuration → JWKS.</summary>
    public string Authority { get; set; } = "";

    /// <summary>Audience của service này, ví dụ "elib-circulation".</summary>
    public string Audience { get; set; } = "";

    public bool RequireHttpsMetadata { get; set; } = true;
}

public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// JWT RS256 qua JWKS của identity (docs 05 §1) + policy [Permission]/[RequiresModule].
    /// Service phải đăng ký <see cref="IPermissionSource"/> và <see cref="IModuleLicenseSource"/>.
    /// Token chỉ nhận qua header Authorization — không đọc từ query string.
    /// </summary>
    public static IServiceCollection AddElibAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var auth = configuration.GetSection(ElibAuthOptions.SectionName).Get<ElibAuthOptions>() ?? new ElibAuthOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = auth.Authority;
                options.Audience = auth.Audience;
                options.RequireHttpsMetadata = auth.RequireHttpsMetadata;
                options.MapInboundClaims = false; // giữ nguyên tên claim: sub, tenant_id, sub_type…
                options.TokenValidationParameters.NameClaimType = ElibClaimTypes.Subject;
                options.TokenValidationParameters.ValidAlgorithms = ["RS256"];
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            });

        services.AddElibAuthorizationCore();
        return services;
    }

    /// <summary>Phần policy/handler, tách riêng để test hoặc dùng với scheme xác thực khác.</summary>
    public static IServiceCollection AddElibAuthorizationCore(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddHybridCache();
        // Replace: AddAuthorization() đã đăng ký provider/result handler mặc định — TryAdd sẽ không có tác dụng.
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, ElibAuthorizationPolicyProvider>());
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, ElibAuthorizationResultHandler>());
        services.TryAddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, ModuleHandler>();
        services.AddScoped<IAuthorizationHandler, SystemContextHandler>();
        services.AddSingleton<IAuthorizationHandler, ServiceCallerHandler>();
        return services;
    }
}
