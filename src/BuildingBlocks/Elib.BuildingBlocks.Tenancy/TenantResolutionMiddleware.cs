using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Tenancy;

/// <summary>
/// Xác định đơn vị và người thực hiện cho mỗi request. Đặt SAU UseAuthentication.
/// - Có token: tenant lấy từ claim tenant_id (không tin header). Không có claim với staff/service = ngữ cảnh hệ thống.
/// - Ẩn danh: tenant lấy từ header gateway, chỉ khi chữ ký HMAC hợp lệ; sai chữ ký → 401.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    private readonly TenancyOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext http, TenantContext tenant, CurrentActor actor)
    {
        var user = http.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            if (!TryResolveFromClaims(user, tenant, actor))
            {
                await RejectAsync(http, "Token chứa tenant_id/tenant_scope không hợp lệ.");
                return;
            }
        }
        else if (http.Request.Headers.TryGetValue(_options.TenantHeader, out var raw))
        {
            if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId) || tenantId <= 0
                || !GatewaySignature.Verify(_options.GatewaySigningKey, tenantId, http.Request.Headers[_options.SignatureHeader]))
            {
                await RejectAsync(http, "Header đơn vị không hợp lệ hoặc không đi qua gateway.");
                return;
            }
            tenant.Initialize(tenantId, isSystem: false, allowedReadScope: null);
        }

        await next(http);
    }

    private static bool TryResolveFromClaims(ClaimsPrincipal user, TenantContext tenant, CurrentActor actor)
    {
        var subjectType = user.FindFirstValue(ElibClaimTypes.SubjectType) ?? ElibSubjectTypes.Staff;
        long? subject = long.TryParse(user.FindFirstValue(ElibClaimTypes.Subject), NumberStyles.None, CultureInfo.InvariantCulture, out var sub)
            ? sub : null;
        actor.Set(subject, subjectType);

        long? tenantId = null;
        var tenantClaim = user.FindFirstValue(ElibClaimTypes.TenantId);
        if (!string.IsNullOrEmpty(tenantClaim))
        {
            if (!long.TryParse(tenantClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0) return false;
            tenantId = parsed;
        }

        var scope = new List<long>();
        foreach (var part in (user.FindFirstValue(ElibClaimTypes.TenantScope) ?? "")
                     .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0) return false;
            scope.Add(id);
        }

        // Bạn đọc và thiết bị luôn thuộc một đơn vị; chỉ nhân viên cấp hệ thống hoặc service mới được thiếu tenant.
        var isSystem = tenantId is null && subjectType is ElibSubjectTypes.Staff or ElibSubjectTypes.Service;
        if (tenantId is null && !isSystem) return false;

        tenant.Initialize(tenantId, isSystem, scope);
        return true;
    }

    private static Task RejectAsync(HttpContext http, string detail)
    {
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return http.Response.WriteAsJsonAsync(
            new { type = "about:blank", title = "Unauthorized", status = 401, code = "TENANT_INVALID", detail },
            options: null, contentType: "application/problem+json");
    }
}
