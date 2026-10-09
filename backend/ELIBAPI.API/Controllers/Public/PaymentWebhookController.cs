using System.Security.Cryptography;
using System.Text;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services.Payment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Public;

/// <summary>Webhook công khai nhận xác nhận thanh toán (port ELIB-LRC 09-25) — gác cổng bằng chữ ký/khoá thay vì lọc IP (nguồn gọi
/// là hạ tầng của gateway/ngân hàng).
/// K12 — mỗi đơn vị một tài khoản nhận tiền:
/// - VNPAY IPN: tìm giao dịch theo vnp_TxnRef rồi kiểm chữ ký bằng HashSecret của ĐƠN VỊ sở hữu giao dịch; đối chiếu vnp_Amount
///   (LRC không kiểm số tiền).
/// - Sepay: <c>POST sepay</c> dùng khoá chung (appsettings Sepay:ApiKey, 1 tài khoản Sepay gom nhiều đơn vị);
///   <c>POST sepay/{tenantPublicId}</c> dùng khoá riêng của đơn vị (PAYMENT_SEPAY_APIKEY) và chỉ khớp giao dịch của đơn vị đó.</summary>
[Route("api/public/payment-webhook")]
[EnableRateLimiting(RateLimitingExtensions.PaymentWebhookPolicy)]
public class PaymentWebhookController(IPaymentService paymentService, PaymentConfig config, ELIBAPIDbContext db, ILogger<PaymentWebhookController> logger)
    : PublicBaseController
{
    // IPN VNPAY — trả đúng format {RspCode, Message} mà VNPAY yêu cầu (không dùng ApiResponse<T> ở đây).
    [HttpGet("vnpay")]
    public async Task<IActionResult> VnPayIpn()
    {
        var query = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        query.TryGetValue("vnp_SecureHash", out var receivedHash);
        query.TryGetValue("vnp_TxnRef", out var txnRef);
        query.TryGetValue("vnp_ResponseCode", out var responseCode);
        query.TryGetValue("vnp_TransactionNo", out var providerRef);
        query.TryGetValue("vnp_Amount", out var rawAmount);

        if (string.IsNullOrEmpty(txnRef) || string.IsNullOrEmpty(receivedHash))
            return Ok(new { RspCode = "97", Message = "Invalid signature" });

        // Cần biết đơn vị để lấy đúng HashSecret — chỉ đọc theo mã (chuỗi ngắn, có chỉ mục duy nhất), chưa đổi gì trước khi kiểm chữ ký.
        var txn = await db.PaymentTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionCode == txnRef && x.IsDelete != 2 && x.Provider == "VNPAY");
        var secret = (await config.VnPayAsync(txn?.TenantId)).HashSecret;
        if (!VnPayHelper.Verify(query, secret, receivedHash))
            return Ok(new { RspCode = "97", Message = "Invalid signature" });

        if (txn == null) return Ok(new { RspCode = "01", Message = "Order not found" });
        if (!long.TryParse(rawAmount, out var amount100) || amount100 != (long)Math.Round(txn.Amount * 100))
            return Ok(new { RspCode = "04", Message = "Invalid amount" });
        if (txn.Status != "Pending") return Ok(new { RspCode = "02", Message = "Order already confirmed" });

        if (responseCode != "00")
            return Ok(new { RspCode = "00", Message = "Confirm Success" }); // VNPAY báo giao dịch thất bại — không settle, chỉ ack

        await paymentService.SettleAsync(txn.Id, providerRef);
        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }

    // Sepay (https://sepay.vn) đẩy webhook này mỗi khi có biến động số dư trên tài khoản ngân hàng đã liên kết — dùng cho đối soát
    // VietQR. Xác thực header "Authorization: Apikey {key}" TRƯỚC khi đụng DB; khớp giao dịch bằng TransactionCode trong nội dung
    // chuyển khoản + đúng số tiền.
    [HttpPost("sepay")]
    public Task<IActionResult> SepayWebhook([FromHeader(Name = "Authorization")] string? authorization, [FromBody] SepayWebhookPayload payload) =>
        HandleSepayAsync(authorization, payload, null);

    [HttpPost("sepay/{tenantPublicId:guid}")]
    public async Task<IActionResult> SepayTenantWebhook(Guid tenantPublicId, [FromHeader(Name = "Authorization")] string? authorization,
        [FromBody] SepayWebhookPayload payload)
    {
        var tenantId = await db.Tenants.AsNoTracking().Where(t => t.PublicId == tenantPublicId && t.IsDelete != 2)
            .Select(t => (long?)t.Id).FirstOrDefaultAsync();
        if (tenantId == null) return Unauthorized(ApiResponse<object>.Fail("Chữ ký không hợp lệ", 401));
        return await HandleSepayAsync(authorization, payload, tenantId);
    }

    private async Task<IActionResult> HandleSepayAsync(string? authorization, SepayWebhookPayload payload, long? tenantId)
    {
        var expected = await config.SepayApiKeyAsync(tenantId);
        if (string.IsNullOrEmpty(expected) || authorization == null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(authorization), Encoding.UTF8.GetBytes($"Apikey {expected}")))
            return Unauthorized(ApiResponse<object>.Fail("Chữ ký không hợp lệ", 401));

        // Chỉ xử lý tiền vào; bỏ qua các biến động ra khỏi tài khoản.
        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            return Ok(ApiResponse<object>.Ok(null!, "Bỏ qua (transferType != in)"));

        var content = $"{payload.Content} {payload.Code} {payload.Description}";
        var txn = await paymentService.FindPendingByTransferContentAsync(content, payload.TransferAmount, tenantId);
        if (txn == null) return Ok(ApiResponse<object>.Ok(null!, "Không khớp giao dịch nào — bỏ qua"));

        // Tiền phải vào đúng tài khoản của đơn vị sở hữu giao dịch (Sepay báo số tài khoản nhận).
        var account = (await config.VietQrAsync(txn.TenantId)).AccountNo;
        if (!string.IsNullOrEmpty(payload.AccountNumber) && account != "" && payload.AccountNumber.Trim() != account)
        {
            logger.LogWarning("Sepay: giao dịch {Code} khớp nội dung nhưng tiền vào tài khoản khác của đơn vị — bỏ qua", txn.TransactionCode);
            return Ok(ApiResponse<object>.Ok(null!, "Sai tài khoản nhận — bỏ qua"));
        }

        await paymentService.SettleAsync(txn.Id, payload.ReferenceCode);
        return Ok(ApiResponse<object>.Ok(null!, "OK"));
    }
}

/// <summary>Payload webhook chuẩn của Sepay — https://docs.sepay.vn/tich-hop-webhooks.html</summary>
public class SepayWebhookPayload
{
    public long?   Id { get; set; }
    public string? Gateway { get; set; }
    public string? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }
    public string? Code { get; set; }
    public string? Content { get; set; }
    public string? TransferType { get; set; } // "in" | "out"
    public double  TransferAmount { get; set; }
    public double  Accumulated { get; set; }
    public string? SubAccount { get; set; }
    public string? ReferenceCode { get; set; }
    public string? Description { get; set; }
}
