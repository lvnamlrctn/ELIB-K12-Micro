using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Payment;
using Elib.Payment.Application;
using Elib.Payment.Domain;
using Elib.Payment.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Payment.Tests.Api;

public sealed class PaymentFlowApiTests(PaymentApiFactory factory) : PaymentTestBase(factory)
{
    private static readonly PaymentAccountRequest Account = new(
        VietQrEnabled: true, BankBin: "970422", AccountNo: "0123456789", AccountName: "Thư viện Trường Á", SepayApiKey: "sepay-key", ClearSepayApiKey: false,
        VnPayEnabled: true, VnPayTmnCode: "ELIBTEST", VnPayHashSecret: "vnpay-secret", ClearVnPayHashSecret: false, VnPayPaymentUrl: null);

    private static async Task Settled(Func<Task> act)
    {
        await act();
        await Task.Delay(300); // bus in-memory xử lý nền
    }

    private async Task<(long TenantId, HttpClient Staff, Guid TicketId, Guid ReaderId)> Charged(decimal amount = 50_000, long version = 1)
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        Assert.True((await staff.PutAsJsonAsync(U("/api/payment-account"), Account, Json)).IsSuccessStatusCode);
        var ticket = Guid.CreateVersion7();
        var reader = Guid.CreateVersion7();
        await Issue(tenantId, ticket, reader, amount, version);
        return (tenantId, staff, ticket, reader);
    }

    private async Task Issue(long tenantId, Guid ticket, Guid reader, decimal amount, long version) =>
        await Settled(() => Factory.PublishAsync(new ChargeIssued
        {
            TenantId = tenantId, SourceService = "circulation", SourceId = ticket, Kind = ChargeKinds.FineTicket, ReaderPublicId = reader, CardNo = "HS01",
            Label = "Phiếu phạt PT001", Amount = amount, Version = version,
        }));

    private static Task<HttpResponseMessage> Create(HttpClient staff, Guid ticket, string provider) =>
        staff.PostAsJsonAsync(U("/api/payments/Create"), new CreatePaymentRequest("circulation", ticket, provider), Json);

    [Fact]
    public async Task A_charge_event_lets_staff_create_a_vietqr_code_with_the_amount_and_the_code_as_transfer_note()
    {
        var (_, staff, ticket, _) = await Charged(50_000);
        var payment = await Read<PaymentDto>(await Create(staff, ticket, "vietqr"));
        Assert.Equal((PaymentProviders.VietQr, 50_000m, TransactionStatuses.Pending, "Phiếu phạt PT001"), (payment.Provider, payment.Amount, payment.Status, payment.Label));
        Assert.StartsWith("000201010212", payment.QrContent, StringComparison.Ordinal);
        Assert.Contains("0010A000000727", payment.QrContent, StringComparison.Ordinal);
        Assert.Contains("970422", payment.QrContent, StringComparison.Ordinal);
        Assert.Contains("540550000", payment.QrContent, StringComparison.Ordinal);
        Assert.Contains(payment.TransactionCode, payment.QrContent, StringComparison.Ordinal);
        // CRC16 cuối chuỗi khớp tính lại.
        var body = payment.QrContent[..^4];
        Assert.Equal(VietQrPayload.Crc16(body).ToString("X4", System.Globalization.CultureInfo.InvariantCulture), payment.QrContent[^4..]);

        // Bấm lần nữa khi mã còn hạn → cùng một mã.
        Assert.Equal(payment.PublicId, (await Read<PaymentDto>(await Create(staff, ticket, "VietQR"))).PublicId);
        Assert.Equal(TransactionStatuses.Pending, (await Read<PaymentDto>(await staff.GetAsync(U($"/api/payments/{payment.PublicId}")))).Status);
    }

    [Fact]
    public async Task A_vnpay_url_is_signed_with_the_unit_secret_in_vnd_times_one_hundred_and_switching_provider_cancels_the_old_code()
    {
        var (_, staff, ticket, _) = await Charged(75_000);
        var qr = await Read<PaymentDto>(await Create(staff, ticket, "VietQR"));
        var vn = await Read<PaymentDto>(await Create(staff, ticket, "VNPAY"));

        var uri = new Uri(vn.QrContent);
        Assert.StartsWith(PaymentAccount.SandboxVnPayUrl, vn.QrContent, StringComparison.Ordinal);
        var query = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
        Assert.Equal(("7500000", "ELIBTEST", vn.TransactionCode), (query["vnp_Amount"], query["vnp_TmnCode"], query["vnp_TxnRef"]));
        Assert.True(VnPayUrl.Verify(query, "vnpay-secret"));
        Assert.False(VnPayUrl.Verify(query, "wrong-secret"));
        query["vnp_Amount"] = "100";
        Assert.False(VnPayUrl.Verify(query, "vnpay-secret")); // sửa số tiền → chữ ký sai

        Assert.Equal(TransactionStatuses.Cancelled, (await Read<PaymentDto>(await staff.GetAsync(U($"/api/payments/{qr.PublicId}")))).Status);
    }

    [Fact]
    public async Task A_changed_amount_cancels_the_waiting_code_and_a_closed_charge_cannot_get_a_new_one()
    {
        var (tenantId, staff, ticket, reader) = await Charged(50_000, version: 1);
        var first = await Read<PaymentDto>(await Create(staff, ticket, "VietQR"));

        await Issue(tenantId, ticket, reader, 20_000, version: 2); // phiếu được giảm
        Assert.Equal(TransactionStatuses.Cancelled, (await Read<PaymentDto>(await staff.GetAsync(U($"/api/payments/{first.PublicId}")))).Status);
        var second = await Read<PaymentDto>(await Create(staff, ticket, "VietQR"));
        Assert.Equal(20_000m, second.Amount);

        await Issue(tenantId, ticket, reader, 5_000, version: 1); // event cũ: bỏ qua
        Assert.Equal(TransactionStatuses.Pending, (await Read<PaymentDto>(await staff.GetAsync(U($"/api/payments/{second.PublicId}")))).Status);

        await Settled(() => Factory.PublishAsync(new ChargeSettled { TenantId = tenantId, SourceService = "circulation", SourceId = ticket, Version = 3 })); // thu tiền mặt
        Assert.Equal(TransactionStatuses.Cancelled, (await Read<PaymentDto>(await staff.GetAsync(U($"/api/payments/{second.PublicId}")))).Status);
        Assert.Equal("CHARGE_CLOSED", await ErrorCode(await Create(staff, ticket, "VietQR")));
    }

    [Fact]
    public async Task Provider_must_be_enabled_and_the_charge_must_exist()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var ticket = Guid.CreateVersion7();
        Assert.Equal("CHARGE_NOT_FOUND", await ErrorCode(await Create(staff, ticket, "VietQR")));
        await Issue(tenantId, ticket, Guid.CreateVersion7(), 10_000, 1);
        Assert.Equal("PROVIDER_NOT_ENABLED", await ErrorCode(await Create(staff, ticket, "VietQR"))); // chưa khai tài khoản nhận tiền
        Assert.Equal("PROVIDER_INVALID", await ErrorCode(await Create(staff, ticket, "PAYPAL")));
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(Staff(tenantId, "PAYMENT_TRANSACTIONS:view"), ticket, "VietQR")).StatusCode);
    }

    [Fact]
    public async Task Readers_see_and_pay_only_their_own_charges()
    {
        var (tenantId, staff, ticket, reader) = await Charged(30_000);
        var other = Guid.CreateVersion7();
        await Issue(tenantId, Guid.CreateVersion7(), other, 99_000, 1);

        HttpClient Reader(Guid id) => Factory.CreateClient().WithClaims(TestClaims.Reader(tenantId, "HS01", id));
        var mine = await Read<List<ChargeDto>>(await Reader(reader).GetAsync(U("/api/reader/charges")));
        var charge = Assert.Single(mine);
        Assert.Equal((30_000m, ticket), (charge.Amount, charge.SourceId));
        Assert.Equal(["VietQR", "VNPAY"], charge.Providers);

        var created = await Read<PaymentDto>(await Reader(reader).PostAsJsonAsync(U("/api/reader/payments/Create"), new ReaderCreatePaymentRequest(charge.PublicId, "VietQR"), Json));
        Assert.Equal(TransactionStatuses.Pending, created.Status);

        // Người khác không tạo mã / xem mã của khoản này.
        Assert.Equal("CHARGE_NOT_FOUND", await ErrorCode(await Reader(other).PostAsJsonAsync(U("/api/reader/payments/Create"), new ReaderCreatePaymentRequest(charge.PublicId, "VietQR"), Json)));
        Assert.Equal(HttpStatusCode.NotFound, (await Reader(other).GetAsync(U($"/api/reader/payments/{created.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Reader(reader).GetAsync(U($"/api/reader/payments/{created.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U("/api/reader/charges"))).StatusCode); // token cán bộ không dùng được
    }

    [Fact]
    public async Task Expired_codes_are_closed_when_read()
    {
        var (_, staff, ticket, _) = await Charged();
        var payment = await Read<PaymentDto>(await Create(staff, ticket, "VietQR"));
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            await db.Database.ExecuteSqlRawAsync("UPDATE payment_transactions SET expires_at = {0} WHERE public_id = {1}", DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"), payment.PublicId);
        }
        Assert.Equal(TransactionStatuses.Expired, (await Read<PaymentDto>(await staff.GetAsync(U($"/api/payments/{payment.PublicId}")))).Status);
    }
}
