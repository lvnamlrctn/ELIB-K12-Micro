using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Elib.BuildingBlocks.Observability;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;

namespace Elib.Gateway;

/// <summary>
/// Chạy trong pipeline proxy, sau xác thực (docs 03 §2):
/// xoá header nội bộ client tự gửi → xác định đơn vị theo host → đối chiếu với token → kiểm license module →
/// gắn X-Tenant-Id + chữ ký HMAC + X-Correlation-Id cho service phía sau.
/// </summary>
public sealed partial class GatewayTenantMiddleware(
    RequestDelegate next, ITenantDirectory directory, IOptions<GatewayOptions> gateway, IOptions<TenancyOptions> tenancy,
    ILogger<GatewayTenantMiddleware> logger)
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Không tra được đơn vị cho host {Host} từ service tenant")]
    private static partial void LogDirectoryUnavailable(ILogger logger, Exception exception, string host);

    private const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext http)
    {
        var request = http.Request;
        var options = tenancy.Value;
        request.Headers.Remove(options.TenantHeader);
        request.Headers.Remove(options.SignatureHeader);

        var route = http.GetReverseProxyFeature().Route.Config;
        var metadata = route.Metadata ?? new Dictionary<string, string>();
        var tenantMode = metadata.GetValueOrDefault(RouteMetadata.Tenant, "Required");
        var module = metadata.GetValueOrDefault(RouteMetadata.Module);

        var host = request.Host.Host.ToLowerInvariant();
        var isSystemHost = gateway.Value.SystemHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));
        TenantInfo? tenant = null;

        if (!isSystemHost && tenantMode != "None")
        {
            try
            {
                tenant = await directory.FindByHostAsync(host, http.RequestAborted);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !http.RequestAborted.IsCancellationRequested)
            {
                // Service tenant chưa sẵn sàng/quá tải: trả 503 có mã lỗi — không để lỗi chưa xử lý (stack trace) lọt ra client.
                LogDirectoryUnavailable(logger, ex, host);
                http.Response.Headers.RetryAfter = "5";
                await GatewayProblem.WriteAsync(http, 503, GatewayErrorCodes.TenantDirectoryUnavailable, "Hệ thống đang bận, vui lòng thử lại sau ít giây.");
                return;
            }
            if (tenant is null && tenantMode == "Required")
            {
                await GatewayProblem.WriteAsync(http, 404, GatewayErrorCodes.TenantNotFound, $"Không có thư viện nào ở địa chỉ '{host}'.");
                return;
            }
            if (tenant is not null && !tenant.IsActive && tenantMode == "Required")
            {
                var (status, code, detail) = tenant.Status == "Suspended"
                    ? (403, GatewayErrorCodes.TenantSuspended, "Thư viện đang tạm ngưng hoạt động.")
                    : (503, GatewayErrorCodes.TenantNotReady, "Thư viện đang được khởi tạo.");
                await GatewayProblem.WriteAsync(http, status, code, detail);
                return;
            }
        }
        else if (tenantMode == "Required")
        {
            await GatewayProblem.WriteAsync(http, 404, GatewayErrorCodes.TenantNotFound, "Chức năng này chỉ dùng trên địa chỉ của thư viện.");
            return;
        }

        // Token của đơn vị A không được dùng trên host của đơn vị B; tài khoản hệ thống/service (không có tenant_id) thì được.
        if (http.User.Identity?.IsAuthenticated == true && http.User.FindFirstValue(ElibClaimTypes.TenantId) is { Length: > 0 } tokenTenant)
        {
            var hostTenant = tenant?.TenantId.ToString(CultureInfo.InvariantCulture);
            if (tokenTenant != hostTenant)
            {
                await GatewayProblem.WriteAsync(http, 403, GatewayErrorCodes.TenantMismatch, "Phiên đăng nhập không thuộc thư viện này.");
                return;
            }
        }

        if (!string.IsNullOrEmpty(module) && tenant is not null && !tenant.Modules.Contains(module, StringComparer.OrdinalIgnoreCase))
        {
            await GatewayProblem.WriteAsync(http, 403, GatewayErrorCodes.ModuleNotLicensed, $"Thư viện chưa đăng ký phân hệ {module}.");
            return;
        }

        if (tenant is not null)
        {
            ElibObservability.TagTenant(tenant.TenantId);
            request.Headers[options.TenantHeader] = tenant.TenantId.ToString(CultureInfo.InvariantCulture);
            request.Headers[options.SignatureHeader] = GatewaySignature.Compute(options.GatewaySigningKey, tenant.TenantId);
        }

        if (!Guid.TryParse(request.Headers[CorrelationHeader], out _))
            request.Headers[CorrelationHeader] = Guid.NewGuid().ToString("N");
        http.Response.Headers[CorrelationHeader] = request.Headers[CorrelationHeader];

        await next(http);
    }
}

internal static class GatewayProblem
{
    public static Task WriteAsync(HttpContext http, int status, string code, string detail)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(
            new { type = "about:blank", status, code, detail, service = "gateway", traceId = Activity.Current?.TraceId.ToHexString() ?? http.TraceIdentifier },
            options: null, contentType: "application/problem+json");
    }
}
