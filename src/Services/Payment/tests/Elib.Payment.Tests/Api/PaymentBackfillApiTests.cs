using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Payment;
using Elib.Payment.Application;
using Elib.Payment.Domain;

namespace Elib.Payment.Tests.Api;

/// <summary>Phiếu nợ có sẵn từ TRƯỚC khi đơn vị dùng thanh toán (chưa từng phát ChargeIssued) vẫn thu được.</summary>
public sealed class PaymentBackfillApiTests(PaymentApiFactory factory) : PaymentTestBase(factory)
{
    private static readonly PaymentAccountRequest Account = new(
        VietQrEnabled: true, BankBin: "970422", AccountNo: "0123456789", AccountName: "Thu vien", SepayApiKey: "k", ClearSepayApiKey: false,
        VnPayEnabled: false, VnPayTmnCode: null, VnPayHashSecret: null, ClearVnPayHashSecret: false, VnPayPaymentUrl: null);

    private static ChargeIssued Existing(long tenantId, Guid ticket, Guid reader, decimal amount = 40_000) => new()
    {
        TenantId = tenantId, SourceService = "circulation", SourceId = ticket, Kind = ChargeKinds.FineTicket, ReaderPublicId = reader, CardNo = "HS07",
        Label = "Phiếu phạt PT007", Amount = amount, Version = 5,
    };

    [Fact]
    public async Task Staff_can_collect_a_ticket_that_existed_before_payment_was_enabled()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        Assert.True((await staff.PutAsJsonAsync(U("/api/payment-account"), Account, Json)).IsSuccessStatusCode);
        var ticket = Guid.CreateVersion7();
        Factory.Sources.Seed(tenantId, Existing(tenantId, ticket, Guid.CreateVersion7()));

        // Không có event nào — chỉ có dữ liệu sẵn ở nguồn. Sinh mã vẫn được (đồng bộ khi thiếu), và số tiền lấy từ nguồn.
        var payment = await Read<PaymentDto>(await staff.PostAsJsonAsync(U("/api/payments/Create"), new CreatePaymentRequest("circulation", ticket, "VietQR"), Json));
        Assert.Equal((40_000m, "Phiếu phạt PT007"), (payment.Amount, payment.Label));
    }

    [Fact]
    public async Task A_reader_sees_pre_existing_debts_on_first_visit_and_an_unreachable_source_is_a_clean_not_found()
    {
        var tenantId = NewTenant();
        var reader = Guid.CreateVersion7();
        Factory.Sources.Seed(tenantId, Existing(tenantId, Guid.CreateVersion7(), reader, 15_000));
        var client = Factory.CreateClient().WithClaims(TestClaims.Reader(tenantId, "HS07", reader));
        var mine = Assert.Single(await Read<List<ChargeDto>>(await client.GetAsync(U("/api/reader/charges"))));
        Assert.Equal(15_000m, mine.Amount);

        var other = NewTenant();
        var staff = Staff(other);
        Factory.Sources.Down = true;
        try
        {
            Assert.Equal("CHARGE_NOT_FOUND", await ErrorCode(await staff.PostAsJsonAsync(U("/api/payments/Create"), new CreatePaymentRequest("circulation", Guid.CreateVersion7(), "VietQR"), Json)));
        }
        finally
        {
            Factory.Sources.Down = false;
        }
    }

    [Fact]
    public async Task Resync_needs_the_reconciliation_permission_and_reports_how_many_charges_were_read()
    {
        var tenantId = NewTenant();
        Factory.Sources.Seed(tenantId, Existing(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7()), Existing(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), 9_000));
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "PAYMENT_RECONCILIATION:view").PostAsync(U("/api/charges/Resync"), null)).StatusCode);
        var result = await Read<Dictionary<string, int>>(await Staff(tenantId).PostAsync(U("/api/charges/Resync"), null));
        Assert.Equal(2, result["synced"]);
    }
}
