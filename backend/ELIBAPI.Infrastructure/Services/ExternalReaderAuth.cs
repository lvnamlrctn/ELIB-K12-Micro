using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ELIBAPI.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>
/// Xác thực bạn đọc qua hệ thống bên ngoài (yêu cầu kỹ thuật mục 1: "truy xuất/xác thực người dùng từ phần mềm quản lý thư viện
/// sẵn có qua API/LDAP, cấu hình được cho từng khách hàng") — port ELIB-LRC 10-04.
/// K12 — mỗi đơn vị một cấu hình: SystemParameter <c>READER_AUTH_CONFIG</c> của đơn vị (JSON cùng cấu trúc bên dưới, có hoặc không
/// bọc trong "ReaderAuth"); đơn vị không khai báo thì dùng mục <c>ReaderAuth</c> trong appsettings (mặc định Local = như cũ).
/// <code>
/// "ReaderAuth": {
///   "Provider": "Local" | "Ldap" | "Api",
///   "FallbackToLocal": true,          // hệ thống ngoài không kết nối được / sai mật khẩu → thử mật khẩu nội bộ
///   "MatchBy": "Cardno" | "Email",    // khớp tài khoản ngoài với hồ sơ bạn đọc trong thư viện
///   "Ldap": { "Host", "Port": 389, "UseSsl": false, "BindDn", "BindPassword", "SearchBase", "SearchFilter": "(uid={0})",
///             "CardNoAttribute": "uid", "EmailAttribute": "mail" },
///   "Api":  { "Url", "UsernameField": "username", "PasswordField": "password", "SuccessField": "success",
///             "CardNoField": "cardNo", "EmailField": "email", "ApiKeyHeader", "ApiKey", "TimeoutSeconds": 10 }
/// }
/// </code>
/// LDAP: dùng tài khoản dịch vụ (BindDn) tìm người dùng theo SearchFilter rồi bind lại bằng DN + mật khẩu bạn đọc. API: POST JSON
/// {username, password}, coi là thành công khi HTTP 2xx và SuccessField = true (hoặc không cấu hình SuccessField); trường lồng
/// nhau viết dạng "data.cardNo".
/// </summary>
public class ExternalReaderAuth(IConfiguration config, IHttpClientFactory httpClientFactory, ELIBAPIDbContext db, ILogger<ExternalReaderAuth> logger)
{
    public const string TenantConfigKey = "READER_AUTH_CONFIG";

    public enum Outcome { Success, InvalidCredentials, Unavailable }

    public sealed record Result(Outcome Outcome, string? CardNo = null, string? Email = null, string? Message = null);

    /// <summary>Cấu hình xác thực của 1 đơn vị.</summary>
    public sealed class Settings(IConfigurationSection section, string source)
    {
        internal IConfigurationSection Section { get; } = section;
        /// <summary>"tenant" (READER_AUTH_CONFIG của đơn vị), "appsettings" hoặc "invalid" (JSON đơn vị lỗi → coi như Local).</summary>
        public string Source { get; } = source;
        public string Provider => (Section["Provider"] ?? "Local").Trim();
        public bool IsExternal => Provider.Equals("Ldap", StringComparison.OrdinalIgnoreCase) || Provider.Equals("Api", StringComparison.OrdinalIgnoreCase);
        public bool FallbackToLocal => !bool.TryParse(Section["FallbackToLocal"], out var b) || b;
        public bool MatchByEmail => string.Equals(Section["MatchBy"], "Email", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Settings> ForTenantAsync(long? tenantId)
    {
        var raw = (await TenantParams.PlainAsync(db, TenantConfigKey, tenantId)).Trim();
        if (raw.Length == 0) return new Settings(config.GetSection("ReaderAuth"), "appsettings");
        try
        {
            // Trang Tham số dùng trình soạn thảo → giá trị có thể bị mã hoá HTML (&quot;). Chấp nhận JSON có hoặc không bọc "ReaderAuth".
            var json = System.Net.WebUtility.HtmlDecode(raw);
            using (var doc = JsonDocument.Parse(json))
                if (!doc.RootElement.TryGetProperty("ReaderAuth", out _)) json = "{\"ReaderAuth\":" + json + "}";
            var built = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
            return new Settings(built.GetSection("ReaderAuth"), "tenant");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Key} của đơn vị {TenantId} không phải JSON hợp lệ — dùng xác thực nội bộ", TenantConfigKey, tenantId);
            return new Settings(new ConfigurationBuilder().Build().GetSection("ReaderAuth"), "invalid");
        }
    }

