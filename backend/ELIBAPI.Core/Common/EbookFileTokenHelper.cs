using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ELIBAPI.Core.Common;

public record EbookFileTokenPayload(Guid FilePublicId, long UserId, string UserName, long ExpiresAt, long? ReaderId = null);

public static class EbookFileTokenHelper
{
    public static string Generate(EbookFileTokenPayload payload, string secret, int expiryMinutes = 60)
    {
        var p = payload with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds() };
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p)));
        return $"{payloadB64}.{Sign(payloadB64, secret)}";
    }

    public static EbookFileTokenPayload? Validate(string token, string secret)
    {
        var dot = token.IndexOf('.');
        if (dot < 0) return null;

        var payloadB64  = token[..dot];
        var sig         = token[(dot + 1)..];
        var expectedSig = Sign(payloadB64, secret);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSig),
                Encoding.UTF8.GetBytes(sig))) return null;

        try
        {
            var json    = Encoding.UTF8.GetString(Base64UrlDecode(payloadB64));
            var payload = JsonSerializer.Deserialize<EbookFileTokenPayload>(json);
            if (payload == null || payload.ExpiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            return payload;
        }
        catch { return null; }
    }

    /// <summary>
    /// Khoá ký token đọc file. <paramref name="configuredSecret"/> (<c>EbookFileSettings:TokenSecret</c>) chỉ được dùng khi
    /// là khoá thật: ≥ 32 ký tự, không phải giá trị giữ chỗ ("&lt;...&gt;" hoặc chứa "change"). Ngược lại suy từ
    /// <paramref name="jwtKey"/> (<c>Jwt:Key</c>, riêng mỗi bản triển khai), tách miền bằng HMAC. Trước đây khoá rơi về giá
    /// trị giữ chỗ công khai trong repo nên ai biết PublicId của file cũng tự tạo được token và tải tài liệu hạn chế.
    /// </summary>
    public static string ResolveSecret(string? configuredSecret, string? jwtKey)
    {
        var configured = configuredSecret?.Trim();
        if (!string.IsNullOrEmpty(configured) && configured.Length >= 32 && !IsPlaceholder(configured)) return configured;

        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException("Thiếu Jwt:Key — không thể ký token đọc tài liệu số.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(jwtKey));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("ebook-file-view-token")));
    }

    private static bool IsPlaceholder(string s)
        => s.StartsWith('<') || s.Contains("change", StringComparison.OrdinalIgnoreCase);

    private static string Sign(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        var padded = (s.Length % 4) switch { 2 => s + "==", 3 => s + "=", _ => s };
        return Convert.FromBase64String(padded);
    }
}
