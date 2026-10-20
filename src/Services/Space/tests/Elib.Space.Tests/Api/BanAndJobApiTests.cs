using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Space.Application;
using Elib.Space.Domain;
using Elib.Space.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Space.Tests.Api;

public sealed class BanAndJobApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Staff_adds_and_lifts_a_ban()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var reader = Reader(tenantId, "hs-20");
        await Publish(reader);

        var ban = await Read<BanDto>(await staff.PostAsJsonAsync(U("/api/bans/Add"), new AddBanRequest("hs-20", 7, "Vắng mặt nhiều lần"), Json));
        Assert.True(ban.IsActive);

        var dup = await staff.PostAsJsonAsync(U("/api/bans/Add"), new AddBanRequest("hs-20", 7, "Thử cấm lần 2"), Json);
        var (status, code, _) = await Error(dup);
        Assert.Equal((HttpStatusCode.Conflict, "BAN_ALREADY_ACTIVE"), ((HttpStatusCode)status, code));

        var lifted = await Read<BanDto>(await staff.PostAsync(U($"/api/bans/{ban.PublicId}/Lift"), null));
        Assert.False(lifted.IsActive);

        var active = await Read<CrudPage<BanDto>>(await staff.PostAsJsonAsync(U("/api/bans/Search"), new BanSearch { ActiveOnly = true }, Json));
        Assert.Empty(active.Items);
    }

    [Fact]
    public async Task A_banned_reader_cannot_book()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P120");
        var reader = Reader(tenantId, "hs-21");
        await Publish(reader);
        await staff.PostAsJsonAsync(U("/api/bans/Add"), new AddBanRequest("hs-21", 7, "Vắng mặt"), Json);
        var readerClient = ReaderClient(tenantId, "hs-21", reader.ReaderPublicId);
        var day = Tomorrow();

        var response = await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.Forbidden, "READER_BANNED"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Job_auto_rejects_pending_and_notifies()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P121", autoApprove: false);
        var reader = Reader(tenantId, "hs-22", email: "hs22@example.com");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-22", reader.ReaderPublicId);
        var day = Tomorrow();
        var start = VnLocal(day, new TimeOnly(9, 0));

        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, start, VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json));
        Assert.Equal(RoomBookingStatus.Pending, booking.Status);

        Factory.Clock.Advance(start.AddMinutes(1) - Factory.Clock.GetUtcNow());
        var result = await RunJobForTenantAsync(tenantId);
        Assert.Equal((1, 0, 0, 0), (result.Rejected, result.Completed, result.NoShow, result.Banned));
        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == SpaceTemplates.Expired);

        var reread = await Read<BookingDto>(await staff.GetAsync(U($"/api/bookings/GetById/{booking.PublicId}")));
        Assert.Equal(RoomBookingStatus.Rejected, reread.Status);
    }

    [Fact]
    public async Task Job_marks_no_show_after_grace_and_auto_bans_on_repeat()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        await staff.PutAsJsonAsync(U("/api/settings"), new UpdateSettingsRequest(new TimeOnly(7, 0), new TimeOnly(21, 0), 0, 0, 0, 1, 30, 7), Json);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P122", checkInGraceMinutes: 10);
        var reader = Reader(tenantId, "hs-23", email: "hs23@example.com");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-23", reader.ReaderPublicId);
        var day = Tomorrow();
        var start = VnLocal(day, new TimeOnly(9, 0));

        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, start, VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json));
        Assert.Equal(RoomBookingStatus.Approved, booking.Status); // tự duyệt mặc định

        Factory.Clock.Advance(start.AddMinutes(15) - Factory.Clock.GetUtcNow()); // qua khỏi 10 phút cho phép check-in muộn
        var result = await RunJobForTenantAsync(tenantId);
        Assert.Equal((0, 0, 1, 1), (result.Rejected, result.Completed, result.NoShow, result.Banned));
        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == SpaceTemplates.NoShow);
        Assert.Contains(Factory.PublishedOf<NotificationRequested>(), n => n.TemplateCode == SpaceTemplates.Banned);

        var reread = await Read<BookingDto>(await staff.GetAsync(U($"/api/bookings/GetById/{booking.PublicId}")));
        Assert.Equal(RoomBookingStatus.NoShow, reread.Status);

        var bans = await Read<CrudPage<BanDto>>(await staff.PostAsJsonAsync(U("/api/bans/Search"), new BanSearch { ActiveOnly = true }, Json));
        Assert.Single(bans.Items);
    }

    [Fact]
    public async Task Job_auto_completes_checked_in_bookings_past_end_time_without_notifying()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P123", checkInGraceMinutes: 120);
        var reader = Reader(tenantId, "hs-24");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-24", reader.ReaderPublicId);
        var day = Tomorrow();
        var start = VnLocal(day, new TimeOnly(9, 0));
        var end = VnLocal(day, new TimeOnly(10, 0));

        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, start, end, 1, null, null), Json));
        Factory.Clock.Advance(start.AddMinutes(5) - Factory.Clock.GetUtcNow());
        await staff.PostAsJsonAsync(U("/api/bookings/CheckIn"), new CheckInRequest(booking.PublicId, null), Json);

        Factory.Clock.Advance(end.AddMinutes(5) - Factory.Clock.GetUtcNow());
        var result = await RunJobForTenantAsync(tenantId);
        Assert.Equal((0, 1, 0, 0), (result.Rejected, result.Completed, result.NoShow, result.Banned));

        var reread = await Read<BookingDto>(await staff.GetAsync(U($"/api/bookings/GetById/{booking.PublicId}")));
        Assert.Equal((RoomBookingStatus.Completed, (DateTimeOffset?)null), (reread.Status, reread.CheckedOutAt));
    }

    private async Task<SpaceBookingExpiryJob.JobResult> RunJobForTenantAsync(long tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        using var use = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        var job = scope.ServiceProvider.GetRequiredService<SpaceBookingExpiryJob>();
        return await job.RunForTenantAsync(scope.ServiceProvider, CancellationToken.None);
    }
}
