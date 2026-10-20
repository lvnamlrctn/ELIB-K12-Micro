using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Space.Application;
using Elib.Space.Domain;

namespace Elib.Space.Tests.Api;

public sealed class BookingApiTests(SpaceApiFactory factory) : SpaceTestBase(factory)
{
    [Fact]
    public async Task Reader_books_an_auto_approve_space_and_sees_it_in_my_bookings()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P101", capacity: 4, autoApprove: true);
        var reader = Reader(tenantId, "hs-01");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-01", reader.ReaderPublicId);

        var day = Tomorrow();
        var start = VnLocal(day, new TimeOnly(9, 0));
        var end = VnLocal(day, new TimeOnly(10, 0));
        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, start, end, 1, "Họp nhóm", null), Json));
        Assert.Equal((RoomBookingStatus.Approved, space.Id, 1), (booking.Status, booking.SpaceId, booking.PartySize));
        Assert.Null(booking.ApprovedBy); // ForReader() ẩn id cán bộ — ở đây cũng null vì tự động duyệt không có cán bộ

        var mine = await Read<List<MyBookingDto>>(await readerClient.GetAsync(U("/api/reader/bookings")));
        var mineOne = Assert.Single(mine);
        Assert.True(mineOne.IsOwner);
        Assert.Equal(booking.PublicId, mineOne.Booking.PublicId);
    }

    /// <summary>
    /// Lưới giờ của trình duyệt thường gửi offset địa phương (+07:00), không phải UTC — endpoint phải tự chuẩn hoá trước khi lưu,
    /// nếu không ElibDbContext.RequireUtc sẽ ném lỗi lúc SaveChanges (đã xảy ra thật với 2 chỗ khác trong service này, xem Reports/Local).
    /// </summary>
    [Fact]
    public async Task Booking_with_a_non_utc_offset_is_normalized_instead_of_failing()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P103", capacity: 4, autoApprove: true);
        var reader = Reader(tenantId, "hs-50");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-50", reader.ReaderPublicId);

        var day = Tomorrow();
        var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(7)); // KHÔNG gọi ToUniversalTime
        var end = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(7));
        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, start, end, 1, null, null), Json));
        Assert.Equal(start.ToUniversalTime(), booking.StartAt);
    }

    [Fact]
    public async Task Manual_approve_space_requires_staff_approval_or_rejection()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P102", autoApprove: false);
        var reader = Reader(tenantId, "hs-02");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-02", reader.ReaderPublicId);
        var day = Tomorrow();

        var pending = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json));
        Assert.Equal(RoomBookingStatus.Pending, pending.Status);

        var approved = await Read<BookingDto>(await staff.PostAsync(U($"/api/bookings/{pending.PublicId}/Approve"), null));
        Assert.Equal(RoomBookingStatus.Approved, approved.Status);

        var another = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(11, 0)), VnLocal(day, new TimeOnly(12, 0)), 1, null, null), Json));
        var rejected = await Read<BookingDto>(await staff.PostAsJsonAsync(U("/api/bookings/Reject"), new RejectBookingRequest(another.PublicId, "Phòng bảo trì"), Json));
        Assert.Equal((RoomBookingStatus.Rejected, "Phòng bảo trì"), (rejected.Status, rejected.RejectReason));
    }

    [Fact]
    public async Task Overlapping_bookings_on_the_same_space_are_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P103", capacity: 8);
        var readerA = Reader(tenantId, "hs-03");
        var readerB = Reader(tenantId, "hs-04");
        await Publish(readerA, readerB);
        var clientA = ReaderClient(tenantId, "hs-03", readerA.ReaderPublicId);
        var clientB = ReaderClient(tenantId, "hs-04", readerB.ReaderPublicId);
        var day = Tomorrow();

        await clientA.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json);
        var response = await clientB.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 30)), VnLocal(day, new TimeOnly(10, 30)), 1, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.Conflict, "SPACE_BOOKING_OVERLAP"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task A_reader_cannot_double_book_themselves_across_different_spaces()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var spaceA = await AddSpaceAsync(staff, floor.Id, "P104A");
        var spaceB = await AddSpaceAsync(staff, floor.Id, "P104B");
        var reader = Reader(tenantId, "hs-05");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-05", reader.ReaderPublicId);
        var day = Tomorrow();

        await readerClient.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(spaceA.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json);
        var response = await readerClient.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(spaceB.Id, VnLocal(day, new TimeOnly(9, 30)), VnLocal(day, new TimeOnly(10, 30)), 1, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.Conflict, "READER_BUSY"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Reader_type_allow_list_blocks_disallowed_readers()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P105", allowedReaderTypeIds: [99]);
        var reader = Reader(tenantId, "hs-06", typeId: 1);
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-06", reader.ReaderPublicId);
        var day = Tomorrow();

        var response = await readerClient.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.Forbidden, "READER_TYPE_NOT_ALLOWED"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Minimum_group_size_and_capacity_are_enforced()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P106", capacity: 3, minGroupSize: 2);
        var reader = Reader(tenantId, "hs-07");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-07", reader.ReaderPublicId);
        var day = Tomorrow();

        var tooFew = await readerClient.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json);
        var (status1, code1, _) = await Error(tooFew);
        Assert.Equal((HttpStatusCode.BadRequest, "MIN_GROUP_NOT_MET"), ((HttpStatusCode)status1, code1));

        var tooMany = await readerClient.PostAsJsonAsync(U("/api/reader/bookings"), new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 10, null, null), Json);
        var (status2, code2, _) = await Error(tooMany);
        Assert.Equal((HttpStatusCode.BadRequest, "PARTY_SIZE_EXCEEDS_CAPACITY"), ((HttpStatusCode)status2, code2));
    }

    [Fact]
    public async Task Booking_with_member_cards_resolves_members_and_party_size()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P107", capacity: 5, minGroupSize: 2);
        var owner = Reader(tenantId, "hs-08");
        var member = Reader(tenantId, "hs-09");
        await Publish(owner, member);
        var readerClient = ReaderClient(tenantId, "hs-08", owner.ReaderPublicId);
        var day = Tomorrow();

        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), null, null, ["hs-09"]), Json));
        Assert.Equal(2, booking.PartySize);
        Assert.Equal("HS-09", Assert.Single(booking.Members).CardNo);

        var memberClient = ReaderClient(tenantId, "hs-09", member.ReaderPublicId);
        var mine = await Read<List<MyBookingDto>>(await memberClient.GetAsync(U("/api/reader/bookings")));
        var mineOne = Assert.Single(mine);
        Assert.False(mineOne.IsOwner);
    }

    [Fact]
    public async Task Staff_checks_in_an_approved_booking_by_public_id_and_can_check_it_out()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P108", checkInGraceMinutes: 120, minAdvanceMinutes: 0);
        var reader = Reader(tenantId, "hs-10");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-10", reader.ReaderPublicId);
        var day = Tomorrow();
        var start = VnLocal(day, new TimeOnly(9, 0));
        var end = VnLocal(day, new TimeOnly(10, 0));

        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, start, end, 1, null, null), Json));
        Assert.Equal(RoomBookingStatus.Approved, booking.Status);

        // Tua đồng hồ test tới 5 phút sau giờ bắt đầu — trong cửa sổ check-in (grace 120 phút) mà không lệ thuộc giờ chạy thật.
        Factory.Clock.Advance(start.AddMinutes(5) - Factory.Clock.GetUtcNow());
        var checkedIn = await Read<BookingDto>(await staff.PostAsJsonAsync(U("/api/bookings/CheckIn"), new CheckInRequest(booking.PublicId, space.Id), Json));
        Assert.Equal(RoomBookingStatus.CheckedIn, checkedIn.Status);

        var checkedOut = await Read<BookingDto>(await staff.PostAsync(U($"/api/bookings/{booking.PublicId}/CheckOut"), null));
        Assert.Equal(RoomBookingStatus.Completed, checkedOut.Status);
    }

    [Fact]
    public async Task Checking_in_too_early_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P109");
        var reader = Reader(tenantId, "hs-11");
        await Publish(reader);
        var readerClient = ReaderClient(tenantId, "hs-11", reader.ReaderPublicId);
        var day = Tomorrow();

        var booking = await Read<BookingDto>(await readerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), 1, null, null), Json));

        var response = await staff.PostAsJsonAsync(U("/api/bookings/CheckIn"), new CheckInRequest(booking.PublicId, null), Json);
        var (status, code, _) = await Error(response);
        Assert.Equal((HttpStatusCode.BadRequest, "OUTSIDE_CHECK_IN_WINDOW"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Owner_can_cancel_before_start_but_member_cannot()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var building = await AddBuildingAsync(staff);
        var floor = await AddFloorAsync(staff, building.Id);
        var space = await AddSpaceAsync(staff, floor.Id, "P110", capacity: 5, minGroupSize: 2);
        var owner = Reader(tenantId, "hs-12");
        var member = Reader(tenantId, "hs-13");
        await Publish(owner, member);
        var ownerClient = ReaderClient(tenantId, "hs-12", owner.ReaderPublicId);
        var memberClient = ReaderClient(tenantId, "hs-13", member.ReaderPublicId);
        var day = Tomorrow();

        var booking = await Read<BookingDto>(await ownerClient.PostAsJsonAsync(U("/api/reader/bookings"),
            new ReaderBookRequest(space.Id, VnLocal(day, new TimeOnly(9, 0)), VnLocal(day, new TimeOnly(10, 0)), null, null, ["hs-13"]), Json));

        var forbidden = await memberClient.DeleteAsync(U($"/api/reader/bookings/{booking.PublicId}"));
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);

        var cancelled = await Read<BookingDto>(await ownerClient.DeleteAsync(U($"/api/reader/bookings/{booking.PublicId}")));
        Assert.Equal(RoomBookingStatus.Cancelled, cancelled.Status);
    }
}
