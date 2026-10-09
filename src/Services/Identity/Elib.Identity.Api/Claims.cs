using System.Globalization;
using System.Security.Claims;
using Elib.BuildingBlocks.Tenancy;
using Elib.Identity.Application;
using Elib.Identity.Infrastructure;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Elib.Identity.Api;

/// <summary>Dựng principal cho cookie đăng nhập và cho token (docs 05 §1 — danh sách claim).</summary>
internal static class ElibPrincipals
{
    private const string TenantCodeClaim = "tenant_code";
    private const string TenantSubdomainClaim = "tenant_subdomain";

    /// <summary>Hạn phiên đóng vai đơn vị (Unix giây) — nằm trong cookie và refresh token, quá hạn thì không cấp token mới.</summary>
    private const string ImpersonationExpiresClaim = "imp_exp";

    /// <summary>Phiên đăng nhập trên trang identity: chỉ đủ để tải lại người dùng.</summary>
    public static ClaimsPrincipal ForLoginCookie(SignInIdentity who, DateTimeOffset? impersonationExpiresAt = null)
    {
        var identity = new ClaimsIdentity(IdentityCookie.Scheme, Claims.Name, Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, who.UserId.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(Claims.Name, who.FullName));
        AddImpersonation(identity, who, impersonationExpiresAt);
        if (who.TenantId is { } tenantId)
        {
            identity.AddClaim(new Claim(ElibClaimTypes.TenantId, tenantId.ToString(CultureInfo.InvariantCulture)));
            identity.AddClaim(new Claim(TenantCodeClaim, who.TenantCode ?? ""));
            identity.AddClaim(new Claim(TenantSubdomainClaim, who.TenantSubdomain ?? ""));
        }
        return new ClaimsPrincipal(identity);
    }

    public static (long? TenantId, long UserId)? ReadSession(ClaimsPrincipal principal)
    {
        if (!long.TryParse(principal.FindFirstValue(Claims.Subject), NumberStyles.None, CultureInfo.InvariantCulture, out var userId)) return null;
        var tenantRaw = principal.FindFirstValue(ElibClaimTypes.TenantId);
        if (string.IsNullOrEmpty(tenantRaw)) return (null, userId);
        return long.TryParse(tenantRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId) ? (tenantId, userId) : null;
    }

    /// <summary>Phiên hiện tại có thuộc đơn vị được yêu cầu (tham số "tenant" của authorize request) không.</summary>
    public static bool MatchesTenant(ClaimsPrincipal session, string? requestedTenant)
    {
        if (string.IsNullOrWhiteSpace(requestedTenant)) return true;
        var key = requestedTenant.Trim();
        return string.Equals(session.FindFirstValue(TenantCodeClaim), key, StringComparison.OrdinalIgnoreCase)
               || string.Equals(session.FindFirstValue(TenantSubdomainClaim), key.Split('.')[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Hạn của phiên đóng vai nếu phiên/token là đóng vai đơn vị; null = phiên thường.</summary>
    public static DateTimeOffset? ImpersonationExpiry(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ElibClaimTypes.Impersonation) is null
            ? null
            : long.TryParse(principal.FindFirstValue(ImpersonationExpiresClaim), NumberStyles.None, CultureInfo.InvariantCulture, out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : DateTimeOffset.MinValue; // có cờ đóng vai mà thiếu hạn = coi như đã hết hạn

    private static void AddImpersonation(ClaimsIdentity identity, SignInIdentity who, DateTimeOffset? expiresAt)
    {
        if (!who.ReadOnlyImpersonation) return;
        identity.SetClaim(ElibClaimTypes.Impersonation, ImpersonationModes.ReadOnly)
            .SetClaim(ImpersonationExpiresClaim, (expiresAt ?? DateTimeOffset.MinValue).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
    }

    public static ClaimsPrincipal ForUserToken(SignInIdentity who, IEnumerable<string> scopes, DateTimeOffset? impersonationExpiresAt = null)
    {
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, who.UserId.ToString(CultureInfo.InvariantCulture))
            .SetClaim(Claims.Name, who.FullName)
            .SetClaim(Claims.PreferredUsername, who.UserName)
            .SetClaim(ElibClaimTypes.SubjectType, ElibSubjectTypes.Staff)
            .SetClaim(ElibClaimTypes.PermissionStamp, who.PermissionStamp);
        if (who.TenantId is { } tenantId) identity.SetClaim(ElibClaimTypes.TenantId, tenantId.ToString(CultureInfo.InvariantCulture));
        AddImpersonation(identity, who, impersonationExpiresAt);
        return Finish(identity, scopes);
    }

    public static ClaimsPrincipal ForService(string clientId, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, clientId)
            .SetClaim(Claims.Name, clientId)
            .SetClaim(ElibClaimTypes.SubjectType, ElibSubjectTypes.Service);
        return Finish(identity, scopes);
    }

    private static ClaimsPrincipal Finish(ClaimsIdentity identity, IEnumerable<string> scopes)
    {
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(ElibScopes.Api);
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name or Claims.PreferredUsername when principal.HasScope(Scopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken],
        });
        return principal;
    }
}
