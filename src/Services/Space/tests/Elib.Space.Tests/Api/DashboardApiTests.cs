using System.Net;
using System.Net.Http.Json;
using Elib.Space.Application;
using Elib.Space.Domain;

namespace Elib.Space.Tests.Api;

public sealed class DashboardApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Dashboard_counts_occupancy_outcomes_and_pending_requests_of_the_tenant_only()
    {
        var tenantId = NewTenant();
        var other = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var open = await AddSpaceAsync(staff, floor.Id, "DB1", checkInGraceMinutes: 120);
        var manual = await AddSpaceAsync(staff, floor.Id, "DB2", autoApprove: false);
        await AddSpaceAsync(staff, floor.Id, "DB3");
        var reader = Reader(tenantId, "db-01");
        var reader2 = Reader(tenantId, "db-02");
        await Publish(reader, reader2);
        var day = Tomorrow();
        var start = VnLocal(day, new TimeOnly(9, 0));
        var booking = await Read<BookingDto>(await ReaderClient(tenantId, "db-01", reader.ReaderPublicId).PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(open.Id, start, VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json));
        var pending = await Read<BookingDto>(await ReaderClient(tenantId, "db-02", reader2.ReaderPublicId).PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(manual.Id, VnLocal(day, new TimeOnly(14, 0)), VnLocal(day, new TimeOnly(15, 0)), 1, null, null), Json));
        Assert.Equal(RoomBookingStatus.Pending, pending.Status);

        var before = await Read<SpaceDashboard>(await staff.GetAsync(U("/api/dashboard")));
        Assert.Equal((3, 0, 3), (before.OccupancyNow.TotalSpaces, before.OccupancyNow.Occupied, before.OccupancyNow.Free));
        Assert.Equal(1, before.PendingRequests);
        Assert.Equal((0, null), (before.NoShow30.Total, before.NoShow30.RatePercent));

        Factory.Clock.Advance(start.AddMinutes(5) - Factory.Clock.GetUtcNow());
        await Read<BookingDto>(await staff.PostAsJsonAsync(U("/api/bookings/CheckIn"), new CheckInRequest(booking.PublicId, open.Id), Json));
        var during = await Read<SpaceDashboard>(await staff.GetAsync(U("/api/dashboard")));
        Assert.Equal((3, 1, 2), (during.OccupancyNow.TotalSpaces, during.OccupancyNow.Occupied, during.OccupancyNow.Free));
        Assert.Equal((1, 0, 0.0), (during.NoShow30.Total, during.NoShow30.NoShow, during.NoShow30.RatePercent));

        var empty = await Read<SpaceDashboard>(await Staff(other).GetAsync(U("/api/dashboard")));
        Assert.Equal((0, 0, 0), (empty.OccupancyNow.TotalSpaces, empty.OccupancyNow.Occupied, empty.PendingRequests));
    }

    [Fact]
    public async Task Dashboard_requires_the_space_report_permission()
    {
        var tenantId = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "SPACE_BOOKINGS:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "SPACE_REPORT:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
