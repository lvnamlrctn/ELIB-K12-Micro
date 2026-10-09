using System.Security.Claims;
using System.Text.Encodings.Web;
using Elib.BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Testing;

/// <summary>
/// Xác thực giả thay cho JWT trong test: header <c>X-Test-Claims: sub=1;sub_type=staff;tenant_id=3</c>.
/// Không có header = ẩn danh.
/// </summary>
public sealed class HeaderAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string HeaderName = "X-Test-Claims";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var raw)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = raw.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Select(kv => new Claim(kv[0], kv[1]));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

public static class TestAuthExtensions
{
    /// <summary>Đặt <see cref="HeaderAuthHandler"/> làm scheme mặc định (ghi đè JWT của AddElibAuth).</summary>
    public static IServiceCollection AddHeaderTestAuth(this IServiceCollection services)
    {
        services.AddAuthentication(o =>
            {
                o.DefaultScheme = HeaderAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = HeaderAuthHandler.SchemeName;
                o.DefaultChallengeScheme = HeaderAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.SchemeName, null);
        return services;
    }

    public static HttpClient WithClaims(this HttpClient client, string claims)
    {
        client.DefaultRequestHeaders.Remove(HeaderAuthHandler.HeaderName);
        client.DefaultRequestHeaders.Add(HeaderAuthHandler.HeaderName, claims);
        return client;
    }
}

/// <summary>Quyền giả: tập quyền theo userId, đếm số lần gọi để kiểm tra cache.</summary>
public sealed class FakePermissionSource : IPermissionSource
{
    private int _calls;

    public Dictionary<long, string[]> Grants { get; } = [];
    public bool Throw { get; set; }
    public int Calls => _calls;

    public Task<IReadOnlySet<string>> GetEffectiveAsync(long? tenantId, long userId, string? permissionStamp, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (Throw) throw new HttpRequestException("identity down");
        return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(Grants.GetValueOrDefault(userId) ?? []));
    }
}

public sealed class FakeLicenseSource : IModuleLicenseSource
{
    public HashSet<(long TenantId, string Module)> Licensed { get; } = [];

    public Task<bool> IsLicensedAsync(long tenantId, string moduleCode, CancellationToken cancellationToken)
        => Task.FromResult(Licensed.Contains((tenantId, moduleCode)));
}

/// <summary>Chuỗi claim mẫu cho các vai trò thường gặp.</summary>
public static class TestClaims
{
    public const string SuperAdmin = "sub=1;sub_type=staff";
    public const string Service = "sub=2;sub_type=service";
    public static string Staff(long tenantId, long userId = 10) => $"sub={userId};sub_type=staff;tenant_id={tenantId}";
    public static string Reader(long tenantId, long readerId = 20) => $"sub={readerId};sub_type=reader;tenant_id={tenantId}";
}
