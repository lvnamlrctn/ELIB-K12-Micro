using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Payment;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Services.Payment;

public class VietQrGatewayService(PaymentConfig config) : IPaymentGatewayService
{
    public string Provider => "VietQR";

    public async Task<bool> IsConfiguredAsync(long? tenantId)
    {
        var o = await config.VietQrAsync(tenantId);
        return o.BankBin != "" && o.AccountNo != "";
    }

    public async Task<PaymentQrResult> BuildQrAsync(PaymentTransaction txn)
    {
        var o = await config.VietQrAsync(txn.TenantId);
        if (o.BankBin == "" || o.AccountNo == "")
            throw new ArgumentException("Đơn vị chưa cấu hình tài khoản nhận tiền VietQR.");
        var qr = VietQrPayloadBuilder.Build(o.BankBin, o.AccountNo, o.AccountName, txn.Amount, txn.TransactionCode);
        return new PaymentQrResult(qr, null);
    }
}

public class VnPayGatewayService(PaymentConfig config, IHttpContextAccessor httpContextAccessor) : IPaymentGatewayService
{
    public string Provider => "VNPAY";

    public async Task<bool> IsConfiguredAsync(long? tenantId)
    {
        var o = await config.VnPayAsync(tenantId);
        return o.TmnCode != "" && o.HashSecret != "";
    }

    public async Task<PaymentQrResult> BuildQrAsync(PaymentTransaction txn)
    {
        var o = await config.VnPayAsync(txn.TenantId);
        if (o.TmnCode == "" || o.HashSecret == "")
            throw new ArgumentException("Đơn vị chưa cấu hình cổng VNPAY.");
        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]    = "2.1.0",
            ["vnp_Command"]    = "pay",
            ["vnp_TmnCode"]    = o.TmnCode,
            ["vnp_Amount"]     = ((long)Math.Round(txn.Amount * 100)).ToString(), // VNPAY yêu cầu *100, không thập phân
            ["vnp_CurrCode"]   = "VND",
            ["vnp_TxnRef"]     = txn.TransactionCode,
            ["vnp_OrderInfo"]  = $"Thanh toan {txn.TargetType} {txn.TransactionCode}",
            ["vnp_OrderType"]  = "other",
            ["vnp_Locale"]     = "vn",
            ["vnp_ReturnUrl"]  = o.ReturnUrl,
            ["vnp_IpAddr"]     = ip,
            ["vnp_CreateDate"] = LibraryClock.Now.ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = txn.ExpiresAt.ToString("yyyyMMddHHmmss"),
        };

        var hash = VnPayHelper.Sign(data, o.HashSecret);
        var query = string.Join("&", data.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var payUrl = $"{o.PaymentUrl}?{query}&vnp_SecureHash={hash}";

        // QR chính là mã hoá payUrl — người dùng quét bằng app ngân hàng hỗ trợ VNPAY QR, hoặc bấm link
        // trực tiếp trên di động.
        return new PaymentQrResult(payUrl, payUrl);
    }
}

public class PaymentGatewayResolver(IEnumerable<IPaymentGatewayService> gateways) : IPaymentGatewayResolver
{
    public IPaymentGatewayService Resolve(string provider)
    {
        var svc = gateways.FirstOrDefault(g => string.Equals(g.Provider, provider, StringComparison.OrdinalIgnoreCase));
        if (svc == null) throw new ArgumentException($"Không hỗ trợ cổng thanh toán '{provider}'.");
        return svc;
    }

    public async Task<List<string>> AvailableAsync(long? tenantId)
    {
        var list = new List<string>();
        foreach (var g in gateways)
            if (await g.IsConfiguredAsync(tenantId)) list.Add(g.Provider);
        // VietQR trước (không phí, quét bằng mọi app ngân hàng).
        return list.OrderBy(p => p == "VietQR" ? 0 : 1).ToList();
    }
}
