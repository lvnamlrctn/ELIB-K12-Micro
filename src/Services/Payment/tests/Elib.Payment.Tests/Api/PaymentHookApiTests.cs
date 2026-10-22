using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Payment;
using Elib.Payment.Application;
using Elib.Payment.Domain;
using Elib.Payment.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Payment.Tests.Api;

/// <summary>Webhook VNPAY / Sepay: chữ ký, số tiền, gửi lại, đồng thời, hết hạn, khoản đã thu cách khác — và đối soát.</summary>
public sealed class PaymentHookApiTests(PaymentApiFactory factory) : PaymentTestBase(factory)
{
    private const string GatewayKey = "test-gateway-key-0123456789abcdef";
    private const string VnPaySecret = "vnpay-secret";
    private const string SepayKey = "sepay-key";

    private static readonly PaymentAccountRequest Account = new(
        VietQrEnabled: true, BankBin: "970422", AccountNo: "0123456789", AccountName: "Thu vien Truong A", SepayApiKey: SepayKey, ClearSepayApiKey: false,
        VnPayEnabled: true, VnPayTmnCode: "ELIBTEST", VnPayHashSecret: VnPaySecret, ClearVnPayHashSecret: false, VnPayPaymentUrl: null);

    private sealed record Setup(long TenantId, HttpClient Staff, Guid Ticket, PaymentDto Payment);

