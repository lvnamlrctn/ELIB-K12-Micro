using System.Globalization;
using System.Net.Http.Json;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Application;
using MassTransit;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Identity.Infrastructure;

/// <summary>Lớp bảo vệ đăng nhập của tài khoản hệ thống (host hệ thống) — không có tham số đơn vị nên lấy từ cấu hình.</summary>
public sealed class SystemLoginOptions
{
    public bool CaptchaEnabled { get; set; }

    /// <summary>OTP gửi tới email của tài khoản hệ thống qua SMTP nền tảng; tài khoản không có email thì bỏ qua.</summary>
    public bool OtpEnabled { get; set; }
}

/// <summary>
/// Tham số đăng nhập của đơn vị đọc từ service tenant (GET /internal/tenants/{id}/parameters?service=identity, service token),
/// cache 5 phút, xoá ngay khi nhận SystemParameterChanged. Tenant không trả lời → bật CAPTCHA, không bật OTP, không cache.
/// </summary>
public sealed partial class TenantLoginPolicySource(
    IHttpClientFactory httpClients, HybridCache cache, IOptions<IdentityServerOptions> options, ILogger<TenantLoginPolicySource> logger)
    : ILoginPolicySource
{
    public const string HttpClientName = "elib-tenant";
    private const string CaptchaCode = "ADMIN_LOGIN_CAPTCHA_ENABLED";
    private const string OtpCode = "ADMIN_LOGIN_OTP_ENABLED";

    private static readonly HybridCacheEntryOptions Entry = new() { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(1) };

    public async Task<LoginPolicy> GetAsync(long? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId is null)
        {
            var system = options.Value.SystemLogin;
            return new LoginPolicy(system.CaptchaEnabled, system.OtpEnabled);
        }

        try
        {
            return await cache.GetOrCreateAsync(Key(tenantId.Value), (httpClients, tenantId.Value),
                static async (state, ct) => await LoadAsync(state.httpClients, state.Value, ct), Entry, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, ex, tenantId.Value);
            return new LoginPolicy(Captcha: true, Otp: false);
        }
    }

    public Task InvalidateAsync(long tenantId, CancellationToken cancellationToken) => cache.RemoveAsync(Key(tenantId), cancellationToken).AsTask();

    private static async Task<LoginPolicy> LoadAsync(IHttpClientFactory httpClients, long tenantId, CancellationToken ct)
    {
        var values = await httpClients.CreateClient(HttpClientName).GetFromJsonAsync<Dictionary<string, string?>>(
            new Uri($"internal/tenants/{tenantId.ToString(CultureInfo.InvariantCulture)}/parameters?service=identity", UriKind.Relative), ct)
            ?? [];
        return new LoginPolicy(IsOn(values.GetValueOrDefault(CaptchaCode)), IsOn(values.GetValueOrDefault(OtpCode)));
    }

    private static bool IsOn(string? value) =>
        value?.Trim() is { } v && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));

    private static string Key(long tenantId) => "login-policy:" + tenantId.ToString(CultureInfo.InvariantCulture);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không đọc được tham số đăng nhập của đơn vị {TenantId} — tạm bật CAPTCHA")]
    private static partial void LogUnavailable(ILogger logger, Exception exception, long tenantId);
}

/// <summary>Tham số của identity đổi ở service tenant → xoá cache chính sách đăng nhập của đơn vị đó.</summary>
public sealed class LoginPolicyChangedConsumer(ILoginPolicySource policies) : IConsumer<SystemParameterChanged>
{
    public Task Consume(ConsumeContext<SystemParameterChanged> context) =>
        context.Message.Service == "identity" ? policies.InvalidateAsync(context.Message.TenantId, context.CancellationToken) : Task.CompletedTask;
}
