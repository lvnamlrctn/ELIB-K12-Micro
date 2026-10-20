using System.Net.Http.Json;
using Elib.Space.Application;

namespace Elib.Space.Tests.Api;

public sealed class ReportApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Report_summarizes_bookings_by_status_and_space()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P130", capacity: 4);
        var readerA = Reader(tenantId, "hs-30");
        var readerB = Reader(tenantId, "hs-31");
        await Publish(readerA, readerB);
        var clientA = ReaderClient(tenantId, "hs-30", readerA.ReaderPublicId);
        var clientB = ReaderClient(tenantId, "hs-31", readerB.ReaderPublicId);
        var day = Tomorrow();

        await clientA.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json);
        var cancelMe = await Read<BookingDto>(await clientB.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(11, 0)), VnLocal(day, new TimeOnly(12, 0)), 1, null, null), Json));
        await clientB.DeleteAsync(U($"/api/reader/bookings/{cancelMe.PublicId}"));

        var report = await Read<RoomBookingReport>(await staff.PostAsJsonAsync(U("/api/reports/Search"), new ReportRequest(day, day), Json));
        Assert.Equal((2, 1, 1), (report.TotalBookings, report.Approved, report.Cancelled));
        var spaceRow = Assert.Single(report.BySpace);
        Assert.Equal((space.Id, 2, 1), (spaceRow.SpaceId, spaceRow.Bookings, spaceRow.Cancelled));
        Assert.Equal(2, report.TopReaders.Count);
    }
}
