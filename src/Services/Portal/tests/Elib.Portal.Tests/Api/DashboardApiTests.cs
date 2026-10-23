using System.Net;
using System.Net.Http.Json;
using Elib.Portal.Application;

namespace Elib.Portal.Tests.Api;

public sealed class DashboardApiTests(PortalApiFactory factory) : PortalTestBase(factory)
{
    [Fact]
    public async Task Dashboard_sums_hits_per_period_and_month_for_the_tenant_only()
    {
        var a = NewTenant();
        var other = NewTenant();
        var opac = Opac(a);
        for (var i = 0; i < 3; i++) await opac.PostAsync(U("/api/opac/counter/hit"), null);
        await Opac(other).PostAsync(U("/api/opac/counter/hit"), null);

        var d = await Read<PortalDashboard>(await Staff(a, "PORTAL_COUNTER:view").GetAsync(U("/api/dashboard")));
        Assert.Equal((3L, 3L, 3L, 3L), (d.Visits.Total, d.Visits.Today, d.Visits.Last7Days, d.Visits.Last30Days));
        Assert.Equal(12, d.Trend.Points.Count);
        Assert.Equal((3, 3), (d.Trend.Total, d.Trend.Points[^1].Value));

        // Sang ngày mới: hôm nay về 0, tổng và 7 ngày giữ nguyên.
        Factory.Clock.Advance(TimeSpan.FromDays(2));
        await opac.PostAsync(U("/api/opac/counter/hit"), null);
        var later = await Read<PortalDashboard>(await Staff(a, "PORTAL_COUNTER:view").GetAsync(U("/api/dashboard")));
        Assert.Equal((4L, 1L, 4L), (later.Visits.Total, later.Visits.Today, later.Visits.Last7Days));
    }

    [Fact]
    public async Task Dashboard_requires_the_counter_permission()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "PORTAL_NEWS:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "PORTAL_COUNTER:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
