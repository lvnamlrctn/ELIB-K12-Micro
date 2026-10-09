using System.Diagnostics;
using Elib.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Elib.BuildingBlocks.Tests.Hosting;

public sealed class ObservabilityTests
{
    [Fact]
    public async Task Off_without_collector_and_on_when_otlp_endpoint_is_set()
    {
        var off = WebApplication.CreateBuilder();
        off.AddElibObservability("test");
        await using (var app = off.Build())
            Assert.Null(app.Services.GetService<TracerProvider>());

        var on = WebApplication.CreateBuilder();
        on.Configuration[ElibObservability.EndpointKey] = "http://127.0.0.1:4317";
        on.AddElibObservability("test");
        await using (var app = on.Build())
            Assert.NotNull(app.Services.GetService<TracerProvider>());
    }

    [Fact]
    public void Tenant_is_tagged_on_the_current_span()
    {
        using var source = new ActivitySource("elib-test");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "elib-test",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("request");
        ElibObservability.TagTenant(42);
        ElibObservability.TagTenant(null); // chưa xác định đơn vị: không ghi đè

        Assert.Equal(42L, activity!.GetTagItem(ElibObservability.TenantTag));
    }
}
