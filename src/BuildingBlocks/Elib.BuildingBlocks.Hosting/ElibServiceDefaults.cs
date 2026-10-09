using System.Diagnostics;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Elib.BuildingBlocks.Hosting;

public static class ElibHealthTags
{
    /// <summary>Liveness — chỉ kiểm tra process, KHÔNG kiểm tra phụ thuộc (DB, RabbitMQ…).</summary>
    public const string Live = "live";

    /// <summary>Readiness — service thêm check DB/RabbitMQ/ES với tag này.</summary>
    public const string Ready = "ready";
}

public static class ElibServiceDefaults
{
    /// <summary>
    /// Wiring chuẩn mọi service (docs 07 §2): tenancy, JWT + [Permission]/[RequiresModule], problem+json có code,
    /// health check, OpenAPI, forwarded headers sau ingress/gateway.
    /// Service tự đăng ký thêm: DbContext (AddElibPostgres), messaging (AddElibMessaging), IPermissionSource, IModuleLicenseSource.
    /// </summary>
    public static WebApplicationBuilder AddElibServiceDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        var services = builder.Services;

        services.AddElibTenancy(builder.Configuration);
        services.AddElibAuth(builder.Configuration);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Extensions["service"] = serviceName;
            ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
            // Mọi problem đều có "code" ổn định — kể cả lỗi do framework sinh (401, 404 route, 405…).
            ctx.ProblemDetails.Extensions.TryAdd("code", ctx.ProblemDetails.Status switch
            {
                401 => "UNAUTHENTICATED",
                403 => "FORBIDDEN",
                404 => "NOT_FOUND",
                405 => "METHOD_NOT_ALLOWED",
                415 => "UNSUPPORTED_MEDIA_TYPE",
                429 => "TOO_MANY_REQUESTS",
                >= 500 => "INTERNAL_ERROR",
                _ => "BAD_REQUEST",
            });
        });
        services.AddExceptionHandler<ElibExceptionHandler>();

        // Enum trao đổi dạng chuỗi ("Active") — dễ đọc với frontend, không vỡ khi thêm giá trị enum mới.
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: [ElibHealthTags.Live]);
        services.AddOpenApi();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            // Trong cluster, chỉ gateway/ingress gọi tới service (NetworkPolicy) — tin header từ mạng nội bộ.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    /// <summary>Pipeline chuẩn. Gọi TRƯỚC khi map endpoint nghiệp vụ.</summary>
    public static WebApplication UseElibServiceDefaults(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseElibTenancy();
        app.UseAuthorization();

        app.MapElibHealthChecks();
        app.MapOpenApi("/internal/openapi/{documentName}.json");
        return app;
    }

    public static IEndpointRouteBuilder MapElibHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ElibHealthTags.Live) });
        endpoints.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ElibHealthTags.Ready) || c.Tags.Contains(ElibHealthTags.Live) });
        return endpoints;
    }
}