    private async Task<Setup> Pending(string provider, decimal amount = 50_000)
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        Assert.True((await staff.PutAsJsonAsync(U("/api/payment-account"), Account, Json)).IsSuccessStatusCode);
        var ticket = Guid.CreateVersion7();
        await Factory.PublishAsync(new ChargeIssued
        {
            TenantId = tenantId, SourceService = "circulation", SourceId = ticket, Kind = ChargeKinds.FineTicket, ReaderPublicId = Guid.CreateVersion7(), CardNo = "HS01",
            Label = "Phiếu phạt PT001", Amount = amount, Version = 1,
        });
        await Task.Delay(300);
        var payment = await Read<PaymentDto>(await staff.PostAsJsonAsync(U("/api/payments/Create"), new CreatePaymentRequest("circulation", ticket, provider), Json));
        return new Setup(tenantId, staff, ticket, payment);
    }

    /// <summary>Client ẩn danh như gateway chuyển tới: tenant theo host, chữ ký gateway.</summary>
    private HttpClient Anonymous(long tenantId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(CultureInfo.InvariantCulture));
        client.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(GatewayKey, tenantId));
        return client;
    }

    private static Dictionary<string, string> VnPayParams(PaymentDto payment, string responseCode = "00", decimal? amount = null) => new()
    {
        ["vnp_TmnCode"] = "ELIBTEST", ["vnp_TxnRef"] = payment.TransactionCode, ["vnp_Amount"] = decimal.Truncate((amount ?? payment.Amount) * 100).ToString(CultureInfo.InvariantCulture),
        ["vnp_ResponseCode"] = responseCode, ["vnp_TransactionStatus"] = responseCode, ["vnp_TransactionNo"] = "14123456", ["vnp_BankCode"] = "NCB",
        ["vnp_PayDate"] = "20261022101500", ["vnp_OrderInfo"] = "Phi phat",
    };

    private static Uri Signed(Dictionary<string, string> parameters, string secret = VnPaySecret)
    {
        var query = VnPayUrl.Query(parameters);
        return new Uri("/hooks/vnpay/ipn?" + query + "&vnp_SecureHash=" + VnPayUrl.Sign(query, secret), UriKind.Relative);
    }

    private static async Task<(string Code, string Message)> Ipn(HttpClient client, Uri url)
    {
        var json = await client.GetFromJsonAsync<JsonElement>(url, Json);
        return (json.GetProperty("RspCode").GetString()!, json.GetProperty("Message").GetString()!);
    }

    private IEnumerable<PaymentSucceeded> Succeeded(Guid ticket) => Factory.PublishedOf<PaymentSucceeded>().Where(e => e.SourceId == ticket);

    private static async Task<PaymentDto> Reload(HttpClient staff, PaymentDto p) => await Read<PaymentDto>(await staff.GetAsync(U($"/api/payments/{p.PublicId}")));

    [Fact]
    public async Task A_valid_signed_vnpay_ipn_pays_once_and_a_replay_is_acknowledged_without_a_second_event()
    {
        var s = await Pending("VNPAY");
        var anon = Anonymous(s.TenantId);
        var url = Signed(VnPayParams(s.Payment));
        Assert.Equal(("00", "Confirm Success"), await Ipn(anon, url));
        Assert.Equal(("02", "Order already confirmed"), await Ipn(anon, url));

        var paid = await Reload(s.Staff, s.Payment);
        Assert.Equal((TransactionStatuses.Paid, false), (paid.Status, paid.NeedsReview));
        var evt = Assert.Single(Succeeded(s.Ticket));
        Assert.Equal((50_000m, s.Payment.TransactionCode, "VNPAY", false), (evt.Amount, evt.TransactionCode, evt.Provider, evt.Late));
    }

    [Fact]
    public async Task Bad_signature_wrong_amount_unknown_order_and_failed_payment_never_settle()
    {
        var s = await Pending("VNPAY");
        var anon = Anonymous(s.TenantId);
        Assert.Equal("97", (await Ipn(anon, Signed(VnPayParams(s.Payment), "other-secret"))).Code);
        Assert.Equal("04", (await Ipn(anon, Signed(VnPayParams(s.Payment, amount: 1_000)))).Code);
        Assert.True((await Reload(s.Staff, s.Payment)).NeedsReview); // sai số tiền: cần đối soát
        var unknown = VnPayParams(s.Payment);
        unknown["vnp_TxnRef"] = "PT000000000000AAAAAA";
        Assert.Equal("01", (await Ipn(anon, Signed(unknown))).Code);
        Assert.Equal("00", (await Ipn(anon, Signed(VnPayParams(s.Payment, responseCode: "24")))).Code); // khách huỷ: chỉ xác nhận đã nhận
        Assert.Equal(TransactionStatuses.Pending, (await Reload(s.Staff, s.Payment)).Status);
        Assert.Empty(Succeeded(s.Ticket));

        // Đơn vị khác không dùng được mã này (không thấy giao dịch).
        var otherTenant = NewTenant();
        Assert.Equal("99", (await Ipn(Anonymous(otherTenant), Signed(VnPayParams(s.Payment)))).Code);
    }

    [Fact]
    public async Task Money_arriving_after_the_code_expired_is_kept_and_flagged_for_review()
    {
        var s = await Pending("VNPAY");
        Factory.Clock.Advance(TimeSpan.FromMinutes(20)); // quá thời hạn 15 phút
        using (var scope = Factory.Services.CreateScope())
        {
            using var _ = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(s.TenantId);
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PaymentExpiry>().RunAsync(CancellationToken.None)); // job đóng mã
        }
        Assert.Contains(Factory.PublishedOf<PaymentExpired>(), e => e.TransactionCode == s.Payment.TransactionCode);

        Assert.Equal("00", (await Ipn(Anonymous(s.TenantId), Signed(VnPayParams(s.Payment)))).Code);
        var late = await Reload(s.Staff, s.Payment);
        Assert.Equal((TransactionStatuses.PaidLate, true), (late.Status, late.NeedsReview));
        Assert.True(Assert.Single(Succeeded(s.Ticket)).Late);

        // Đối soát: thấy trong danh sách cần xử lý; xử lý tay cần ghi chú.
        var list = await Read<PaymentSearchResult>(await s.Staff.PostAsJsonAsync(U("/api/payments/Search"), new PaymentSearch { NeedsReview = true }, Json));
        Assert.Equal((1, 50_000m), (list.Totals.NeedsReviewCount, list.Totals.NeedsReview));
        Assert.Equal("REASON_REQUIRED", await ErrorCode(await s.Staff.PostAsJsonAsync(U($"/api/payments/{s.Payment.PublicId}/ResolveReview"), new ResolveReviewRequest(" "), Json)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(s.TenantId, "PAYMENT_RECONCILIATION:view").PostAsJsonAsync(U($"/api/payments/{s.Payment.PublicId}/ResolveReview"), new ResolveReviewRequest("ok"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Staff.PostAsJsonAsync(U($"/api/payments/{s.Payment.PublicId}/ResolveReview"), new ResolveReviewRequest("Đã đối chiếu sao kê"), Json)).StatusCode);
        Assert.False((await Reload(s.Staff, s.Payment)).NeedsReview);
    }

    [Fact]
    public async Task Money_for_a_charge_already_closed_by_cash_is_not_reported_to_the_source_again()
    {
        var s = await Pending("VNPAY");
        await Factory.PublishAsync(new ChargeSettled { TenantId = s.TenantId, SourceService = "circulation", SourceId = s.Ticket, Version = 2 });
        await Task.Delay(300);
        Assert.Equal("00", (await Ipn(Anonymous(s.TenantId), Signed(VnPayParams(s.Payment)))).Code);
        var txn = await Reload(s.Staff, s.Payment);
        Assert.Equal((TransactionStatuses.PaidLate, true), (txn.Status, txn.NeedsReview));
        Assert.Contains("hoàn tiền", txn.ReviewNote);
        Assert.Empty(Succeeded(s.Ticket)); // nguồn không ghi lần hai
    }

    [Fact]
    public async Task Parallel_ipns_settle_exactly_once()
    {
        var s = await Pending("VNPAY");
        var url = Signed(VnPayParams(s.Payment));
        var codes = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Ipn(Anonymous(s.TenantId), url)));
        Assert.All(codes, c => Assert.True(c.Code is "00" or "02", c.Code));
        Assert.Single(codes, c => c.Code == "00");
        // (Không đếm event ở đây: test chạy bus không outbox nên lời gọi thua cuộc đã "phát" trước khi lỗi; ở môi trường thật outbox huỷ cùng giao dịch.)
        Assert.Equal(TransactionStatuses.Paid, (await Reload(s.Staff, s.Payment)).Status);
    }

    [Fact]
    public async Task Sepay_needs_the_unit_key_matches_the_code_in_the_note_and_checks_the_amount()
    {
        var s = await Pending("VietQR", 70_000);
        var anon = Anonymous(s.TenantId);
        SepayWebhook Hook(string content, decimal amount = 70_000, string type = "in") =>
            new(Id: 9001, Gateway: "MBBank", TransactionDate: "2026-10-22 10:15:00", AccountNumber: "0123456789", Code: null, Content: content, TransferType: type,
                TransferAmount: amount, ReferenceCode: "FT26295000123", Description: content);
        Task<HttpResponseMessage> Send(SepayWebhook hook, string? key = SepayKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, U("/hooks/sepay")) { Content = JsonContent.Create(hook, options: Json) };
            if (key is not null) request.Headers.TryAddWithoutValidation("Authorization", "Apikey " + key);
            return anon.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Hook(s.Payment.TransactionCode), key: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(Hook(s.Payment.TransactionCode), key: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(Hook("chuyen tien khong ro", 70_000))).StatusCode);                       // không khớp mã: bỏ qua
        Assert.Equal(HttpStatusCode.OK, (await Send(Hook(s.Payment.TransactionCode, 70_000, "out"))).StatusCode);             // tiền ra: bỏ qua
        Assert.Equal(HttpStatusCode.OK, (await Send(Hook(s.Payment.TransactionCode, 1_000))).StatusCode);                     // sai số tiền
        var flagged = await Reload(s.Staff, s.Payment);
        Assert.Equal((TransactionStatuses.Pending, true), (flagged.Status, flagged.NeedsReview));
        Assert.Empty(Succeeded(s.Ticket));

        // Ngân hàng có thể đổi chữ hoa/thường và chen khoảng trắng quanh mã.
        Assert.Equal(HttpStatusCode.OK, (await Send(Hook($"CT DEN:123 {s.Payment.TransactionCode.ToLowerInvariant()} TT PHAT"))).StatusCode);
        Assert.Equal(TransactionStatuses.Paid, (await Reload(s.Staff, s.Payment)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Send(Hook(s.Payment.TransactionCode))).StatusCode); // gửi lại
        Assert.Single(Succeeded(s.Ticket));

        var logs = await Read<CrudPage<WebhookLogRow>>(await s.Staff.PostAsJsonAsync(U("/api/webhook-logs/Search"), new WebhookLogSearch { Provider = "VietQR" }, Json));
        Assert.True(logs.TotalCount >= 6);
        Assert.DoesNotContain(logs.Items, l => l.Payload.Contains(SepayKey, StringComparison.Ordinal)); // khoá không lọt vào nhật ký
    }
}
