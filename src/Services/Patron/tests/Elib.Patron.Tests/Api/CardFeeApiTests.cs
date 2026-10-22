using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Payment;
using Elib.Patron.Application;

namespace Elib.Patron.Tests.Api;

/// <summary>Phí cấp lại thẻ: lập khoản thu, thu tiền mặt / qua QR (không ghi hai lần), huỷ có lý do, nhật ký.</summary>
public sealed class CardFeeApiTests(PatronApiFactory factory) : IClassFixture<PatronApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 6500;
    private static long _nextUserId = 16500;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

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

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private async Task<(long TenantId, HttpClient Staff)> WithReader(string card)
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        Assert.True((await staff.PostAsJsonAsync(U("/api/readers/Add"), new ReaderRequest(card, "Nguyễn Văn", "An"), Json)).IsSuccessStatusCode);
        return (tenantId, staff);
    }

    private static Task<HttpResponseMessage> Issue(HttpClient staff, string card, decimal amount = 30_000, string reason = "Mất thẻ cũ") =>
        staff.PostAsJsonAsync(U("/api/card-fees/Add"), new CardFeeRequest(card, amount, reason), Json);

    [Fact]
    public async Task Issuing_a_fee_announces_the_charge_and_only_one_open_fee_per_reader_is_allowed()
    {
        var (_, staff) = await WithReader("CF01");
        Assert.Equal("REASON_REQUIRED", await ErrorCode(await Issue(staff, "cf01", reason: " ")));
        Assert.Equal("AMOUNT_INVALID", await ErrorCode(await Issue(staff, "cf01", amount: 0)));
        Assert.Equal("READER_NOT_FOUND", await ErrorCode(await Issue(staff, "ZZ99")));

        var fee = await Read<CardFeeDto>(await Issue(staff, "cf01"));
        Assert.Equal(("CF01", "Nguyễn Văn An", 30_000m, "Open"), (fee.CardNo, fee.ReaderName, fee.Amount, fee.Status));
        var charge = Assert.Single(factory.PublishedOf<ChargeIssued>(), e => e.SourceId == fee.PublicId);
        Assert.Equal(("patron", "CARD_REISSUE", 30_000m), (charge.SourceService, charge.Kind, charge.Amount));
        Assert.Equal("CARD_FEE_EXISTS", await ErrorCode(await Issue(staff, "CF01")));
    }

    [Fact]
    public async Task Cash_closes_the_fee_and_tells_payment_and_a_qr_payment_closes_it_once()
    {
        var (tenantId, staff) = await WithReader("CF02");
        var cash = await Read<CardFeeDto>(await Issue(staff, "CF02"));
        var settled = await Read<CardFeeDto>(await staff.PostAsync(U($"/api/card-fees/{cash.PublicId}/SettleCash"), null));
        Assert.Equal(("Paid", "CASH"), (settled.Status, settled.PaidVia));
        Assert.Contains(factory.PublishedOf<ChargeSettled>(), e => e.SourceId == cash.PublicId);
        Assert.Equal("CARD_FEE_CLOSED", await ErrorCode(await staff.PostAsync(U($"/api/card-fees/{cash.PublicId}/SettleCash"), null)));

        var qr = await Read<CardFeeDto>(await Issue(staff, "CF02", 20_000, "Rách thẻ"));
        var paid = new PaymentSucceeded
        {
            TenantId = tenantId, SourceService = "patron", SourceId = qr.PublicId, Kind = "CARD_REISSUE", TransactionCode = "PT261022000000112233", Provider = "VietQR",
            Amount = 20_000, PaidAt = DateTimeOffset.UtcNow, ReaderPublicId = Guid.CreateVersion7(),
        };
        await factory.PublishAsync(paid);
        await factory.PublishAsync(paid with { EventId = Guid.CreateVersion7() });
        await Task.Delay(400);
        var page = await Read<CrudPage<CardFeeDto>>(await staff.PostAsJsonAsync(U("/api/card-fees/Search"), new CardFeeSearch { CardNo = "CF02" }, Json));
        var closed = Assert.Single(page.Items, f => f.PublicId == qr.PublicId);
        Assert.Equal(("Paid", "QR"), (closed.Status, closed.PaidVia));
        var last = factory.PublishedOf<ChargeIssued>().Where(e => e.SourceId == qr.PublicId).MaxBy(e => e.Version)!;
        Assert.Equal((0m, "PT261022000000112233"), (last.Amount, last.LastPaymentCode));
    }

    [Fact]
    public async Task Cancelling_needs_a_reason_withdraws_the_charge_and_permissions_are_enforced()
    {
        var (tenantId, staff) = await WithReader("CF03");
        var fee = await Read<CardFeeDto>(await Issue(staff, "CF03"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "READER_CARD_FEES:view").PostAsJsonAsync(U($"/api/card-fees/{fee.PublicId}/Cancel"), new CardFeeCancelRequest("x"), Json)).StatusCode);
        Assert.Equal("REASON_REQUIRED", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/card-fees/{fee.PublicId}/Cancel"), new CardFeeCancelRequest(null), Json)));
        var cancelled = await Read<CardFeeDto>(await staff.PostAsJsonAsync(U($"/api/card-fees/{fee.PublicId}/Cancel"), new CardFeeCancelRequest("Lập nhầm"), Json));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Contains(factory.PublishedOf<ChargeWaived>(), e => e.SourceId == fee.PublicId);
        Assert.Single((await Read<CrudPage<CardFeeDto>>(await staff.PostAsJsonAsync(U("/api/card-fees/Search"), new CardFeeSearch { FeeStatus = "Cancelled" }, Json))).Items, f => f.PublicId == fee.PublicId);
    }
}
