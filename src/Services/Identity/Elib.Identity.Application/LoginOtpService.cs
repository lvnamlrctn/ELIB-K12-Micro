using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;

namespace Elib.Identity.Application;

/// <summary>Lớp bảo vệ thêm khi nhân viên đăng nhập — theo tham số ADMIN_LOGIN_CAPTCHA_ENABLED / ADMIN_LOGIN_OTP_ENABLED của đơn vị.</summary>
public sealed record LoginPolicy(bool Captcha, bool Otp)
{
    public static readonly LoginPolicy None = new(false, false);
}

/// <summary>
/// Chính sách đăng nhập: đơn vị → tham số của service tenant (cache, xoá khi SystemParameterChanged);
/// tenantId null (tài khoản hệ thống / host hệ thống) → cấu hình Identity:SystemLogin.
/// </summary>
public interface ILoginPolicySource
{
    Task<LoginPolicy> GetAsync(long? tenantId, CancellationToken cancellationToken);

    Task InvalidateAsync(long tenantId, CancellationToken cancellationToken);
}

public enum OtpError
{
    Expired,
    Invalid,
    TooManyAttempts,
    TooSoon,
    TooManySends,
}

public sealed record OtpPending(string MaskedEmail, int SecondsUntilResend);

/// <summary>Xác thực OTP thành công: người dùng để tải lại và nơi quay về.</summary>
public sealed record OtpPassed(long? TenantId, long UserId, string ReturnUrl);

/// <summary>
/// OTP qua email sau khi mật khẩu đúng (monolith: OtpService + AuthService). Mã 6 số, hạn 5 phút, tối đa 5 lần nhập sai,
/// gửi lại sau 60 giây, tối đa 3 lần gửi. Server chỉ giữ hash của mã; thư gửi qua service notification.
/// Tài khoản không có email thì không áp dụng OTP (quản trị đơn vị nhập email cho tài khoản để bật lớp bảo vệ này).
/// </summary>
public sealed class LoginOtpService(IDistributedCache cache, IPublishEndpoint publisher, IIdentityDb db, TimeProvider clock)
{
    public const int MaxAttempts = 5;
    public const int MaxSends = 3;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(60);

    private sealed record State(long? TenantId, long UserId, string Email, string FullName, string ReturnUrl,
        string CodeHash, int Attempts, int Sends, DateTimeOffset LastSentAt, DateTimeOffset ExpiresAt);

    public static bool Applies(SignInIdentity who) => !string.IsNullOrWhiteSpace(who.Email);

    /// <summary>Tạo thử thách OTP và gửi mã. Trả về mã định danh thử thách (đặt vào cookie của trình duyệt).</summary>
    public async Task<string> StartAsync(SignInIdentity who, string returnUrl, CancellationToken ct)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var now = clock.GetUtcNow();
        var state = new State(who.TenantId, who.UserId, who.Email!, who.FullName, returnUrl, "", 0, 0, now, now + Lifetime);
        await SendAsync(id, state, ct);
        return id;
    }

    public async Task<OtpPending?> GetAsync(string? id, CancellationToken ct) =>
        await LoadAsync(id, ct) is { } s ? Pending(s) : null;

    public async Task<(OtpPassed? Passed, OtpError? Error)> VerifyAsync(string? id, string? code, CancellationToken ct)
    {
        var state = await LoadAsync(id, ct);
        if (state is null) return (null, OtpError.Expired);
        var normalized = new string((code ?? "").Where(char.IsAsciiDigit).ToArray());
        if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(id!, normalized)), Encoding.ASCII.GetBytes(state.CodeHash)))
        {
            await cache.RemoveAsync(Key(id!), ct);
            return (new OtpPassed(state.TenantId, state.UserId, state.ReturnUrl), null);
        }

        var attempts = state.Attempts + 1;
        if (attempts >= MaxAttempts)
        {
            await cache.RemoveAsync(Key(id!), ct);
            return (null, OtpError.TooManyAttempts);
        }
        await SaveAsync(id!, state with { Attempts = attempts }, ct);
        return (null, OtpError.Invalid);
    }

    public async Task<OtpError?> ResendAsync(string? id, CancellationToken ct)
    {
        var state = await LoadAsync(id, ct);
        if (state is null) return OtpError.Expired;
        if (state.Sends >= MaxSends) return OtpError.TooManySends;
        if (clock.GetUtcNow() < state.LastSentAt + ResendAfter) return OtpError.TooSoon;
        await SendAsync(id!, state with { Attempts = 0 }, ct);
        return null;
    }

    private async Task SendAsync(string id, State state, CancellationToken ct)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var now = clock.GetUtcNow();
        var next = state with { CodeHash = Hash(id, code), Sends = state.Sends + 1, LastSentAt = now };
        await SaveAsync(id, next, ct);

        var recipient = new NotificationRecipient(null, state.UserId, state.Email, null, state.FullName);
        var data = new Dictionary<string, string>
        {
            ["otp"] = code,
            ["minutes"] = ((int)Math.Ceiling((state.ExpiresAt - now).TotalMinutes)).ToString(CultureInfo.InvariantCulture),
        };
        var actor = new EventActor(state.UserId, "staff");
        if (state.TenantId is { } tenantId)
        {
            await publisher.Publish(new NotificationRequested
            {
                TenantId = tenantId, Actor = actor, TemplateCode = "LOGIN_OTP", Channels = ["email"], Recipient = recipient, Data = data,
                DeduplicationKey = $"login-otp:{id}:{next.Sends}",
            }, ct);
        }
        else
        {
            await publisher.Publish(new SystemNotificationRequested { Actor = actor, TemplateCode = "LOGIN_OTP", Recipient = recipient, Data = data }, ct);
        }
        await db.SaveChangesAsync(ct); // đẩy outbox
    }

    private async Task<State?> LoadAsync(string? id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64) return null;
        var json = await cache.GetStringAsync(Key(id), ct);
        var state = json is null ? null : JsonSerializer.Deserialize<State>(json);
        return state is not null && clock.GetUtcNow() < state.ExpiresAt ? state : null;
    }

    private Task SaveAsync(string id, State state, CancellationToken ct) =>
        cache.SetStringAsync(Key(id), JsonSerializer.Serialize(state), new DistributedCacheEntryOptions { AbsoluteExpiration = state.ExpiresAt }, ct);

    private OtpPending Pending(State s) =>
        new(Mask(s.Email), Math.Max(0, (int)Math.Ceiling((s.LastSentAt + ResendAfter - clock.GetUtcNow()).TotalSeconds)));

    /// <summary>nguyenvana@truong.edu.vn → ng*******@truong.edu.vn</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0) return "***";
        var keep = Math.Min(2, at);
        return email[..keep] + new string('*', Math.Max(3, at - keep)) + email[at..];
    }

    private static string Hash(string id, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id + ":" + code)));

    public static string Key(string id) => "login-otp:" + id;
}
