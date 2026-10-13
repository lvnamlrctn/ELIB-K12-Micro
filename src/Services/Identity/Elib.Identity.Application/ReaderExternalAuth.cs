using System.Text.Json;

namespace Elib.Identity.Application;

public enum ExternalAuthOutcome
{
    Success,
    InvalidCredentials,
    Unavailable,
}

/// <summary>Kết quả xác thực ngoài: số thẻ / email hệ thống ngoài trả về để khớp hồ sơ bạn đọc của thư viện.</summary>
public sealed record ExternalAuthResult(ExternalAuthOutcome Outcome, string? CardNo = null, string? Email = null, string? Message = null);

/// <summary>Xác thực bạn đọc qua LDAP hoặc API của trường (ELIB-LRC: ExternalReaderAuth) — cấu hình theo đơn vị.</summary>
public interface IExternalReaderAuth
{
    Task<ExternalAuthResult> AuthenticateAsync(ReaderAuthSettings settings, string login, string password, CancellationToken ct);
}

/// <summary>
/// Tham số READER_AUTH_CONFIG của đơn vị (JSON, cùng cấu trúc mục ReaderAuth của ELIB-LRC):
/// <code>
/// { "Provider": "Local" | "Ldap" | "Api", "FallbackToLocal": true, "MatchBy": "Cardno" | "Email",
///   "Ldap": { "Host", "Port": 389, "UseSsl": false, "BindDn", "BindPassword", "SearchBase", "SearchFilter": "(uid={0})",
///             "CardNoAttribute": "uid", "EmailAttribute": "mail" },
///   "Api":  { "Url", "UsernameField": "username", "PasswordField": "password", "SuccessField": "success",
///             "CardNoField": "cardNo", "EmailField": "email", "ApiKeyHeader", "ApiKey", "TimeoutSeconds": 10 } }
/// </code>
/// <see cref="FallbackToLocal"/>: hệ thống ngoài sai mật khẩu / không kết nối được → thử mật khẩu thư viện (mặc định bật, như LRC).
/// </summary>
public sealed class ReaderAuthSettings
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public string Provider { get; set; } = "Local";
    public bool FallbackToLocal { get; set; } = true;
    public string MatchBy { get; set; } = "Cardno";
    public LdapSettings Ldap { get; set; } = new();
    public ApiSettings Api { get; set; } = new();

    public bool IsLdap => Provider.Trim().Equals("Ldap", StringComparison.OrdinalIgnoreCase);
    public bool IsApi => Provider.Trim().Equals("Api", StringComparison.OrdinalIgnoreCase);
    public bool IsExternal => IsLdap || IsApi;
    public bool MatchByEmail => MatchBy.Trim().Equals("Email", StringComparison.OrdinalIgnoreCase);

    /// <summary>Trống / không phải JSON hợp lệ → null (coi như chỉ dùng mật khẩu thư viện).</summary>
    public static ReaderAuthSettings? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<ReaderAuthSettings>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public sealed class LdapSettings
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public bool UseSsl { get; set; }
        public string? BindDn { get; set; }
        public string? BindPassword { get; set; }
        public string? SearchBase { get; set; }
        public string SearchFilter { get; set; } = "(uid={0})";
        public string CardNoAttribute { get; set; } = "uid";
        public string EmailAttribute { get; set; } = "mail";
    }

    public sealed class ApiSettings
    {
        public string? Url { get; set; }
        public string UsernameField { get; set; } = "username";
        public string PasswordField { get; set; } = "password";
        public string? SuccessField { get; set; }
        public string CardNoField { get; set; } = "cardNo";
        public string EmailField { get; set; } = "email";
        public string? ApiKeyHeader { get; set; }
        public string? ApiKey { get; set; }
        public int TimeoutSeconds { get; set; } = 10;
    }
}

public sealed record ReaderAuthTestRequest(string? Login, string? Password);

/// <summary>Kết quả "Kiểm tra kết nối" (ELIB-LRC: ReaderAuthTest) — không trả mật khẩu; <see cref="ProfileFound"/>: thư viện có hồ sơ khớp.</summary>
public sealed record ReaderAuthTestResult(string Provider, string Outcome, string? CardNo, string? Email, bool ProfileFound, string? Message);
