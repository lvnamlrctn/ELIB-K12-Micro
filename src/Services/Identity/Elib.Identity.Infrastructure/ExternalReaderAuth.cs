using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Elib.Identity.Application;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace Elib.Identity.Infrastructure;

/// <summary>
/// Xác thực bạn đọc qua LDAP / API của trường (port từ ELIB-LRC ExternalReaderAuth, cấu hình theo đơn vị thay vì appsettings chung).
/// LDAP: tài khoản dịch vụ (BindDn) tìm người dùng theo SearchFilter rồi bind lại bằng DN + mật khẩu bạn đọc. API: POST JSON
/// {username, password}; thành công khi HTTP 2xx và SuccessField = true (hoặc không cấu hình SuccessField); trường lồng nhau "data.cardNo".
/// </summary>
public sealed partial class ExternalReaderAuth(IHttpClientFactory httpClients, ILogger<ExternalReaderAuth> logger) : IExternalReaderAuth
{
    public const string HttpClientName = "elib-reader-auth";

    public async Task<ExternalAuthResult> AuthenticateAsync(ReaderAuthSettings settings, string login, string password, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password)) return new ExternalAuthResult(ExternalAuthOutcome.InvalidCredentials);
        try
        {
            return settings.IsLdap ? await LdapAsync(settings.Ldap, login.Trim(), password)
                : settings.IsApi ? await ApiAsync(settings.Api, login.Trim(), password, ct)
                : new ExternalAuthResult(ExternalAuthOutcome.Unavailable, Message: "Chưa cấu hình xác thực ngoài (Provider = Local).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogFailed(logger, ex, settings.Provider);
            return new ExternalAuthResult(ExternalAuthOutcome.Unavailable, Message: $"Không kết nối được hệ thống xác thực {settings.Provider}: {ex.Message}");
        }
    }

    /// <summary>Thoát ký tự đặc biệt trong giá trị bộ lọc LDAP (RFC 4515) — chặn chèn bộ lọc qua tên đăng nhập.</summary>
    public static string EscapeFilter(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value ?? "")
            sb.Append(c switch { '\\' => @"\5c", '*' => @"\2a", '(' => @"\28", ')' => @"\29", '\0' => @"\00", _ => c.ToString() });
        return sb.ToString();
    }

    private static async Task<ExternalAuthResult> LdapAsync(ReaderAuthSettings.LdapSettings s, string login, string password)
    {
        if (string.IsNullOrWhiteSpace(s.Host)) return new ExternalAuthResult(ExternalAuthOutcome.Unavailable, Message: "Chưa cấu hình Ldap.Host.");
        var port = s.Port ?? (s.UseSsl ? 636 : 389);
        var filter = s.SearchFilter.Replace("{0}", EscapeFilter(login), StringComparison.Ordinal);

        using var conn = new LdapConnection { SecureSocketLayer = s.UseSsl, ConnectionTimeout = 10000 };
        await conn.ConnectAsync(s.Host, port);
        if (!string.IsNullOrEmpty(s.BindDn)) await conn.BindAsync(s.BindDn, s.BindPassword ?? "");
        var results = await conn.SearchAsync(s.SearchBase ?? "", LdapConnection.ScopeSub, filter, [s.CardNoAttribute, s.EmailAttribute], false);
        LdapEntry? entry = null;
        while (await results.HasMoreAsync())
        {
            var next = await results.NextAsync();
            if (entry is not null) return new ExternalAuthResult(ExternalAuthOutcome.InvalidCredentials, Message: "Tên đăng nhập khớp nhiều tài khoản LDAP.");
            entry = next;
        }
        if (entry is null) return new ExternalAuthResult(ExternalAuthOutcome.InvalidCredentials, Message: "Không có tài khoản LDAP này.");

        using var userConn = new LdapConnection { SecureSocketLayer = s.UseSsl, ConnectionTimeout = 10000 };
        await userConn.ConnectAsync(s.Host, port);
        try
        {
            await userConn.BindAsync(entry.Dn, password);
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            return new ExternalAuthResult(ExternalAuthOutcome.InvalidCredentials, Message: "Sai mật khẩu.");
        }
        var attrs = entry.GetAttributeSet();
        string? Attr(string name) => attrs.ContainsKey(name) ? attrs.GetAttribute(name).StringValue : null;
        return new ExternalAuthResult(ExternalAuthOutcome.Success, Attr(s.CardNoAttribute) ?? login, Attr(s.EmailAttribute));
    }

    private async Task<ExternalAuthResult> ApiAsync(ReaderAuthSettings.ApiSettings s, string login, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.Url) || !Uri.TryCreate(s.Url, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
            return new ExternalAuthResult(ExternalAuthOutcome.Unavailable, Message: "Chưa cấu hình Api.Url (http/https).");
        var client = httpClients.CreateClient(HttpClientName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(s.TimeoutSeconds, 2, 60)));
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new Dictionary<string, string> { [s.UsernameField] = login, [s.PasswordField] = password }),
        };
        if (!string.IsNullOrWhiteSpace(s.ApiKeyHeader)) request.Headers.TryAddWithoutValidation(s.ApiKeyHeader, s.ApiKey ?? "");
        using var response = await client.SendAsync(request, timeout.Token);
        if ((int)response.StatusCode is 401 or 403) return new ExternalAuthResult(ExternalAuthOutcome.InvalidCredentials, Message: "Sai tên đăng nhập hoặc mật khẩu.");
        if (!response.IsSuccessStatusCode) return new ExternalAuthResult(ExternalAuthOutcome.Unavailable, Message: $"API xác thực trả HTTP {(int)response.StatusCode}.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        if (!string.IsNullOrWhiteSpace(s.SuccessField) && Read(doc.RootElement, s.SuccessField) is not ("true" or "True" or "1"))
            return new ExternalAuthResult(ExternalAuthOutcome.InvalidCredentials, Message: "Sai tên đăng nhập hoặc mật khẩu.");
        return new ExternalAuthResult(ExternalAuthOutcome.Success, Read(doc.RootElement, s.CardNoField) ?? login, Read(doc.RootElement, s.EmailField));
    }

    /// <summary>Đọc trường JSON theo đường dẫn "a.b.c" (không phân biệt hoa thường), trả chuỗi hoặc null.</summary>
    public static string? Read(JsonElement root, string path)
    {
        var current = root;
        foreach (var part in (path ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            var prop = current.EnumerateObject().FirstOrDefault(p => p.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (prop.Value.ValueKind == JsonValueKind.Undefined) return null;
            current = prop.Value;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => current.GetRawText(),
            _ => null,
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Xác thực bạn đọc qua {Provider} thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception, string provider);
}
