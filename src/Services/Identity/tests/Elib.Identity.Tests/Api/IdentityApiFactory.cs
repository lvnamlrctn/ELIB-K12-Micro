using System.Collections.Concurrent;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Testing;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Application;
using Elib.Identity.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenIddict.Validation.AspNetCore;

namespace Elib.Identity.Tests.Api;

/// <summary>
/// Chạy nguyên service identity trong process: SQLite thay PostgreSQL, bus in-memory, khoá ký sinh trong bộ nhớ, HTTP.
/// Request có header X-Test-Claims dùng xác thực giả; không có thì xác minh Bearer token thật của OpenIddict.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "http://localhost/";
    public const string AdminRedirect = "https://admin.test/callback";
    public const string TenantRedirectPattern = "https://*.truong.test/admin/callback";
    public const string GatewaySecret = "gateway-secret-for-tests";
    public const string GatewayKey = "gateway-signing-key-for-identity-tests-0123456789";
    private const string Combined = "TestOrToken";

    private readonly SqliteTestDatabase _database = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public ConcurrentQueue<object> Published { get; } = new();

    /// <summary>CAPTCHA/OTP theo đơn vị (thay cho gọi service tenant). Khoá 0 = tài khoản hệ thống.</summary>
    public FakeLoginPolicySource LoginPolicies { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:IdentityDb"] = "Host=unused-in-tests",
            ["Messaging:Transport"] = "InMemory",
            ["Messaging:UseOutbox"] = "false",
            ["Identity:Issuer"] = Issuer,
            ["Identity:RequireHttps"] = "false",
            ["Identity:UseEphemeralKeys"] = "true",
            ["Identity:Clients:elib-admin:RedirectUris:0"] = AdminRedirect,
            ["Identity:Clients:elib-admin:RedirectUriPatterns:0"] = TenantRedirectPattern,
            ["Identity:Clients:svc-gateway:ClientSecret"] = GatewaySecret,
            ["Identity:Clients:svc-tenant:ClientSecret"] = "tenant-secret-for-tests",
            ["Identity:Clients:svc-notification:ClientSecret"] = "notification-secret-for-tests",
            ["Identity:Clients:svc-identity:ClientSecret"] = "identity-secret-for-tests",
            ["Identity:Clients:svc-audit:ClientSecret"] = "audit-secret-for-tests",
            ["Identity:Clients:svc-patron:ClientSecret"] = "patron-secret-for-tests",
            ["Identity:Clients:svc-catalog:ClientSecret"] = "catalog-secret-for-tests",
            ["Identity:Clients:svc-holdings:ClientSecret"] = "holdings-secret-for-tests",
            ["Tenancy:GatewaySigningKey"] = GatewayKey,
            ["Identity:BootstrapAdmin:UserName"] = "sysadmin",
            ["Identity:BootstrapAdmin:Password"] = "Sysadmin123",
            ["Identity:BootstrapAdmin:Email"] = "sysadmin@platform.test",
        };
        foreach (var (key, value) in settings) builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IdentityDbContext>>();
            services.AddDbContext<IdentityDbContext>((sp, options) => options
                .UseSqlite(_database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .UseOpenIddict()
                .AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));

            services.RemoveAll<ILoginPolicySource>();
            services.AddSingleton<ILoginPolicySource>(LoginPolicies);

            // Seeder chạy tường minh sau khi tạo schema (InitializeAsync), không chạy nền.
            services.Remove(services.Single(d => d.ImplementationType == typeof(IdentityStartupSeederService)));

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.SchemeName, null)
                .AddPolicyScheme(Combined, null, o => o.ForwardDefaultSelector = ctx =>
                    ctx.Request.Headers.ContainsKey(HeaderAuthHandler.HeaderName)
                        ? HeaderAuthHandler.SchemeName
                        : OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = Combined;
                o.DefaultAuthenticateScheme = Combined;
                o.DefaultChallengeScheme = Combined;
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await using (var scope = Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();
                await scope.ServiceProvider.GetRequiredService<IdentityStartupSeeder>().RunAsync(CancellationToken.None);
            }
            Services.GetRequiredService<IBus>().ConnectPublishObserver(new Recorder(Published));
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>Đưa đơn vị vào bản sao của identity như khi service tenant phát event, chờ tới khi Active.</summary>
    public async Task<long> ProvisionTenantAsync(long tenantId, string code)
    {
        var bus = Services.GetRequiredService<IBus>();
        await bus.Publish(new TenantProvisioned { TenantId = tenantId, Code = code, Name = "Trường " + code, Subdomain = code.ToLowerInvariant(), Modules = [] });
        await bus.Publish(new TenantUpdated
        {
            TenantId = tenantId, Code = code, Name = "Trường " + code, Subdomain = code.ToLowerInvariant(),
            TimeZone = "Asia/Ho_Chi_Minh", Status = TenantStatus.Active, SourceVersion = 1,
        });

        for (var i = 0; i < 100; i++)
        {
            await using var scope = Services.CreateAsyncScope();
            var replica = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<TenantReplicaRecord>()
                .AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId);
            if (replica is { Status: "Active" } && Published.OfType<TenantSeeded>().Any(s => s.TenantId == tenantId)) return tenantId;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Đơn vị {tenantId} không được đồng bộ vào identity.");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _database.Dispose();
            _initLock.Dispose();
        }
    }

    private sealed class Recorder(ConcurrentQueue<object> sink) : IPublishObserver
    {
        public Task PrePublish<T>(PublishContext<T> context) where T : class => Task.CompletedTask;

        public Task PostPublish<T>(PublishContext<T> context) where T : class
        {
            sink.Enqueue(context.Message);
            return Task.CompletedTask;
        }

        public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class => Task.CompletedTask;
    }
}

public sealed class FakeLoginPolicySource : ILoginPolicySource
{
    public ConcurrentDictionary<long, LoginPolicy> Policies { get; } = new();

    public Task<LoginPolicy> GetAsync(long? tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Policies.GetValueOrDefault(tenantId ?? 0, LoginPolicy.None));

    public Task InvalidateAsync(long tenantId, CancellationToken cancellationToken) => Task.CompletedTask;
}
