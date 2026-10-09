using Microsoft.AspNetCore.Http;
namespace ELIBAPI.Infrastructure.RateLimiting;

/// <summary>Đợt 22 — chạy TRƯỚC <c>UseRateLimiter()</c>, ghi sẵn đơn vị (nếu tra được qua Host, giống
/// <see cref="ELIBAPI.API.Filters.PublicHostTenantFilter"/>) vào <c>HttpContext.Items</c> để partition key
/// factory của rate limiter đọc được ĐỒNG BỘ (factory của .NET rate limiting không cho await). Chỉ tra cho
/// <c>/api/public/**</c> (OPAC/OAI-PMH — nơi đơn vị xác định được qua subdomain); route khác (vd đăng nhập
/// quản trị, không theo subdomain) không có giá trị này, các policy dùng route đó tự khoá theo IP.</summary>
public class TenantContextMiddleware(RequestDelegate next)
{
    public const string ItemKey = "RateLimitTenantId";

    public async Task InvokeAsync(HttpContext context, HostTenantResolver resolver)
    {
        if (context.Request.Path.StartsWithSegments("/api/public"))
        {
            var tenant = await resolver.ResolveAsync(context.Request.Host.Host);
            context.Items[ItemKey] = tenant?.Id;
        }
        await next(context);
    }
}
