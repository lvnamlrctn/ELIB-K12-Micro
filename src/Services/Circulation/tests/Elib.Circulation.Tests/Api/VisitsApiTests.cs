using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;

namespace Elib.Circulation.Tests.Api;

public sealed class VisitsApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private static Task<HttpResponseMessage> Status(HttpClient staff, string card, long? place = null) =>
        staff.PostAsJsonAsync(U("/api/visits/Status"), new GateStatusRequest(card, place), Json);

    private static Task<HttpResponseMessage> CheckIn(HttpClient staff, string card, long place) =>
        staff.PostAsJsonAsync(U("/api/visits/CheckIn"), new CheckInGateRequest(card, place), Json);

    private static Task<HttpResponseMessage> CheckOut(HttpClient staff, Guid visitPublicId, long? place = null) =>
        staff.PostAsJsonAsync(U("/api/visits/CheckOut"), new CheckOutGateRequest(visitPublicId, place), Json);

    private static Task<HttpResponseMessage> Search(HttpClient staff, VisitSearch search) =>
        staff.PostAsJsonAsync(U("/api/visits/Search"), search, Json);

    [Fact]
    public async Task Checkin_then_checkout_closes_the_visit()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "CONG");
        var reader = Reader(tenantId, "hs-01");
        await Publish(reader);

        var status = await Read<GateStatus>(await Status(staff, " hs-01 ", place.Id));
        Assert.Equal(("HS-01", false), (status.Reader.CardNo, status.CheckedIn));

        var visit = await Read<VisitDto>(await CheckIn(staff, "hs-01", place.Id));
        Assert.Equal(("HS-01", (DateTimeOffset?)null, reader.ReaderPublicId), (visit.CardNo, visit.CheckedOutAt, visit.ReaderPublicId));

        var afterIn = await Read<GateStatus>(await Status(staff, "HS-01"));
        Assert.Equal((true, visit.PublicId), (afterIn.CheckedIn, afterIn.VisitPublicId));

        var closed = await Read<VisitDto>(await CheckOut(staff, visit.PublicId, place.Id));
        Assert.Equal(visit.PublicId, closed.PublicId);
        Assert.NotNull(closed.CheckedOutAt);

        var afterOut = await Read<GateStatus>(await Status(staff, "HS-01"));
        Assert.Equal((false, (Guid?)null), (afterOut.CheckedIn, afterOut.VisitPublicId));
    }

    [Fact]
    public async Task Scanning_in_twice_returns_the_same_open_visit_without_duplicating_history()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "CONG");
        await Publish(Reader(tenantId, "hs-02"));

        var first = await Read<VisitDto>(await CheckIn(staff, "HS-02", place.Id));
        var second = await Read<VisitDto>(await CheckIn(staff, "hs-02", place.Id));
        Assert.Equal(first.PublicId, second.PublicId);

        var history = await Read<CrudPage<VisitDto>>(await Search(staff, new VisitSearch { CardNo = "HS-02" }));
        Assert.Equal(1, history.TotalCount);
    }

    [Fact]
    public async Task Checking_out_an_already_closed_visit_fails()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "CONG");
        await Publish(Reader(tenantId, "hs-03"));

        var visit = await Read<VisitDto>(await CheckIn(staff, "HS-03", place.Id));
        Assert.True((await CheckOut(staff, visit.PublicId, place.Id)).IsSuccessStatusCode);

        var (status, code, _) = await Error(await CheckOut(staff, visit.PublicId, place.Id));
        Assert.Equal((HttpStatusCode.BadRequest, "VISIT_ALREADY_CHECKED_OUT"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task Checking_in_without_a_circ_place_is_rejected()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        await Publish(Reader(tenantId, "hs-04"));

        var (status, code, _) = await Error(await CheckIn(staff, "HS-04", 0));
        Assert.Equal((HttpStatusCode.BadRequest, "CIRC_PLACE_REQUIRED"), ((HttpStatusCode)status, code));
    }

    [Fact]
    public async Task History_filters_by_open_or_closed_state_and_is_isolated_per_tenant()
    {
        var tenantA = NewTenant();
        var tenantB = NewTenant();
        var staffA = Staff(tenantA);
        var staffB = Staff(tenantB);
        var placeA = await AddPlace(staffA, "CONG");
        await Publish(Reader(tenantA, "hs-05"));
        await Publish(Reader(tenantB, "hs-05")); // cùng số thẻ, đơn vị khác — không được lộ chéo

        var openVisit = await Read<VisitDto>(await CheckIn(staffA, "HS-05", placeA.Id));

        var crossTenant = await Read<CrudPage<VisitDto>>(await Search(staffB, new VisitSearch { CardNo = "HS-05" }));
        Assert.Equal(0, crossTenant.TotalCount);

        var openOnly = await Read<CrudPage<VisitDto>>(await Search(staffA, new VisitSearch { State = "open" }));
        Assert.Equal(1, openOnly.TotalCount);
        var closedBefore = await Read<CrudPage<VisitDto>>(await Search(staffA, new VisitSearch { State = "closed" }));
        Assert.Equal(0, closedBefore.TotalCount);

        await CheckOut(staffA, openVisit.PublicId, placeA.Id);

        var openAfter = await Read<CrudPage<VisitDto>>(await Search(staffA, new VisitSearch { State = "open" }));
        Assert.Equal(0, openAfter.TotalCount);
        var closedAfter = await Read<CrudPage<VisitDto>>(await Search(staffA, new VisitSearch { State = "closed" }));
        Assert.Equal(1, closedAfter.TotalCount);
    }
}
