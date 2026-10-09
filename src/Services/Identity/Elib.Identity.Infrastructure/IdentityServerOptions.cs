namespace Elib.Identity.Infrastructure;

public static class ElibScopes
{
    /// <summary>Scope/audience chung cho mọi API của nền tảng. Service cấu hình Auth:Audience = "elib-api".</summary>
    public const string Api = "elib-api";
}

public sealed class IdentityServerOptions
{
    public const string SectionName = "Identity";

    /// <summary>URL công khai của identity, ví dụ https://id.thuvientn.vn/ — là "iss" trong token.</summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Khoá ký/mã hoá sinh trong bộ nhớ — CHỈ cho dev/test (mất khi khởi động lại, không dùng được nhiều replica).
    /// Production dùng chứng chỉ PFX từ secret.
    /// </summary>
    public bool UseEphemeralKeys { get; set; }

    public string? SigningCertificatePath { get; set; }
    public string? SigningCertificatePassword { get; set; }
    public string? EncryptionCertificatePath { get; set; }
    public string? EncryptionCertificatePassword { get; set; }

    /// <summary>false chỉ khi chạy dev/test bằng HTTP. Sau ingress TLS, X-Forwarded-Proto đã là https.</summary>
    public bool RequireHttps { get; set; } = true;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Khoá = ClientId. Dùng dictionary (không phải mảng) để secret đặt được theo tên:
    /// Identity__Clients__svc-gateway__ClientSecret — mảng gộp cấu hình theo chỉ số, rất dễ ghi đè nhầm.
    /// </summary>
    public Dictionary<string, ClientDefinition> Clients { get; set; } = new(StringComparer.Ordinal);

    public BootstrapAdminOptions BootstrapAdmin { get; set; } = new();

    /// <summary>CAPTCHA/OTP cho tài khoản hệ thống (đơn vị thì theo tham số ADMIN_LOGIN_* ở service tenant).</summary>
    public SystemLoginOptions SystemLogin { get; set; } = new();

    /// <summary>URL nội bộ service tenant — đọc tham số đăng nhập của đơn vị (gọi bằng service token của client svc-identity).</summary>
    public string TenantServiceUrl { get; set; } = "http://tenant:8080";
}

/// <summary>Client OIDC được seed lúc khởi động. Secret của client confidential lấy từ biến môi trường.</summary>
public sealed class ClientDefinition
{
    public string? ClientSecret { get; set; }
    public string DisplayName { get; set; } = "";
    public List<string> RedirectUris { get; set; } = [];
    public List<string> PostLogoutRedirectUris { get; set; } = [];

    /// <summary>Mẫu cho app chạy trên tên miền từng đơn vị, vd https://*.thuvientn.vn/admin/callback (xem RedirectUriPattern).</summary>
    public List<string> RedirectUriPatterns { get; set; } = [];
    public List<string> PostLogoutRedirectUriPatterns { get; set; } = [];

    /// <summary>authorization_code, refresh_token, client_credentials</summary>
    public List<string> GrantTypes { get; set; } = [];
}

/// <summary>Tài khoản quản trị hệ thống đầu tiên — chỉ tạo khi chưa có tài khoản hệ thống nào. Mật khẩu từ secret.</summary>
public sealed class BootstrapAdminOptions
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FullName { get; set; } = "Quản trị hệ thống";

    /// <summary>Email nhận OTP khi bật Identity:SystemLogin:OtpEnabled. Gán cho tài khoản bootstrap nếu tài khoản chưa có email.</summary>
    public string? Email { get; set; }
}
