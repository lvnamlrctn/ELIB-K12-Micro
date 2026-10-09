using System.Reflection;
using ELIBAPI.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ELIBAPI.API.Filters;

/// <summary>
/// Đợt 18 — khóa đơn vị theo host cho API công khai (<c>/api/public/**</c>).
/// Trước đây mọi API OPAC tin <c>TenantId</c> (GUID) do trình duyệt tự gửi → đổi GUID là đọc được dữ liệu
/// đơn vị khác, bỏ trống là thấy dữ liệu mọi đơn vị. Nay: nếu host của request khớp 1 đơn vị
/// (<c>&lt;madonvi&gt;.thuvientn.vn</c>, nginx giữ nguyên header Host) thì server GHI ĐÈ mọi tham số/thuộc tính
/// <c>TenantId</c> kiểu Guid bằng đơn vị đó — kể cả khi client không gửi. Host không khớp đơn vị nào
/// (localhost, IP, SSR gọi nội bộ, tên miền chung) → giữ nguyên hành vi cũ (dùng GUID client gửi).
/// Chỉ đụng tham số cấp 1 của action (query/route) và thuộc tính cấp 1 của DTO body.
/// </summary>
public class PublicHostTenantFilter(HostTenantResolver resolver) : IAsyncActionFilter
{
    private const string TenantParam   = "TenantId";
    private const string ResolvedParam = "ResolvedTenantId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/public"))
        {
            var tenant = await resolver.ResolveAsync(context.HttpContext.Request.Host.Host);
            if (tenant != null)
                Apply(context, tenant.Value.PublicId, tenant.Value.Id);
        }
        await next();
    }

    private static void Apply(ActionExecutingContext context, Guid publicId, long id)
    {
        foreach (var p in context.ActionDescriptor.Parameters)
        {
            var type = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

            // Tham số trực tiếp: [FromQuery] Guid? tenantId — gán cả khi client bỏ trống (không có trong ActionArguments).
            if (type == typeof(Guid) && string.Equals(p.Name, TenantParam, StringComparison.OrdinalIgnoreCase))
            {
                context.ActionArguments[p.Name] = publicId;
                continue;
            }

            // DTO body/query: thuộc tính TenantId (Guid/Guid?) và ResolvedTenantId (long?) nếu có.
            if (!context.ActionArguments.TryGetValue(p.Name, out var arg) || arg == null) continue;
            if (!type.IsClass || type == typeof(string)) continue;

            SetProp(arg, TenantParam, typeof(Guid), publicId);
            SetProp(arg, ResolvedParam, typeof(long), id);
        }
    }

    private static void SetProp(object target, string name, Type expected, object value)
    {
        var prop = target.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null || !prop.CanWrite) return;
        var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (propType == expected) prop.SetValue(target, value);
    }
}
