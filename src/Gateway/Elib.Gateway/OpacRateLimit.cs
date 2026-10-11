using System.Threading.RateLimiting;

namespace Elib.Gateway;

/// <summary>Giới hạn lượt gọi OPAC công khai theo IP (chống quét dữ liệu) — tên policy đặt ở route YARP ("RateLimiterPolicy").</summary>
public static class OpacRateLimit
{
    public const string Policy = "opac";
    public const string HeavyPolicy = "opac-heavy";
    private const int Segments = 6;

    /// <summary>Retry-After khi limiter không tự báo: một đoạn của cửa sổ một phút.</summary>
    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(1) / Segments;

    /// <summary>Theo host + IP thật của client (đã qua UseForwardedHeaders) — mỗi thư viện một hạn mức riêng.</summary>
    public static string Key(HttpContext http, string policy) =>
        $"{policy}|{http.Request.Host.Host}|{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    /// <summary>Cửa sổ trượt 6 đoạn: không cho dồn gấp đôi hạn mức ở ranh giới hai cửa sổ như cửa sổ cố định.</summary>
    public static RateLimiter Window(int permits, TimeSpan window) => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = Math.Max(1, permits),
        Window = window,
        SegmentsPerWindow = Segments,
        QueueLimit = 0,
    });
}
