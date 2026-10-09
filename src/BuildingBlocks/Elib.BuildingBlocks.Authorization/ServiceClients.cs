using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Authorization;

public sealed class ServiceClientOptions
{
    public const string SectionName = "ServiceClient";

    /// <summary>URL nội bộ của identity (ví dụ http://identity:8080) — dùng để lấy token và hỏi quyền.</summary>
    public string IdentityUrl { get; set; } = "";

    /// <summary>Client credentials của service này (đăng ký ở identity). Secret lấy từ biến môi trường.</summary>
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public string Scope { get; set; } = "elib-api";
}

/// <summary>Lấy và cache access token client_credentials của chính service (docs 05 §4).</summary>
public sealed class ServiceTokenProvider(IHttpClientFactory httpClients, IOptions<ServiceClientOptions> options, TimeProvider clock) : IDisposable
{
    public const string HttpClientName = "elib-identity-token";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && clock.GetUtcNow() < _expiresAt) return _token;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && clock.GetUtcNow() < _expiresAt) return _token;

            var o = options.Value;
            using var response = await httpClients.CreateClient(HttpClientName).PostAsync(
                new Uri(new Uri(o.IdentityUrl.TrimEnd('/') + "/"), "connect/token"),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = o.ClientId,
                    ["client_secret"] = o.ClientSecret,
                    ["scope"] = o.Scope,
                }),
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Identity trả về token rỗng.");

            _token = body.AccessToken;
            _expiresAt = clock.GetUtcNow().AddSeconds(Math.Max(30, body.ExpiresIn - 60)); // làm mới trước hạn 60 giây
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>Gắn service token vào mọi request của HttpClient gọi service nội bộ.</summary>
public sealed class ServiceTokenHandler(ServiceTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Hỏi quyền hiệu lực từ identity qua API nội bộ (thay cho gRPC trong docs 03 §4 ở giai đoạn đầu).</summary>
public sealed class HttpPermissionSource(IHttpClientFactory httpClients) : IPermissionSource
{
    public const string HttpClientName = "elib-identity";

    public async Task<IReadOnlySet<string>> GetEffectiveAsync(long? tenantId, long userId, string? permissionStamp, CancellationToken cancellationToken)
    {
        var query = $"internal/permissions?userId={userId.ToString(CultureInfo.InvariantCulture)}"
                    + (tenantId is { } t ? $"&tenantId={t.ToString(CultureInfo.InvariantCulture)}" : "")
                    + (permissionStamp is null ? "" : "&stamp=" + Uri.EscapeDataString(permissionStamp));
        var codes = await httpClients.CreateClient(HttpClientName)
            .GetFromJsonAsync<string[]>(new Uri(query, UriKind.Relative), cancellationToken);
        return new HashSet<string>(codes ?? [], StringComparer.Ordinal);
    }
}

public static class ServiceClientServiceCollectionExtensions
{
    /// <summary>
    /// Service token + HttpClient tới identity + <see cref="IPermissionSource"/> qua API nội bộ của identity.
    /// Cấu hình: ServiceClient:IdentityUrl, ServiceClient:ClientId, ServiceClient__ClientSecret (secret).
    /// </summary>
    public static IServiceCollection AddElibIdentityClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceClientOptions>(configuration.GetSection(ServiceClientOptions.SectionName));
        services.AddElibServiceTokens();
        services.AddHttpClient(HttpPermissionSource.HttpClientName, (sp, c) =>
            {
                c.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ServiceClientOptions>>().Value.IdentityUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddSingleton<IPermissionSource, HttpPermissionSource>();
        return services;
    }

    /// <summary>Chỉ phần lấy service token — dùng cho HttpClient gọi service nội bộ khác (gateway → tenant…).</summary>
    public static IServiceCollection AddElibServiceTokens(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient(ServiceTokenProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<ServiceTokenProvider>();
        services.AddTransient<ServiceTokenHandler>();
        return services;
    }
}
