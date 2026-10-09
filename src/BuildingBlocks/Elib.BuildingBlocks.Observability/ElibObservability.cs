using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Elib.BuildingBlocks.Observability;

/// <summary>
/// OpenTelemetry cho mọi service (docs 06 §quan sát): trace (HTTP vào/ra, PostgreSQL, RabbitMQ qua MassTransit, YARP),
/// metrics (HTTP, runtime, MassTransit) và log, xuất OTLP tới collector (DEV: grafana/otel-lgtm).
/// Chỉ bật khi có <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> — test và máy dev không có collector thì không tốn gì.
/// Trace đi xuyên service nhờ header W3C <c>traceparent</c> (HTTP) và header của MassTransit (message).
/// </summary>
public static class ElibObservability
{
    /// <summary>Tag đơn vị trên span của request — lọc trace theo trường trong Grafana/Tempo.</summary>
    public const string TenantTag = "elib.tenant_id";

    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>ActivitySource/Meter của MassTransit và YARP (thư viện tự phát, chỉ cần đăng ký nghe).</summary>
    private static readonly string[] Sources = ["MassTransit", "Yarp.ReverseProxy"];

    public static bool IsEnabled(IConfiguration configuration) => !string.IsNullOrWhiteSpace(configuration[EndpointKey]);

    public static IHostApplicationBuilder AddElibObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!IsEnabled(builder.Configuration)) return builder;

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService("elib-" + serviceName, serviceNamespace: "elib", serviceVersion: version)
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = ctx => !IsProbe(ctx.Request.Path);
                    o.RecordException = true;
                })
                .AddHttpClientInstrumentation(o => o.RecordException = true)
                .AddNpgsql()
                .AddSource(Sources))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Sources))
            .UseOtlpExporter();
        return builder;
    }

    /// <summary>Gắn mã đơn vị vào span của request (gọi sau khi đã xác định đơn vị).</summary>
    public static void TagTenant(long? tenantId)
    {
        if (tenantId is { } id) Activity.Current?.SetTag(TenantTag, id);
    }

    /// <summary>Không ghi trace cho healthcheck/probe — K8s gọi vài giây một lần, chỉ làm nhiễu.</summary>
    private static bool IsProbe(PathString path) =>
        path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/ready", StringComparison.OrdinalIgnoreCase);
}
