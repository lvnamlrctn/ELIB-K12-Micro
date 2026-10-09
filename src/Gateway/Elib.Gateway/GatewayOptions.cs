namespace Elib.Gateway;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>Host của quản trị nền tảng (không thuộc đơn vị nào), ví dụ quantri.thuvientn.vn, id.thuvientn.vn.</summary>
    public List<string> SystemHosts { get; set; } = [];

    /// <summary>URL nội bộ của service tenant để tra host → đơn vị.</summary>
    public string TenantServiceUrl { get; set; } = "";

    public int TenantCacheSeconds { get; set; } = 60;

    /// <summary>Số request tối đa mỗi cửa sổ cho một (host, người dùng hoặc IP).</summary>
    public int RateLimitPermits { get; set; } = 300;

    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Dải IP (CIDR) của reverse proxy/ingress đứng trước gateway — chỉ tin X-Forwarded-For/Proto từ các địa chỉ này
    /// (IP thật của client cho rate limit, https cho URL do identity sinh). Rỗng = chỉ loopback.
    /// Không đặt 0.0.0.0/0 khi gateway nhận traffic trực tiếp từ Internet: client sẽ giả được IP để lách rate limit.
    /// </summary>
    public List<string> TrustedProxyNetworks { get; set; } = [];
}

/// <summary>Metadata đặt trên route YARP (appsettings ReverseProxy:Routes:*:Metadata).</summary>
public static class RouteMetadata
{
    /// <summary>Mã module cần license, ví dụ CIRCULATION. Không đặt = không kiểm license.</summary>
    public const string Module = "Module";

    /// <summary>Required (mặc định) | Optional | None — route có cần xác định đơn vị theo host không.</summary>
    public const string Tenant = "Tenant";
}

public static class GatewayErrorCodes
{
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string TenantSuspended = "TENANT_SUSPENDED";
    public const string TenantNotReady = "TENANT_NOT_READY";
    public const string TenantDirectoryUnavailable = "TENANT_DIRECTORY_UNAVAILABLE";
    public const string TenantMismatch = "TENANT_MISMATCH";
    public const string ModuleNotLicensed = "MODULE_NOT_LICENSED";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
}
