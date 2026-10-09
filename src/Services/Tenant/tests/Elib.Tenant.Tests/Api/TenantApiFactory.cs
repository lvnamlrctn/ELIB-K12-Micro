using System.Collections.Concurrent;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Testing;
using Elib.Tenant.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Tenant.Tests.Api;

/// <summary>
/// Chạy nguyên service tenant trong process: SQLite in-memory thay PostgreSQL, bus in-memory thay RabbitMQ (không outbox),
/// xác thực bằng header thay JWT. Ghi lại mọi event được publish để kiểm tra.
/// </summary>
public sealed class TenantApiFactory : WebApplicationFactory<Program>
{
    public const string GatewayKey = "test-gateway-key-0123456789abcdef";

    private readonly SqliteTestDatabase _database = new();

    public ConcurrentQueue<object> Published { get; } = new();

    /// <summary>Quyền của nhân viên theo userId (thay cho gọi identity). Mỗi test dùng userId riêng — quyền được cache theo user.</summary>
    public FakePermissionSource Permissions { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TenantDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Provisioning:DeployedServices:0", "identity");
        builder.UseSetting("Provisioning:SweepInterval", "01:00:00");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", GatewayKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<TenantDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TenantDbContext>>();
            services.AddDbContext<TenantDbContext>((sp, options) => options
                .UseSqlite(_database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));
            services.AddHeaderTestAuth();
            services.RemoveAll<IPermissionSource>();
            services.AddSingleton<IPermissionSource>(Permissions);
        });
    }

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <summary>Tạo schema + gắn observer — chỉ một lần dù được gọi từ mọi test trong fixture.</summary>
    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await InitializeCoreAsync();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitializeCoreAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.EnsureCreatedAsync();
        }
        Services.GetRequiredService<IBus>().ConnectPublishObserver(new Recorder(Published));
    }

    public IEnumerable<T> PublishedOf<T>() => Published.OfType<T>();

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