    public async Task<Result> AuthenticateAsync(Settings settings, string login, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password)) return new Result(Outcome.InvalidCredentials);
        var provider = settings.Provider;
        try
        {
            return provider.Equals("Ldap", StringComparison.OrdinalIgnoreCase) ? await LdapAsync(settings.Section.GetSection("Ldap"), login.Trim(), password)
                : provider.Equals("Api", StringComparison.OrdinalIgnoreCase) ? await ApiAsync(settings.Section.GetSection("Api"), login.Trim(), password, ct)
                : new Result(Outcome.Unavailable, Message: "Chưa cấu hình xác thực ngoài (Provider = Local).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Xác thực bạn đọc qua {Provider} thất bại", provider);
            return new Result(Outcome.Unavailable, Message: $"Không kết nối được hệ thống xác thực {provider}: {ex.Message}");
        }
    }

    // ── LDAP ─────────────────────────────────────────────────────────────

    /// <summary>Thoát ký tự đặc biệt trong giá trị bộ lọc LDAP (RFC 4515) — chặn chèn bộ lọc qua tên đăng nhập.</summary>
    public static string EscapeFilter(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
            sb.Append(c switch { '\\' => @"\5c", '*' => @"\2a", '(' => @"\28", ')' => @"\29", '\0' => @"\00", _ => c.ToString() });
        return sb.ToString();
    }

    private static async Task<Result> LdapAsync(IConfigurationSection s, string login, string password)
    {
        var host = s["Host"];
        if (string.IsNullOrWhiteSpace(host)) return new Result(Outcome.Unavailable, Message: "Chưa cấu hình Ldap:Host.");
        var useSsl = bool.TryParse(s["UseSsl"], out var ssl) && ssl;
        var port = int.TryParse(s["Port"], out var p) ? p : useSsl ? 636 : 389;
        var cardAttr = s["CardNoAttribute"] ?? "uid";
        var mailAttr = s["EmailAttribute"] ?? "mail";
        var filter = (s["SearchFilter"] ?? "(uid={0})").Replace("{0}", EscapeFilter(login));

        using var conn = new LdapConnection { SecureSocketLayer = useSsl, ConnectionTimeout = 10000 };
        await conn.ConnectAsync(host, port);
        if (!string.IsNullOrEmpty(s["BindDn"])) await conn.BindAsync(s["BindDn"], s["BindPassword"] ?? "");

        var results = await conn.SearchAsync(s["SearchBase"] ?? "", LdapConnection.ScopeSub, filter, [cardAttr, mailAttr], false);
        LdapEntry? entry = null;
        while (await results.HasMoreAsync())
        {
            var next = await results.NextAsync();
            if (entry != null) return new Result(Outcome.InvalidCredentials, Message: "Tên đăng nhập khớp nhiều tài khoản LDAP.");
            entry = next;
        }
        if (entry == null) return new Result(Outcome.InvalidCredentials, Message: "Không có tài khoản LDAP này.");

        using var userConn = new LdapConnection { SecureSocketLayer = useSsl, ConnectionTimeout = 10000 };
        await userConn.ConnectAsync(host, port);
        try { await userConn.BindAsync(entry.Dn, password); }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            return new Result(Outcome.InvalidCredentials, Message: "Sai mật khẩu.");
        }
        var attrs = entry.GetAttributeSet();
        string? Attr(string name) => attrs.ContainsKey(name) ? attrs.GetAttribute(name).StringValue : null;
        return new Result(Outcome.Success, Attr(cardAttr) ?? login, Attr(mailAttr));
    }

    // ── API ──────────────────────────────────────────────────────────────

    private async Task<Result> ApiAsync(IConfigurationSection s, string login, string password, CancellationToken ct)
    {
        var url = s["Url"];
        if (string.IsNullOrWhiteSpace(url)) return new Result(Outcome.Unavailable, Message: "Chưa cấu hình Api:Url.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return new Result(Outcome.Unavailable, Message: "Api:Url phải là địa chỉ http(s) đầy đủ.");
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(int.TryParse(s["TimeoutSeconds"], out var t) ? Math.Clamp(t, 2, 60) : 10);
        using var req = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new Dictionary<string, string>
            {
                [s["UsernameField"] ?? "username"] = login, [s["PasswordField"] ?? "password"] = password,
            }),
        };
        if (!string.IsNullOrWhiteSpace(s["ApiKeyHeader"])) req.Headers.TryAddWithoutValidation(s["ApiKeyHeader"]!, s["ApiKey"] ?? "");
        using var res = await client.SendAsync(req, ct);
        if ((int)res.StatusCode is 401 or 403) return new Result(Outcome.InvalidCredentials, Message: "Sai tên đăng nhập hoặc mật khẩu.");
        if (!res.IsSuccessStatusCode) return new Result(Outcome.Unavailable, Message: $"API xác thực trả HTTP {(int)res.StatusCode}.");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var successField = s["SuccessField"];
        if (!string.IsNullOrWhiteSpace(successField))
        {
            var ok = Read(doc.RootElement, successField);
            if (ok is not ("true" or "True" or "1")) return new Result(Outcome.InvalidCredentials, Message: "Sai tên đăng nhập hoặc mật khẩu.");
        }
        return new Result(Outcome.Success, Read(doc.RootElement, s["CardNoField"] ?? "cardNo") ?? login, Read(doc.RootElement, s["EmailField"] ?? "email"));
    }

    /// <summary>Đọc trường JSON theo đường dẫn "a.b.c" (không phân biệt hoa thường), trả chuỗi hoặc null.</summary>
    public static string? Read(JsonElement root, string path)
    {
        var cur = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (cur.ValueKind != JsonValueKind.Object) return null;
            var prop = cur.EnumerateObject().FirstOrDefault(p => p.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (prop.Value.ValueKind == JsonValueKind.Undefined) return null;
            cur = prop.Value;
        }
        return cur.ValueKind switch
        {
            JsonValueKind.String => cur.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => cur.GetRawText(),
            _ => null,
        };
    }
}
