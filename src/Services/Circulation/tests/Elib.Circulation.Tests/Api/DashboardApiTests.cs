using System.Net;
using System.Net.Http.Json;
using Elib.Circulation.Application;

namespace Elib.Circulation.Tests.Api;

public sealed class DashboardApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private static async Task<CirculationDashboard> Dashboard(HttpClient staff) => await Read<CirculationDashboard>(await staff.GetAsync(U("/api/dashboard")));

    [Fact]
    public async Task Dashboard_counts_activity_ranking_overdue_store_load_and_unused_titles_of_the_tenant_only()
    {
        var a = NewTenant();
        var other = NewTenant();
        var staff = Staff(a);
        var place = await AddPlace(staff, "D1");
        var (popular, quiet) = (NextMfn(), NextMfn());
        await Publish(Reader(a, "DB-01"), Reader(a, "DB-02"), Bib(a, popular, "Tuổi thơ dữ dội"), Bib(a, quiet, "Sách ít đọc"),
            Item(a, "DB001", popular), Item(a, "DB002", popular), Item(a, "DB003", quiet),
            Reader(other, "DB-01"), Item(other, "DB001", popular));
        var otherStaff = Staff(other);
        var otherPlace = await AddPlace(otherStaff, "D1");

        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "DB-01", place.Id, "DB001"))).Succeeded);
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "DB-02", place.Id, "DB002"))).Succeeded);
        await Read<ReturnResult>(await staff.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest("DB002"), Json));
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(otherStaff, "DB-01", otherPlace.Id, "DB001"))).Succeeded);

        var d = await Dashboard(staff);
        Assert.Equal(30, d.Daily.Count);
        Assert.Equal((2, 1), (d.DailyBorrowed, d.DailyReturned));
        Assert.Equal((2, 1), (d.Daily[^1].Borrowed, d.Daily[^1].Returned));
        Assert.Equal(24, d.Hourly.Count);
        Assert.Equal((2, 1), (d.HourlyBorrowed, d.HourlyReturned));
        Assert.Equal(12, d.Trend.Points.Count);
        Assert.Equal(2, d.Trend.Total);
        Assert.Equal(2, d.Trend.Points[^1].Value);
        var top = Assert.Single(d.TopBorrowed);
        Assert.Equal(("Tuổi thơ dữ dội", 2), (top.Title, top.Count));
        Assert.Equal((2, 2), (d.ActiveReaders, d.Loans12Months));
        Assert.Equal(("Học sinh", 2), (Assert.Single(d.BorrowerTypes).Name, d.BorrowerTypes[0].Count));
        Assert.Equal(2, Assert.Single(d.StoreUsage).Count);
        Assert.Equal((0, 0), (d.OverdueReaders, d.OverdueLoans));
        var unused = Assert.Single(d.Unused);
        Assert.Equal(("Sách ít đọc", 1), (unused.Title, unused.Copies));
        Assert.Equal(1, d.UnusedTotal);

        // Quá hạn 9 ngày (hạn mượn 14 ngày) → vào nhóm 8–30; người đã trả không tính.
        Factory.Clock.Advance(TimeSpan.FromDays(23));
        var late = await Dashboard(staff);
        Assert.Equal((1, 1), (late.OverdueReaders, late.OverdueLoans));
        Assert.Equal([0, 1, 0], late.OverdueAging.Select(b => b.Count));
    }

    [Fact]
    public async Task Dashboard_requires_the_circulation_report_permission()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "BORROW:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "CIRC_REPORT:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
