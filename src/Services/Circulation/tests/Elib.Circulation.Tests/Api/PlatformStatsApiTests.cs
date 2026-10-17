using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;

namespace Elib.Circulation.Tests.Api;

public sealed class PlatformStatsApiTests(CirculationApiFactory factory) : CirculationTestBase(factory)
{
    [Fact]
    public async Task Platform_admin_sees_loans_and_fines_per_tenant_filtered_by_tenant_and_period()
    {
        var a = NewTenant();
        var b = NewTenant();
        var staffA = Staff(a);
        var staffB = Staff(b);
        var placeA = await AddPlace(staffA, "TK");
        var placeB = await AddPlace(staffB, "TK");
        var mfnA = NextMfn();
        var mfnB = NextMfn();
        await Publish(Reader(a, "TK-01"), Reader(a, "TK-02"), Item(a, "TK001", mfnA), Item(a, "TK002", mfnA), Item(a, "TK003", mfnA),
            Reader(b, "TK-01"), Item(b, "TK001", mfnB));
        Assert.Equal(2, (await Read<CheckoutResult>(await Checkout(staffA, "TK-01", placeA.Id, "TK001", "TK002"))).Succeeded);
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staffA, "TK-02", placeA.Id, "TK003"))).Succeeded);
        await Read<ReturnResult>(await staffA.PostAsJsonAsync(U("/api/loans/Return"), new ReturnRequest("TK001"), Json));
        Assert.Equal(1, (await Read<CheckoutResult>(await Checkout(staffB, "TK-01", placeB.Id, "TK001"))).Succeeded);

        // Phiếu phạt thủ công: một phiếu trong kỳ (đã nộp một phần), một phiếu 40 ngày trước — ngoài kỳ.
        var now = Factory.Clock.GetUtcNow();
        var inPeriod = await Read<FineTicketDto>(await staffA.PostAsJsonAsync(U("/api/fine-tickets/Add"), new FineTicketCreateRequest("TK-01", 30000, FineDate: now), Json));
        await Read<FineTicketDetail>(await staffA.PutAsJsonAsync(U($"/api/fine-tickets/Save/{inPeriod.PublicId}"),
            new SaveFineTicketRequest(FineTicketStatus.Open, 0, 10000, ManualAmount: 30000), Json));
        await Read<FineTicketDto>(await staffA.PostAsJsonAsync(U("/api/fine-tickets/Add"), new FineTicketCreateRequest("TK-02", 5000, FineDate: now.AddDays(-40)), Json));

        var system = Factory.CreateClient().WithClaims(TestClaims.SuperAdmin);
        var today = Loan.LocalDate(now);
        async Task<CirculationPlatformStats> Stats(PlatformStatsRequest request) =>
            await Read<CirculationPlatformStats>(await system.PostAsJsonAsync(U("/api/system/stats/circulation"), request, Json));

        var week = await Stats(new PlatformStatsRequest([a, b], today.AddDays(-7), today));
        Assert.Equal([a, b], week.Loans.Select(l => l.TenantId));
        var loansA = week.Loans.Single(l => l.TenantId == a);
        Assert.Equal((3, 1, 2, 0, 2, 0), (loansA.Loans, loansA.Returns, loansA.ReadersServed, loansA.Lost, loansA.OnLoan, loansA.Overdue));
        var loansB = week.Loans.Single(l => l.TenantId == b);
        Assert.Equal((1, 0, 1, 1), (loansB.Loans, loansB.Returns, loansB.ReadersServed, loansB.OnLoan));
        var finesA = Assert.Single(week.Fines);
        Assert.Equal((a, 1, 30000m, 10000m, 20000m), (finesA.TenantId, finesA.Tickets, finesA.Total, finesA.Paid, finesA.Remaining));

        // Kỳ đã qua: không có lượt mượn/phiếu trong kỳ, số "hiện tại" (đang mượn) vẫn giữ.
        var past = await Stats(new PlatformStatsRequest([a], today.AddDays(-60), today.AddDays(-1)));
        var pastA = Assert.Single(past.Loans);
        Assert.Equal((0, 0, 0, 2), (pastA.Loans, pastA.Returns, pastA.ReadersServed, pastA.OnLoan));
        Assert.Equal((1, 5000m), (Assert.Single(past.Fines).Tickets, past.Fines[0].Total));

        // Không lọc đơn vị: có cả hai đơn vị (DB test dùng chung — chỉ kiểm tra có mặt).
        var all = await Stats(new PlatformStatsRequest(null, today, today));
        Assert.Contains(all.Loans, l => l.TenantId == a);
        Assert.Contains(all.Loans, l => l.TenantId == b);

        // Cán bộ đơn vị không gọi được, kể cả có toàn quyền.
        Assert.Equal(HttpStatusCode.Forbidden, (await staffA.PostAsJsonAsync(U("/api/system/stats/circulation"), new PlatformStatsRequest(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await system.PostAsJsonAsync(U("/api/system/stats/circulation"), new PlatformStatsRequest(null, today, today.AddDays(-1)), Json)).StatusCode);
    }
}
