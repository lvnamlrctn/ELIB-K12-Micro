namespace ELIBAPI.Core.Common;

// Cấu hình cổng thanh toán (port ELIB-LRC 09-25). appsettings là mặc định chung; mỗi đơn vị ghi đè bằng SystemParameter
// PAYMENT_* của đơn vị (xem PaymentConfig) — mỗi trường có tài khoản nhận tiền riêng.

public class VnPayOptions
{
    public string TmnCode    { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string ReturnUrl  { get; set; } = "";
}

public class VietQrOptions
{
    public string BankBin     { get; set; } = "";
    public string AccountNo   { get; set; } = "";
    public string AccountName { get; set; } = "";
}

/// <summary>Đối soát VietQR qua Sepay (https://sepay.vn) — Sepay tự đẩy webhook mỗi khi có biến động số
/// dư trên tài khoản ngân hàng đã liên kết, kèm header "Authorization: Apikey {ApiKey}" để xác thực
/// nguồn gọi (PaymentWebhookController.SepayWebhook).</summary>
public class SepayOptions
{
    public string ApiKey { get; set; } = "";
}

public class PaymentOptions
{
    /// <summary>Số phút QR còn hiệu lực trước khi PaymentExpiryJob tự chuyển Expired.</summary>
    public int ExpireMinutes { get; set; } = 15;
}
