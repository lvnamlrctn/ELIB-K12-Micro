using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Circulation.Api;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;

namespace Elib.Circulation.Tests.Api;

/// <summary>Bạn đọc tự phục vụ qua OPAC (/api/reader/**) — tự suy ra bạn đọc từ claim của token, không thấy/đụng được dữ liệu của người khác.</summary>
public sealed class ReaderCirculationApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    [Fact]
    public async Task Reader_sees_only_their_own_loans_and_can_renew_and_view_history()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "R1");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14, MaxRenewals: 2), Json));
        var mfn = NextMfn();
        var readerA = Reader(tenantId, "RD-A");
        var readerB = Reader(tenantId, "RD-B");
        await Publish(readerA, readerB, Bib(tenantId, mfn, "Dế Mèn phiêu lưu ký"), Item(tenantId, "DM001", mfn));
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "RD-A", place.Id, "DM001"))).Succeeded);

        var clientA = ReaderClient(tenantId, "RD-A", readerA.ReaderPublicId);
        var panel = await Read<ReaderPanel>(await clientA.GetAsync(U("/api/reader/loans")));
        var loan = Assert.Single(panel.CurrentLoans);
        Assert.Equal("DM001", loan.Barcode);

        // Bạn đọc khác không thấy lượt mượn của RD-A và không gia hạn được lượt mượn đó (404 — không lộ việc nó tồn tại).
        var clientB = ReaderClient(tenantId, "RD-B", readerB.ReaderPublicId);
        Assert.Empty((await Read<ReaderPanel>(await clientB.GetAsync(U("/api/reader/loans")))).CurrentLoans);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PostAsync(U($"/api/reader/loans/{loan.PublicId}/Renew"), null)).StatusCode);

        var renewed = await Read<LoanDto>(await clientA.PostAsync(U($"/api/reader/loans/{loan.PublicId}/Renew"), null));
        Assert.Equal(1, renewed.RenewCount);

        var history = await Read<CrudPage<LoanDto>>(await clientA.PostAsJsonAsync(U("/api/reader/loans/History"), new ReaderLoanHistoryRequest(), Json));
        Assert.Equal("DM001", Assert.Single(history.Items).Barcode);
        Assert.Empty((await Read<CrudPage<LoanDto>>(await clientB.PostAsJsonAsync(U("/api/reader/loans/History"), new ReaderLoanHistoryRequest(), Json))).Items);
    }

    [Fact]
    public async Task Reader_can_place_and_cancel_their_own_hold_but_not_someone_elses()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var place = await AddPlace(staff, "R2");
        await Read<LoanPolicyDto>(await staff.PostAsJsonAsync(U("/api/loan-policies/Add"), new LoanPolicyRequest(null, null, 14, HoldDays: 2, MaxHolds: 5), Json));
        var mfn = NextMfn();
        var readerA = Reader(tenantId, "HD-A");
        var readerB = Reader(tenantId, "HD-B");
        var busyReader = Reader(tenantId, "HD-C");
        await Publish(readerA, readerB, busyReader, Bib(tenantId, mfn, "Tắt đèn"), Item(tenantId, "TD001", mfn));
        // Giữ bản bằng lượt mượn của một bạn đọc khác để chỗ đặt phải xếp hàng chờ, không giữ ngay.
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staff, "HD-C", place.Id, "TD001"))).Succeeded);

        var clientA = ReaderClient(tenantId, "HD-A", readerA.ReaderPublicId);
        var placed = await Read<HoldDto>(await clientA.PostAsJsonAsync(U("/api/reader/holds"), new ReaderPlaceHoldRequest(Mfn: mfn), Json));
        Assert.Equal(HoldStatus.Waiting, placed.Status);

        var myHolds = await Read<List<HoldDto>>(await clientA.GetAsync(U("/api/reader/holds")));
        Assert.Equal(placed.PublicId, Assert.Single(myHolds).PublicId);

        var clientB = ReaderClient(tenantId, "HD-B", readerB.ReaderPublicId);
        Assert.Empty(await Read<List<HoldDto>>(await clientB.GetAsync(U("/api/reader/holds"))));
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PostAsync(U($"/api/reader/holds/{placed.PublicId}/Cancel"), null)).StatusCode);

        var cancelled = await Read<HoldDto>(await clientA.PostAsync(U($"/api/reader/holds/{placed.PublicId}/Cancel"), null));
        Assert.Equal(HoldStatus.Cancelled, cancelled.Status);
    }
}
