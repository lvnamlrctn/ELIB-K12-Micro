using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Application;
using Elib.Circulation.Application.Sip2;
using Elib.Circulation.Domain;

namespace Elib.Circulation.Tests.Api;

/// <summary>Điểm lưu thông như ELIB-LRC: kho + loại bạn đọc được phục vụ, chính sách theo loại × điểm, đọc tại chỗ, giới hạn tại điểm.</summary>
public sealed class CircPlacesApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    private static CircPlaceRequest Place(string code, long[]? readerTypes = null, int kind = CircPlace.KindBorrow, int? maxLoans = null,
        IReadOnlyList<WorkSession>? sessions = null) =>
        new(code, "Điểm " + code, [], readerTypes, kind, maxLoans, sessions, null);

    private static async Task<List<LoanPolicyDto>> Policies(HttpClient staff) =>
        await Read<List<LoanPolicyDto>>(await staff.PostAsJsonAsync(U("/api/loan-policies/SearchAll"), new CrudSearch(), Json));

    [Fact]
    public async Task Assigning_reader_types_creates_one_policy_per_type_copied_from_the_rule_in_force()
    {
        var staff = Staff(NewTenant());
        var general = await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"),
            new LoanPolicyRequest(null, null, LoanDays: 10, MaxLoans: 3, RenewDays: 5, FinePerDay: 1000), Json));
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(2, null, LoanDays: 30), Json));

        var place = await Read<CircPlaceDto>(await staff.PostAsJsonAsync(U("/api/circ-places/Add"),
            Place("TV", [1, 2], sessions: [new WorkSession { Day = 1, From = "7:00", To = "11:30" }]), Json));
        Assert.Equal([1L, 2L], place.ReaderTypeIds);
        Assert.Equal("07:00", Assert.Single(place.WorkSessions!).From);

        var own = (await Policies(staff)).Where(p => p.CircPlaceId == place.Id).OrderBy(p => p.ReaderTypeId).ToList();
        Assert.Equal([(1L, 10, 3), (2L, 30, (int?)null)], own.Select(p => (p.ReaderTypeId!.Value, p.LoanDays, p.MaxLoans)).ToList());

        // Sửa chính sách tại điểm, bỏ rồi chọn lại loại 1: không mất cấu hình; thêm loại 3 thì có thêm một chính sách.
        var p1 = own[0];
        await Read<LoanPolicyDto>(await staff.PutAsJsonAsync(U($"/api/loan-policies/Update/{p1.PublicId}"),
            new LoanPolicyRequest(1, place.Id, LoanDays: 7), Json));
        await Read<CircPlaceDto>(await staff.PutAsJsonAsync(U($"/api/circ-places/Update/{place.PublicId}"), Place("TV", [2]), Json));
        await Read<CircPlaceDto>(await staff.PutAsJsonAsync(U($"/api/circ-places/Update/{place.PublicId}"), Place("TV", [1, 2, 3]), Json));
        own = (await Policies(staff)).Where(p => p.CircPlaceId == place.Id).OrderBy(p => p.ReaderTypeId).ToList();
        Assert.Equal([(1L, 7), (2L, 30), (3L, 10)], own.Select(p => (p.ReaderTypeId!.Value, p.LoanDays)).ToList());

        // Xoá điểm: xoá luôn chính sách riêng, chính sách chung còn nguyên.
        Assert.True((await staff.DeleteAsync(U($"/api/circ-places/Delete/{place.PublicId}"))).IsSuccessStatusCode);
        var left = await Policies(staff);
        Assert.DoesNotContain(left, p => p.CircPlaceId == place.Id);
        Assert.Contains(left, p => p.PublicId == general.PublicId);
    }

    [Fact]
    public async Task Place_with_a_sip2_station_cannot_be_deleted_and_invalid_values_are_rejected()
    {
        var staff = Staff(NewTenant());
        var place = await AddPlace(staff, "SIP");
        await Read<Sip2StationDto>(await staff.PostAsJsonAsync(U("/api/sip2-stations/Add"),
            new Sip2StationRequest("K1", "Kiosk", "kiosk-" + Guid.NewGuid().ToString("N")[..8], "Kiosk#2026", place.Id), Json));
        Assert.Equal((409, "CIRC_PLACE_IN_USE"), Pick(await Error(await staff.DeleteAsync(U($"/api/circ-places/Delete/{place.PublicId}")))));

        Assert.Equal("CIRC_PLACE_KIND_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/circ-places/Add"), Place("X1", kind: 9), Json))).Code);
        Assert.Equal("WORK_SESSION_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/circ-places/Add"),
            Place("X2", sessions: [new WorkSession { Day = 2, From = "17:00", To = "08:00" }]), Json))).Code);
        Assert.Equal("WORK_SESSION_OVERLAP", (await Error(await staff.PostAsJsonAsync(U("/api/circ-places/Add"),
            Place("X3", sessions: [new WorkSession { Day = 2, From = "07:00", To = "11:00" }, new WorkSession { Day = 2, From = "10:00", To = "12:00" }]), Json))).Code);
    }

    [Fact]
    public async Task Desk_serves_only_its_reader_types_and_caps_loans_taken_there()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await Read<CircPlaceDto>(await staff.PostAsJsonAsync(U("/api/circ-places/Add"), Place("GV", [5], maxLoans: 1), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "GV01", typeId: 5), Reader(tenantId, "HS01", typeId: 6), Reader(tenantId, "KL01"), Bib(tenantId, mfn, "Toán 6"),
            Item(tenantId, "GV0001", mfn), Item(tenantId, "GV0002", mfn));

        var panel = await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest("HS01", place.Id), Json));
        Assert.False(panel.CanBorrow);
        Assert.Contains("không được phục vụ tại Điểm GV", panel.BlockReason, StringComparison.Ordinal);
        Assert.Equal("READER_TYPE_NOT_SERVED", (await Error(await Checkout(staff, "HS01", place.Id, "GV0001"))).Code);
        Assert.Equal("READER_TYPE_NOT_SERVED", (await Error(await Checkout(staff, "KL01", place.Id, "GV0001"))).Code);

        var result = await Read<CheckoutResult>(await Checkout(staff, "GV01", place.Id, "GV0001", "GV0002"));
        Assert.Equal((1, 1), (result.Succeeded, result.Failed));
        Assert.Equal("Bạn đọc đã mượn đủ 1 tài liệu tại Điểm GV.", result.Lines[1].Message);
    }

    [Fact]
    public async Task Reading_room_loans_are_due_at_the_end_of_the_day_and_cannot_be_renewed()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await Read<CircPlaceDto>(await staff.PostAsJsonAsync(U("/api/circ-places/Add"), Place("DTC", kind: CircPlace.KindReadingRoom), Json));
        var mfn = NextMfn();
        await Publish(Reader(tenantId, "DOC01"), Bib(tenantId, mfn, "Từ điển"), Item(tenantId, "TD0001", mfn), Item(tenantId, "TD0002", mfn));

        Assert.Equal(0, (await Read<ReaderPanel>(await staff.PostAsJsonAsync(U("/api/loans/Reader"), new ReaderPanelRequest("DOC01", place.Id), Json))).LoanDays);
        var loan = (await Read<CheckoutResult>(await Checkout(staff, "DOC01", place.Id, "TD0001"))).Lines[0].Loan!;
        Assert.Equal(Loan.EndOfLocalDay(loan.LoanedAt), loan.DueAt);
        Assert.Equal(Loan.LocalDate(loan.LoanedAt), Loan.LocalDate(loan.DueAt));
        Assert.Equal("RENEW_READING_ROOM", (await Error(await staff.PostAsJsonAsync(U("/api/loans/Renew"), new RenewRequest(loan.PublicId, "Đọc tiếp"), Json))).Code);

        // Vẫn trong ngày: không bị coi là quá hạn, mượn thêm được.
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "DOC01", place.Id, "TD0002"))).Succeeded);
    }

    [Fact]
    public async Task Places_and_their_policies_stay_inside_the_tenant()
    {
        var staffA = Staff(NewTenant());
        var staffB = Staff(NewTenant());
        var place = await Read<CircPlaceDto>(await staffA.PostAsJsonAsync(U("/api/circ-places/Add"), Place("A1", [1]), Json));

        Assert.Empty(await Read<List<CircPlaceDto>>(await staffB.PostAsJsonAsync(U("/api/circ-places/SearchAll"), new CrudSearch(), Json)));
        Assert.DoesNotContain(await Policies(staffB), p => p.CircPlaceId == place.Id);
        Assert.Equal(404, (int)(await staffB.PutAsJsonAsync(U($"/api/circ-places/Update/{place.PublicId}"), Place("A1"), Json)).StatusCode);
        // Cùng mã ở đơn vị khác vẫn được.
        await Read<CircPlaceDto>(await staffB.PostAsJsonAsync(U("/api/circ-places/Add"), Place("A1"), Json));
    }

    private static (int, string?) Pick((int Status, string? Code, string? Detail) e) => (e.Status, e.Code);
}
