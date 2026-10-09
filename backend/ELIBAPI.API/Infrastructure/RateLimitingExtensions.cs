using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.RateLimiting;

namespace ELIBAPI.API.Infrastructure;

using ELIBAPI.Infrastructure.RateLimiting; // TenantContextMiddleware.ItemKey

/// <summary>Đợt 22 — port từ ELIB-LRC, khóa theo (đơn vị, IP) thay vì chỉ IP (LRC đơn tenant): 1 đơn vị bị
/// tấn công/dùng quá tải không ảnh hưởng đơn vị khác chia sẻ cùng hạ tầng. Đơn vị lấy từ
/// <see cref="TenantContextMiddleware.ItemKey"/> (đã tra qua Host trước khi tới đây — xem
/// <see cref="TenantContextMiddleware"/>), "0" khi không tra được (route không theo subdomain, hoặc host lạ).
/// Riêng đăng nhập quản trị (<see cref="LoginPolicy"/> dùng ở <c>AuthController</c>) không theo subdomain
/// nên KHÔNG có đơn vị ở bước này — khoá theo IP như LRC.</summary>
public static class RateLimitingExtensions
{
    public const string OpacSearchPolicy   = "opac-search-limit";
    public const string OtpPolicy          = "otp-limit";
    public const string ReaderLoginPolicy  = "reader-login-limit";
    public const string LoginPolicy        = "login-limit";
    public const string ChatPublicPolicy   = "chat-public-limit";
    public const string ChatAdminPolicy    = "chat-admin-limit";
    public const string PaymentWebhookPolicy = "payment-webhook-limit";
    public const string PaymentCreatePolicy  = "payment-create-limit";
    // OaiPmhPolicy: chưa có — OAI-PMH nằm trong ELIBAPI.API.Public (đã xoá); khi gắn lại OaiPmhController vào
    // ELIBAPI.API (như ELIB-LRC 09-25) thì đăng ký policy ở đây.

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Trả JSON chuẩn ApiResponse 429 khi vượt hạn mức.
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var responseObj = new
                {
                    success = false,
                    message = "Bạn đang thực hiện thao tác quá nhanh hoặc quá số lần cho phép. Vui lòng thử lại sau.",
                    statusCode = 429
                };
                await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(responseObj), cancellationToken: token);

                // Ghi audit cho dashboard DevOps (Đợt 22.4) — best-effort, không được làm hỏng phản hồi 429.
                try
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<ELIBAPIDbContext>();
                    db.UserLogs.Add(new UserLog
                    {
                        ActionType = "RateLimitRejected",
                        Object     = context.HttpContext.Request.Path,
                        Ip         = ResolveClientIp(context.HttpContext),
                        Submited   = DateTime.UtcNow,
                        TenantId   = context.HttpContext.Items.TryGetValue(TenantContextMiddleware.ItemKey, out var t) ? t as long? : null,
                    });
                    await db.SaveChangesAsync(token);
                }
                catch { /* best-effort audit, không chặn phản hồi 429 */ }
            };

            // 1. Tra cứu OPAC (unified search): Fixed Window 60 request/phút/(đơn vị+IP).
            options.AddPolicy(OpacSearchPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(TenantIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60, Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                }));

            // 2. Xác thực OTP đăng nhập bạn đọc: Fixed Window 5 request/phút/(đơn vị+IP) — chặn dò mã OTP.
            options.AddPolicy(OtpPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(TenantIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5, Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                }));

            // 3. Đăng nhập bạn đọc OPAC (theo subdomain, có đơn vị): Sliding Window 10 request/5 phút/(đơn vị+IP).
            options.AddPolicy(ReaderLoginPolicy, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(TenantIpKey(httpContext), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(5), SegmentsPerWindow = 5,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                }));

            // 4. Đăng nhập quản trị (KHÔNG theo subdomain — không có đơn vị ở bước này): Sliding Window
            // 10 request/5 phút/IP, đúng LRC (chống brute-force theo IP tấn công, không cần tách đơn vị).
            options.AddPolicy(LoginPolicy, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(ResolveClientIp(httpContext), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(5), SegmentsPerWindow = 5,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                }));

            // 5. Chat AI công khai (port ELIB-LRC 09-29): mỗi câu hỏi tốn lượt gọi Gemini có phí và endpoint cho phép ẩn
            // danh — 12 câu/phút/IP. Trước đây không giới hạn.
            options.AddPolicy(ChatPublicPolicy, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(ResolveClientIp(httpContext), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 12, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                }));
            // 6. Chat thống kê admin: theo tài khoản (fallback IP), 30 câu/phút.
            options.AddPolicy(ChatAdminPolicy, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? ResolveClientIp(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 30, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                    }));

            // 7. Webhook thanh toán (VNPAY IPN / Sepay, port ELIB-LRC 09-25): 120 request/phút/IP — gateway có thể gửi dồn
            // khi retry, nhưng vẫn chặn dò chữ ký từ 1 nguồn.
            options.AddPolicy(PaymentWebhookPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(ResolveClientIp(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120, Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                }));
            // 8. Tạo QR thanh toán từ OPAC: 10 lần/phút/(đơn vị+IP) — mỗi lần ghi 1 giao dịch.
            options.AddPolicy(PaymentCreatePolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(TenantIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst, QueueLimit = 0,
                }));

        });

        return services;
    }

    private static string TenantIpKey(HttpContext context)
    {
        var tenantId = context.Items.TryGetValue(TenantContextMiddleware.ItemKey, out var t) ? (t as long?) ?? 0 : 0;
        return $"{tenantId}:{ClientIpResolver.Resolve(context)}";
    }

    internal static string ResolveClientIp(HttpContext context) => ClientIpResolver.Resolve(context);
}
