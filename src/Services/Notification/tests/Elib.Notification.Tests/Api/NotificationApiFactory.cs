using System.Collections.Concurrent;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Testing;
using Elib.Notification.Application;
using Elib.Notification.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elib.Notification.Tests.Api;

/// <summary>
/// Chạy nguyên service notification trong process: SQLite thay PostgreSQL, bus in-memory (không outbox), xác thực bằng header,
/// SMTP giả ghi lại thư thay vì gửi. Thư tới địa chỉ bắt đầu bằng "fail" giả lập lỗi máy chủ SMTP.
/// </summary>
public sealed class NotificationApiFactory : WebApplicationFactory<Program>
{
    public const string PlatformHost = "smtp.platform.test";

    private readonly SqliteTestDatabase _database = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public ConcurrentQueue<object> Published { get; } = new();
    public FakeSmtpSender Smtp { get; } = new();
    public FakePermissionSource Permissions { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:NotificationDb", "Host=unused-in-tests");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Messaging:UseOutbox", "false");
        builder.UseSetting("Auth:Authority", "https://identity.test");
        builder.UseSetting("Tenancy:GatewaySigningKey", "test-gateway-key-0123456789abcdef");
        builder.UseSetting("Notification:Smtp:Host", PlatformHost);
        builder.UseSetting("Notification:Smtp:Port", "25");
        builder.UseSetting("Notification:Smtp:Security", "None");
        builder.UseSetting("Notification:Smtp:FromAddress", "no-reply@platform.test");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<NotificationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<NotificationDbContext>>();
            services.AddDbContext<NotificationDbContext>((sp, options) => options
                .UseSqlite(_database.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<ElibSaveChangesInterceptor>()));
            services.AddHeaderTestAuth();
            services.RemoveAll<IPermissionSource>();
            services.AddSingleton<IPermissionSource>(Permissions);
            services.RemoveAll<ISmtpSender>();
            services.AddSingleton<ISmtpSender>(Smtp);
        });
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            using (var scope = Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.EnsureCreatedAsync();
            }
            Services.GetRequiredService<IBus>().ConnectPublishObserver(new Recorder(Published));
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task PublishAsync<T>(T message) where T : class => Services.GetRequiredService<IBus>().Publish(message);

    /// <summary>Chờ consumer xử lý xong (bus in-memory chạy nền).</summary>
    public static async Task<T> WaitForAsync<T>(Func<Task<T?>> probe, string what) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await probe() is { } found) return found;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Hết thời gian chờ: {what}");
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

public sealed class FakeSmtpSender : ISmtpSender
{
    public ConcurrentQueue<(SmtpEndpoint Endpoint, OutgoingEmail Email)> Sent { get; } = new();

    public Task SendAsync(SmtpEndpoint endpoint, OutgoingEmail email, CancellationToken cancellationToken)
    {
        if (email.ToAddress.StartsWith("fail", StringComparison.Ordinal))
            throw new SmtpDeliveryException("Máy chủ SMTP từ chối thư (550): mailbox unavailable");
        Sent.Enqueue((endpoint, email));
        return Task.CompletedTask;
    }

    public IEnumerable<(SmtpEndpoint Endpoint, OutgoingEmail Email)> To(string address) => Sent.Where(s => s.Email.ToAddress == address);
}
