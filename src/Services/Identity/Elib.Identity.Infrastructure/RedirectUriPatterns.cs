using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace Elib.Identity.Infrastructure;

/// <summary>
/// Mẫu redirect URI cho app chạy trên tên miền của từng đơn vị: <c>https://*.thuvientn.vn/admin/callback</c>.
/// <c>*</c> chỉ khớp ĐÚNG MỘT nhãn DNS (a-z, 0-9, '-') ngay sau "scheme://" — không khớp '.', '@', ':' hay '/',
/// nên không thể lách sang tên miền khác (evil.com/..., x@evil.com, a.b.thuvientn.vn...). Phần còn lại so khớp nguyên văn.
/// </summary>
public static class RedirectUriPattern
{
    private const string Label = "[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?";

    public static Regex Compile(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var schemeEnd = pattern.IndexOf("://", StringComparison.Ordinal);
        var star = pattern.IndexOf('*', StringComparison.Ordinal);
        if (schemeEnd < 0 || star != schemeEnd + 3 || pattern.IndexOf('*', star + 1) >= 0 || pattern.Length <= star + 1 || pattern[star + 1] != '.')
            throw new InvalidOperationException($"Mẫu redirect URI '{pattern}' không hợp lệ: chỉ được một '*' đứng ngay sau '://' và trước '.' (vd https://*.thuvientn.vn/admin/callback).");

        var host = pattern[(star + 1)..].Split('/', 2)[0];
        if (host.Count(c => c == '.') < 2)
            throw new InvalidOperationException($"Mẫu redirect URI '{pattern}' quá rộng: tên miền sau '*' phải có ít nhất hai cấp.");

        var regex = "^" + Regex.Escape(pattern[..star]) + Label + Regex.Escape(pattern[(star + 1)..]) + "$";
        return new Regex(regex, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }

    public static bool Matches(IEnumerable<Regex> patterns, string uri) =>
        !string.IsNullOrEmpty(uri) && Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && string.IsNullOrEmpty(parsed.UserInfo)
        && patterns.Any(p => p.IsMatch(uri));
}

/// <summary>Application manager chấp nhận thêm các mẫu redirect URI khai báo trong Identity:Clients:{id}:RedirectUriPatterns.</summary>
public sealed class ElibApplicationManager<TApplication>(
    IOpenIddictApplicationCache<TApplication> cache,
    ILogger<OpenIddictApplicationManager<TApplication>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> coreOptions,
    IOpenIddictApplicationStore<TApplication> store,
    IOptions<IdentityServerOptions> identityOptions)
    : OpenIddictApplicationManager<TApplication>(cache, logger, coreOptions, store)
    where TApplication : class
{
    private readonly Dictionary<string, (Regex[] Redirect, Regex[] PostLogout)> _patterns =
        identityOptions.Value.Clients.ToDictionary(
            c => c.Key,
            c => (c.Value.RedirectUriPatterns.Select(RedirectUriPattern.Compile).ToArray(),
                  c.Value.PostLogoutRedirectUriPatterns.Select(RedirectUriPattern.Compile).ToArray()),
            StringComparer.Ordinal);

    public override async ValueTask<bool> ValidateRedirectUriAsync(TApplication application, string uri, CancellationToken cancellationToken = default)
    {
        if (await base.ValidateRedirectUriAsync(application, uri, cancellationToken)) return true;
        return _patterns.TryGetValue(await GetClientIdAsync(application, cancellationToken) ?? "", out var p) && RedirectUriPattern.Matches(p.Redirect, uri);
    }

    public override async ValueTask<bool> ValidatePostLogoutRedirectUriAsync(TApplication application, string uri, CancellationToken cancellationToken = default)
    {
        if (await base.ValidatePostLogoutRedirectUriAsync(application, uri, cancellationToken)) return true;
        return _patterns.TryGetValue(await GetClientIdAsync(application, cancellationToken) ?? "", out var p) && RedirectUriPattern.Matches(p.PostLogout, uri);
    }

    public override async IAsyncEnumerable<TApplication> FindByPostLogoutRedirectUriAsync(string uri, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var found = false;
        await foreach (var application in base.FindByPostLogoutRedirectUriAsync(uri, cancellationToken))
        {
            found = true;
            yield return application;
        }
        if (found) yield break;

        // Đăng xuất không kèm client_id: tìm client có mẫu khớp.
        foreach (var (clientId, p) in _patterns)
        {
            if (!RedirectUriPattern.Matches(p.PostLogout, uri)) continue;
            if (await FindByClientIdAsync(clientId, cancellationToken) is { } application) yield return application;
        }
    }
}
