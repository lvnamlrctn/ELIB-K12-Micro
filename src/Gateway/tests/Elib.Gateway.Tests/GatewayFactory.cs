using Elib.BuildingBlocks.Testing;
using Elib.Gateway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Elib.Gateway.Tests;

/// <summary>Đơn vị giả cho gateway — thay HttpTenantDirectory (không gọi service tenant thật).</summary>
public sealed class FakeTenantDirectory : ITenantDirectory
{
    public Dictionary<string, TenantInfo> Tenants { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Giả lập service tenant không phản hồi.</summary>
    public bool Unavailable { get; set; }

    public Task<TenantInfo?> FindByHostAsync(string host, CancellationToken cancellationToken) =>
        Unavailable
            ? Task.FromException<TenantInfo?>(new TaskCanceledException("HttpClient.Timeout"))
            : Task.FromResult(Tenants.GetValueOrDefault(host.Split('.')[0]));
}

/// <summary>Backend thật (Kestrel, cổng ngẫu nhiên) trả lại đúng những gì nó nhận — để kiểm tra gateway chuyển tiếp gì.</summary>
public sealed class EchoBackend : IAsyncDisposable
{
    private readonly WebApplication _app;

    private EchoBackend(WebApplication app, string address)
    {
        _app = app;
        Address = address;
    }

    public string Address { get; }

    public static async Task<EchoBackend> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Map("{**path}", (HttpContext ctx) => Results.Json(new
        {
            path = ctx.Request.Path.Value,
            headers = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
        }));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new EchoBackend(app, address.TrimEnd('/') + "/");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

public sealed class GatewayFactory(string backendAddress) : WebApplicationFactory<Program>
{
    public const string SigningKey = "gateway-signing-key-for-tests-0123456789";

    public FakeTenantDirectory Directory { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string>
        {
            ["Auth:Authority"] = "https://identity.test",
            ["Tenancy:GatewaySigningKey"] = SigningKey,
            ["Gateway:SystemHosts:0"] = "quantri.thuvientn.vn",
            ["Gateway:RateLimitPermits"] = "20",
            ["ReverseProxy:Clusters:identity:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:tenant:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:notification:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:audit:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:media:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:patron:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:catalog:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:minio:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:search:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:admin-web:Destinations:d1:Address"] = backendAddress,
            ["ReverseProxy:Clusters:opac-web:Destinations:d1:Address"] = backendAddress,
            // Route mẫu của một service nghiệp vụ có license: OPAC tra cứu (ẩn danh) và quản trị lưu thông (cần đăng nhập).
            ["ReverseProxy:Routes:search-opac:ClusterId"] = "search",
            ["ReverseProxy:Routes:search-opac:AuthorizationPolicy"] = "anonymous",
            ["ReverseProxy:Routes:search-opac:Match:Path"] = "/api/opac/search/{**rest}",
            ["ReverseProxy:Routes:search-opac:Transforms:0:PathPattern"] = "/api/{**rest}",
            ["ReverseProxy:Routes:search-opac:Metadata:Module"] = "SEARCH",
            ["ReverseProxy:Routes:circulation-admin:ClusterId"] = "search",
            ["ReverseProxy:Routes:circulation-admin:AuthorizationPolicy"] = "default",
            ["ReverseProxy:Routes:circulation-admin:Match:Path"] = "/api/admin/circulation/{**rest}",
            ["ReverseProxy:Routes:circulation-admin:Transforms:0:PathPattern"] = "/api/{**rest}",
            ["ReverseProxy:Routes:circulation-admin:Metadata:Module"] = "CIRCULATION",
        };
        settings["Gateway:TrustedProxyNetworks:0"] = "10.0.0.0/8"; // "nginx" trong test
        foreach (var (key, value) in settings) builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantDirectory>();
            services.AddSingleton<ITenantDirectory>(Directory);
            services.AddHeaderTestAuth();
            services.AddSingleton<IStartupFilter, RemoteIpStartupFilter>();
        });
    }

    /// <summary>Header giả IP nguồn của kết nối TCP (TestServer không có IP thật). Chạy trước mọi middleware của gateway.</summary>
    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((ctx, nextMiddleware) =>
            {
                if (ctx.Request.Headers.Remove(RemoteIpHeader, out var ip)) ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip.ToString());
                return nextMiddleware(ctx);
            });
            next(app);
        };
    }

    public HttpClient ClientFor(string host, string? claims = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"http://{host}/"), AllowAutoRedirect = false });
        return claims is null ? client : client.WithClaims(claims);
    }
}
