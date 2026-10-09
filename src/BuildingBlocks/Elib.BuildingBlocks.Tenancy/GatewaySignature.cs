using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Elib.BuildingBlocks.Tenancy;

/// <summary>
/// Chữ ký HMAC gateway gắn vào header X-Tenant-Id cho route ẩn danh của OPAC (docs 03 §2, 05 §4).
/// Service chỉ tin tenant từ header khi chữ ký hợp lệ — client gọi thẳng service không giả được tenant.
/// </summary>
public static class GatewaySignature
{
    public static string Compute(string signingKey, long tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(signingKey);
        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes(tenantId.ToString(CultureInfo.InvariantCulture)));
        return Convert.ToBase64String(mac);
    }

    public static bool Verify(string signingKey, long tenantId, string? signature)
    {
        if (string.IsNullOrEmpty(signingKey) || string.IsNullOrEmpty(signature)) return false;
        var expected = Encoding.UTF8.GetBytes(Compute(signingKey, tenantId));
        var actual = Encoding.UTF8.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
