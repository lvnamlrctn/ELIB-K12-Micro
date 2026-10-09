using System.Security.Cryptography.X509Certificates;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Identity.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Elib.Identity.Infrastructure;

public static class IdentityCookie
{
    /// <summary>Phiên đăng nhập trên trang của identity (không phải token API).</summary>
    public const string Scheme = "Elib.Identity.Login";
}

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

/// <summary>Identity tự tính quyền từ DB của mình (service khác dùng HttpPermissionSource).</summary>
public sealed class LocalPermissionSource(PermissionQueries queries) : IPermissionSource
{
    public Task<IReadOnlySet<string>> GetEffectiveAsync(long? tenantId, long userId, string? permissionStamp, CancellationToken cancellationToken)
        => queries.GetEffectiveAsync(tenantId, userId, cancellationToken);
}

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "identity";

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:IdentityDb (đặt qua biến môi trường ConnectionStrings__IdentityDb).");

        var section = configuration.GetSection(IdentityServerOptions.SectionName);
        services.Configure<IdentityServerOptions>(section);
        var options = section.Get<IdentityServerOptions>() ?? new IdentityServerOptions();

        services.AddElibPostgres<IdentityDbContext>(connectionString, o => o.UseOpenIddict());
        services.AddScoped<IIdentityDb>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddTenantReplica<IdentityDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<IdentityDbContext>(configuration);
        services.AddElibMessaging<IdentityDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<IdentityDbContext>();
            x.AddConsumer<LoginPolicyChangedConsumer>();
            x.AddPermissionCacheInvalidation();
        });

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IPermissionSource, LocalPermissionSource>();
        services.AddScoped<IdentityStartupSeeder>();
        services.AddHostedService<IdentityStartupSeederService>();
        services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("db", tags: [ElibHealthTags.Ready]);
        services.AddAntiforgery(o => o.Cookie.Name = "elib.identity.af");

        // Vé đóng vai đơn vị, captcha, OTP: bộ nhớ của pod. Nhiều replica cần Redis (AddStackExchangeRedisCache) — cùng interface.
        services.AddDistributedMemoryCache();

        // Đọc tham số đăng nhập của đơn vị ở service tenant bằng service token của chính identity (client svc-identity).
        services.Configure<ServiceClientOptions>(configuration.GetSection(ServiceClientOptions.SectionName));
        services.AddElibServiceTokens();
        services.AddHttpClient(TenantLoginPolicySource.HttpClientName, c =>
            {
                c.BaseAddress = new Uri(options.TenantServiceUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddSingleton<ILoginPolicySource, TenantLoginPolicySource>();
        services.Configure<ImpersonationOptions>(configuration.GetSection(ImpersonationOptions.SectionName));
        services.PostConfigure<ImpersonationOptions>(o =>
        {
            // Mặc định suy từ mẫu redirect URI của app Admin: https://*.thuvientn.vn/admin/callback → https://*.thuvientn.vn
            if (!string.IsNullOrWhiteSpace(o.TenantOriginPattern)) return;
            var pattern = options.Clients.GetValueOrDefault("elib-admin")?.RedirectUriPatterns.FirstOrDefault();
            var cut = pattern?.IndexOf("/admin", StringComparison.Ordinal) ?? -1;
            if (cut > 0) o.TenantOriginPattern = pattern![..cut];
        });

        services.AddOpenIddict()
            .AddCore(o =>
            {
                o.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>();
                o.ReplaceApplicationManager(typeof(ElibApplicationManager<>));
            })
            .AddServer(o =>
            {
                o.SetAuthorizationEndpointUris("connect/authorize")
                 .SetTokenEndpointUris("connect/token")
                 .SetEndSessionEndpointUris("connect/logout");

                o.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
                 .AllowRefreshTokenFlow()
                 .AllowClientCredentialsFlow();

                o.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.OfflineAccess, ElibScopes.Api);
                o.SetAccessTokenLifetime(options.AccessTokenLifetime);
                o.SetRefreshTokenLifetime(options.RefreshTokenLifetime);

                // Access token là JWT ký RS256, KHÔNG mã hoá — service khác xác minh qua JWKS (docs 05 §1).
                o.DisableAccessTokenEncryption();
                if (!string.IsNullOrWhiteSpace(options.Issuer)) o.SetIssuer(new Uri(options.Issuer));

                if (options.UseEphemeralKeys)
                {
                    o.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
                }
                else
                {
                    o.AddSigningCertificate(LoadCertificate(options.SigningCertificatePath, options.SigningCertificatePassword, "Signing"));
                    o.AddEncryptionCertificate(LoadCertificate(options.EncryptionCertificatePath, options.EncryptionCertificatePassword, "Encryption"));
                }

                var aspNet = o.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
                if (!options.RequireHttps) aspNet.DisableTransportSecurityRequirement();
            })
            .AddValidation(o =>
            {
                o.UseLocalServer();
                o.UseAspNetCore();
            });

        services.AddAuthentication()
            .AddCookie(IdentityCookie.Scheme, o =>
            {
                o.LoginPath = "/account/login";
                o.LogoutPath = "/account/logout";
                o.Cookie.Name = "elib.identity";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = options.RequireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromHours(1);
                o.SlidingExpiration = true;
            });

        // API của chính identity xác minh token bằng OpenIddict (local) thay cho JwtBearer của AddElibAuth.
        services.Configure<AuthenticationOptions>(o =>
        {
            o.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            o.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        return services;
    }

    private static X509Certificate2 LoadCertificate(string? path, string? password, string purpose)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"Thiếu Identity:{purpose}CertificatePath (hoặc bật Identity:UseEphemeralKeys cho dev/test).");
        return X509CertificateLoader.LoadPkcs12FromFile(path, password);
    }
}
