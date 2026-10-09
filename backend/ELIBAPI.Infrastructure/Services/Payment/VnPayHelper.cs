using System.Security.Cryptography;
using System.Text;

namespace ELIBAPI.Infrastructure.Services.Payment;

/// <summary>Ký/kiểm tra chữ ký HMAC-SHA512 theo tài liệu tích hợp VNPAY (vnp_SecureHash) — dùng chung
/// cho cả lúc build payment URL (VnPayGatewayService) và lúc xác thực IPN webhook (PaymentWebhookController).</summary>
public static class VnPayHelper
{
    public static string Sign(SortedDictionary<string, string> data, string hashSecret)
    {
        var signData = string.Join("&", data
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var keyBytes = Encoding.UTF8.GetBytes(hashSecret);
        var dataBytes = Encoding.UTF8.GetBytes(signData);
        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(IDictionary<string, string> query, string hashSecret, string? receivedHash)
    {
        if (string.IsNullOrEmpty(receivedHash) || string.IsNullOrEmpty(hashSecret)) return false;
        var data = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in query)
            if (kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                data[kv.Key] = kv.Value;
        var computed = Sign(data, hashSecret);
        // So sánh thời gian cố định — tránh lộ chữ ký đúng qua thời gian phản hồi.
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(receivedHash.ToLowerInvariant()));
    }
}
