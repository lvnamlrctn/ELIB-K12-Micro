using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.Acquisition.Application;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Catalog;

namespace Elib.Acquisition.Tests.Api;

public sealed class DashboardApiTests(AcquisitionApiFactory factory) : IClassFixture<AcquisitionApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static long _nextTenantId = 31000;
    private static long _nextUserId = 41000;
    private static long _nextMfn = 9000;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var id = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((id, "ACQUISITION"));
        return id;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    [Fact]
    public async Task Dashboard_counts_orders_receipts_and_fund_spend_of_the_tenant_only()
    {
        var a = NewTenant();
        var b = NewTenant();
        var staff = Staff(a);
        var bib = new BibChanged { TenantId = a, BibPublicId = Guid.CreateVersion7(), Mfn = Interlocked.Increment(ref _nextMfn), Title = "Truyện Kiều", Status = 2 };
        await factory.PublishAsync(bib);
        await factory.WaitConsumedAsync(bib);
        var fund = await Read<FundDto>(await staff.PostAsJsonAsync(U("/api/funds/Add"), new FundRequest("Quỹ sách", Amount: 1_000_000m), Json));
        await Read<FundDto>(await staff.PostAsJsonAsync(U("/api/funds/Add"), new FundRequest("Quỹ trống"), Json));
        await Read<PurchaseOrderDto>(await staff.PostAsJsonAsync(U("/api/orders/Add"), new PurchaseOrderRequest("Đơn 1"), Json));
        await Read<PurchaseOrderDto>(await staff.PostAsJsonAsync(U("/api/orders/Add"), new PurchaseOrderRequest("Đơn 2", Status: 2), Json));
        var receipt = await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 10, 5), FundId: fund.Id), Json));
        await Read<ReceiptLineDto>(await staff.PostAsJsonAsync(U($"/api/receipts/SaveLine/{receipt.PublicId}"),
            new ReceiptLineRequest(Mfn: bib.Mfn, Quantity: 2, Price: 2m, Currency: "USD", Rate: 25000m), Json));
        await Read<ReceiptDto>(await staff.PostAsJsonAsync(U("/api/receipts/Add"), new ReceiptRequest(new DateOnly(2026, 10, 6)), Json));

        var d = await Read<AcquisitionDashboard>(await staff.GetAsync(U("/api/dashboard")));
        Assert.Equal((2, 2), (d.OrderProgress.OrderTotal, d.OrderProgress.ReceiptTotal));
        Assert.Equal([1, 1, 0], d.OrderProgress.OrdersByStatus.Select(s => s.Count));
        Assert.Equal([2, 0], d.OrderProgress.ReceiptsByStatus.Select(s => s.Count));
        Assert.Equal(["Quỹ sách", "Quỹ trống"], d.BudgetVsSpend.Funds.Select(f => f.Name).Order());
        var used = d.BudgetVsSpend.Funds.Single(f => f.FundId == fund.Id);
        Assert.Equal((1_000_000m, 100_000m), (used.Budget, used.Spend));
        Assert.Equal((1_000_000m, 100_000m), (d.BudgetVsSpend.TotalBudget, d.BudgetVsSpend.TotalSpend));

        var other = await Read<AcquisitionDashboard>(await Staff(b).GetAsync(U("/api/dashboard")));
        Assert.Equal(0, other.OrderProgress.OrderTotal);
        Assert.Empty(other.BudgetVsSpend.Funds);
    }

    [Fact]
    public async Task Dashboard_requires_the_receipt_view_permission()
    {
        var t = NewTenant();
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(t, "AB_ORDERS:view").GetAsync(U("/api/dashboard"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(t, "AB_RECEIPTS:view").GetAsync(U("/api/dashboard"))).StatusCode);
    }
}
