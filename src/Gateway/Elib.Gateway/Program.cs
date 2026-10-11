using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Observability;
using Elib.BuildingBlocks.Tenancy;
using Elib.Gateway;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
builder.AddElibObservability("gateway"); // gốc của trace: gateway → service → consumer

services.Configure<GatewayOptions>(builder.Configuration.GetSection(GatewayOptions.SectionName));
services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));

// Xác thực JWT (JWKS của identity). Ủy quyền chi tiết ([Permission]) do từng service làm — gateway chỉ kiểm "đã đăng nhập".
services.AddElibAuth(builder.Configuration);
services.AddSingleton<IPermissionSource, IdentityNotConnectedPermissionSource>(); // gateway không dùng [Permission]
services.AddScoped<ITenantContext, TenantContext>();
services.AddScoped<ICurrentActor, CurrentActor>();
services.AddSingleton<IModuleLicenseSource, NoModuleLicenseSource>();

services.AddElibServiceTokens();
services.AddHttpClient(HttpTenantDirectory.HttpClientName, (sp, c) =>
    {
        c.BaseAddress = new Uri(sp.GetRequiredService<IOptions<GatewayOptions>>().Value.TenantServiceUrl.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(5); // gồm cả lấy service token từ identity ở lần gọi đầu sau khởi động
    })
    .AddHttpMessageHandler<ServiceTokenHandler>();
services.AddSingleton<ITenantDirectory, HttpTenantDirectory>();
services.Configure<ServiceClientOptions>(builder.Configuration.GetSection(ServiceClientOptions.SectionName));

services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (ctx, _) =>
    {
        // Cửa sổ trượt (OPAC) không báo RetryAfter — mặc định một đoạn của cửa sổ một phút (6 đoạn).
        var retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? after : OpacRateLimit.DefaultRetryAfter;
        ctx.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return new ValueTask(GatewayProblem.WriteAsync(ctx.HttpContext, 429, GatewayErrorCodes.TooManyRequests, "Quá nhiều yêu cầu, vui lòng thử lại sau."));
    };
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        var g = http.RequestServices.GetRequiredService<IOptions<GatewayOptions>>().Value;
        // Theo host + người dùng (đã đăng nhập) hoặc IP (ẩn danh) — một trường không làm nghẽn trường khác.
        var who = http.User.FindFirstValue(ElibClaimTypes.Subject) ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{http.Request.Host.Host}|{who}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = g.RateLimitPermits,
            Window = TimeSpan.FromSeconds(g.RateLimitWindowSeconds),
            QueueLimit = 0,
        });
    });

    // Route OPAC công khai (appsettings: "RateLimiterPolicy"): giới hạn theo IP thật (sau X-Forwarded-For) trên từng host, chồng lên
    // giới hạn chung ở trên. Đếm trong bộ nhớ từng instance gateway.
    o.AddPolicy(OpacRateLimit.Policy, http =>
    {
        var g = http.RequestServices.GetRequiredService<IOptions<GatewayOptions>>().Value;
        return RateLimitPartition.Get(OpacRateLimit.Key(http, OpacRateLimit.Policy), _ => RateLimiter.CreateChained(
            OpacRateLimit.Window(g.OpacPermitsPerMinute, TimeSpan.FromMinutes(1)),
            OpacRateLimit.Window(g.OpacPermitsPerHour, TimeSpan.FromHours(1))));
    });
    o.AddPolicy(OpacRateLimit.HeavyPolicy, http =>
    {
        var g = http.RequestServices.GetRequiredService<IOptions<GatewayOptions>>().Value;
        return RateLimitPartition.Get(OpacRateLimit.Key(http, OpacRateLimit.HeavyPolicy),
            _ => OpacRateLimit.Window(g.OpacHeavyPermitsPerMinute, TimeSpan.FromMinutes(1)));
    });
});

// Sau nginx/ingress: lấy IP client và scheme gốc. Host KHÔNG lấy từ header — proxy phải giữ nguyên Host (proxy_set_header Host).
var trustedProxies = builder.Configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>()?.TrustedProxyNetworks ?? [];
services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var cidr in trustedProxies) o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
});

services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
services.AddHealthChecks();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapHealthChecks("/healthz");
// Host hệ thống: trang gốc → app Admin. Host đơn vị: "/" đi tiếp vào route opac-web (OPAC ở gốc host của thư viện).
var systemHosts = builder.Configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>()?.SystemHosts ?? [];
if (systemHosts.Count > 0)
    app.MapGet("/", () => Results.Redirect("/admin/")).AllowAnonymous().RequireHost([.. systemHosts]);
app.MapReverseProxy(proxy => proxy.UseMiddleware<GatewayTenantMiddleware>());
app.Run();

/// <summary>Để WebApplicationFactory trong test tham chiếu được.</summary>
public partial class Program;
