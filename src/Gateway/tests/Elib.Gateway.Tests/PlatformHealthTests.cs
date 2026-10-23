using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Elib.BuildingBlocks.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elib.Gateway.Tests;

public sealed class PlatformHealthTests
{
    private const string System = "quantri.thuvientn.vn";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record Service(string Name, string Status, long LatencyMs, string? Detail);
    private sealed record Queue(string Name, long Messages);
    private sealed record Health(DateTimeOffset CheckedAt, List<Service> Services, List<Queue>? Queues, string? QueuesDetail);

    /// <summary>Backend giả: /ready trả mã chỉ định; /api/queues/{vhost} trả danh sách hàng đợi và nhớ header Authorization.</summary>
    private sealed class Stub : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public string Address { get; }
        public string? Authorization { get; private set; }
        public string? QueuePath { get; private set; }

        private Stub(WebApplication app, string address) { _app = app; Address = address; }

        public static async Task<Stub> StartAsync(int readyStatus, string queuesJson = "[]")
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            Stub? self = null;
            app.MapGet("/ready", () => Results.StatusCode(readyStatus));
            app.MapGet("/api/queues/{**rest}", (HttpContext ctx) =>
            {
                self!.Authorization = ctx.Request.Headers.Authorization.ToString();
                self.QueuePath = ctx.Request.Path.Value + ctx.Request.QueryString.Value;
                return Results.Content(queuesJson, "application/json");
            });
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/') + "/";
            self = new Stub(app, address);
            return self;
        }

        public ValueTask DisposeAsync() => _app.DisposeAsync();
    }

    private static Uri U(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task System_admin_sees_each_cluster_as_healthy_unhealthy_or_unreachable_without_the_non_dotnet_clusters()
    {
        await using var healthy = await Stub.StartAsync(200);
        await using var broken = await Stub.StartAsync(503);
        await using var factory = new GatewayFactory(healthy.Address, new Dictionary<string, string>
        {
            ["ReverseProxy:Clusters:catalog:Destinations:d1:Address"] = broken.Address,
            ["ReverseProxy:Clusters:audit:Destinations:d1:Address"] = "http://127.0.0.1:1/", // không có ai nghe
        });

        var response = await factory.ClientFor(System, TestClaims.SuperAdmin).GetAsync(U("/api/system/health"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<Health>(Web))!;
        Assert.DoesNotContain(health.Services, s => s.Name is "minio" or "admin-web" or "opac-web");
        var byName = health.Services.ToDictionary(s => s.Name);
        Assert.Equal("Healthy", byName["identity"].Status);
        Assert.Equal(("Unhealthy", "HTTP 503"), (byName["catalog"].Status, byName["catalog"].Detail));
        Assert.Equal("Unreachable", byName["audit"].Status);
        Assert.Equal(health.Services.Select(s => s.Name).Order(StringComparer.Ordinal), health.Services.Select(s => s.Name));
        Assert.Null(health.Queues); // gateway test không có tài khoản Messaging
        Assert.NotNull(health.QueuesDetail);
    }

    [Fact]
    public async Task Error_queue_depths_come_from_rabbitmq_management_with_the_service_account_and_other_queues_are_ignored()
    {
        await using var healthy = await Stub.StartAsync(200);
        await using var rabbit = await Stub.StartAsync(200,
            """[{"name":"search-ebook-changed","messages":4},{"name":"search-ebook-changed_error","messages":27},{"name":"audit-x_error","messages":0}]""");
        await using var factory = new GatewayFactory(healthy.Address, new Dictionary<string, string>
        {
            ["Gateway:RabbitManagementUrl"] = rabbit.Address,
            ["Messaging:Username"] = "elib",
            ["Messaging:Password"] = "s3cret",
            ["Messaging:VirtualHost"] = "/",
        });

        var health = (await factory.ClientFor(System, TestClaims.SuperAdmin).GetFromJsonAsync<Health>(U("/api/system/health"), Web))!;

        Assert.Equal([("search-ebook-changed_error", 27L), ("audit-x_error", 0L)], health.Queues!.Select(q => (q.Name, q.Messages)));
        Assert.Null(health.QueuesDetail);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("elib:s3cret")), rabbit.Authorization);
        Assert.StartsWith("/api/queues/%2F", rabbit.QueuePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dead_rabbitmq_management_does_not_fail_the_health_page()
    {
        await using var healthy = await Stub.StartAsync(200);
        await using var factory = new GatewayFactory(healthy.Address, new Dictionary<string, string>
        {
            ["Gateway:RabbitManagementUrl"] = "http://127.0.0.1:1/",
            ["Messaging:Username"] = "elib",
        });

        var response = await factory.ClientFor(System, TestClaims.SuperAdmin).GetAsync(U("/api/system/health"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<Health>(Web))!;
        Assert.Null(health.Queues);
        Assert.NotNull(health.QueuesDetail);
    }

    [Fact]
    public async Task Library_hosts_library_staff_and_anonymous_callers_are_refused()
    {
        await using var healthy = await Stub.StartAsync(200);
        await using var factory = new GatewayFactory(healthy.Address);

        // Cán bộ thư viện, kể cả trên host hệ thống (token có tenant_id) → 403 SYSTEM_ONLY.
        var staffOnSystem = await factory.ClientFor(System, TestClaims.Staff(11)).GetAsync(U("/api/system/health"));
        Assert.Equal(HttpStatusCode.Forbidden, staffOnSystem.StatusCode);
        Assert.Equal("SYSTEM_ONLY", JsonDocument.Parse(await staffOnSystem.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        // Host của thư viện, dù token hệ thống.
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor("truong-a.thuvientn.vn", TestClaims.SuperAdmin).GetAsync(U("/api/system/health"))).StatusCode);
        // Bạn đọc, và chưa đăng nhập.
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(System, TestClaims.Reader(11)).GetAsync(U("/api/system/health"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.ClientFor(System).GetAsync(U("/api/system/health"))).StatusCode);
    }

    [Fact]
    public void Only_error_queues_are_kept_and_the_fullest_come_first()
    {
        using var json = JsonDocument.Parse("""[{"name":"b_error","messages":1},{"name":"a","messages":9},{"name":"c_error","messages":5},{"name":"d_error"}]""");

        Assert.Equal([("c_error", 5L), ("b_error", 1L), ("d_error", 0L)], PlatformHealthService.ParseErrorQueues(json.RootElement).Select(q => (q.Name, q.Messages)));
        using var notAnArray = JsonDocument.Parse("{}");
        Assert.Empty(PlatformHealthService.ParseErrorQueues(notAnArray.RootElement));
    }
}
