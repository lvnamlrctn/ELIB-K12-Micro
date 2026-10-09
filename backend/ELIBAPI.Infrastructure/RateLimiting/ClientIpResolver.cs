using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.RateLimiting;

/// <summary>Đợt 22 — IP thật của client sau reverse proxy (X-Forwarded-For/X-Real-IP), dùng chung cho mọi
/// policy rate limit của ELIBAPI.API.</summary>
public static class ClientIpResolver
{
    public static string Resolve(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var ips = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ips.Length > 0 && !string.IsNullOrWhiteSpace(ips[0]))
                return ips[0];
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
    }
}
