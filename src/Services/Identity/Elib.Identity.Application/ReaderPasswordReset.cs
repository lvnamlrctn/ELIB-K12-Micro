using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Elib.Identity.Application;

/// <summary>
/// Bạn đọc quên mật khẩu OPAC (ELIB-LRC chỉ có nút, chưa làm): nhập số thẻ → thư có link đặt lại gửi tới email trong hồ sơ. Mã 32 byte ngẫu
/// nhiên, server chỉ giữ hash, hạn 30 phút, dùng một lần; mỗi thẻ tối đa một thư / 60 giây. Không cho biết số thẻ có tồn tại / có email hay
/// không (trang luôn trả cùng một câu). Đặt lại xong: hết bắt đổi mật khẩu, xoá khoá tạm, ghi nhật ký. Đơn vị chỉ dùng LDAP/API (không cho
/// mật khẩu thư viện) thì không gửi — bạn đọc đổi mật khẩu ở hệ thống của trường.
/// </summary>
public sealed class ReaderPasswordResetService(
    IIdentityDb db, ITenantContext tenant, IPasswordHasher hasher, IDistributedCache cache, IPublishEndpoint publisher, ILoginPolicySource policies,
    TimeProvider clock, IdentityAudit audit)
{
    public const string TemplateCode = "READER_PASSWORD_RESET";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(60);

    private sealed record State(long TenantId, long AccountId, DateTimeOffset ExpiresAt);

    /// <summary>Gửi thư nếu đủ điều kiện; luôn trả về bình thường. <paramref name="linkBase"/>: "https://host/account/reader-reset".</summary>
    public async Task RequestAsync(long tenantId, string? cardNo, string linkBase, CancellationToken ct)
    {
        var key = (cardNo ?? "").Trim();
        if (key.Length is 0 or > 100) return;
        if (ReaderAuthSettings.Parse((await policies.GetAsync(tenantId, ct)).ReaderAuth) is { IsExternal: true, FallbackToLocal: false }) return;
        using var scope = tenant.Use(tenantId);
        var cardKey = ReaderAccount.Key(key);
        var account = await db.ReaderAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.CardKey == cardKey && !a.Deleted, ct);
        if (account is not { Status: IHasStatus.Active } || string.IsNullOrWhiteSpace(account.Email)) return;

        var throttle = "reader-reset-sent:" + account.Id.ToString(CultureInfo.InvariantCulture);
        if (await cache.GetStringAsync(throttle, ct) is not null) return;
        await cache.SetStringAsync(throttle, "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ResendAfter }, ct);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expires = clock.GetUtcNow() + Lifetime;
        await cache.SetStringAsync(Key(token), JsonSerializer.Serialize(new State(tenantId, account.Id, expires)),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expires }, ct);
        await publisher.Publish(new NotificationRequested
        {
            TenantId = tenantId, Actor = new EventActor(account.Id, "reader"), TemplateCode = TemplateCode, Channels = ["email"],
            Recipient = new NotificationRecipient(null, null, account.Email, null, account.FullName),
            Data = new Dictionary<string, string>
            {
                ["link"] = linkBase + "?token=" + token,
                ["minutes"] = ((int)Lifetime.TotalMinutes).ToString(CultureInfo.InvariantCulture),
                ["card_no"] = account.CardNo,
            },
            DeduplicationKey = "reader-reset:" + Hash(token)[..16],
        }, ct);
        await audit.TenantAsync(tenantId, "READER_PASSWORD_RESET_REQUESTED", "Reader", account.ReaderPublicId.ToString(),
            $"Bạn đọc {account.CardNo} yêu cầu đặt lại mật khẩu OPAC qua email", ct, new IdentityAudit.Who(null, account.CardNo));
        await db.SaveChangesAsync(ct); // đẩy outbox
    }

    /// <summary>Mã còn hạn và đúng đơn vị của host → số thẻ (hiển thị trên trang đặt lại); ngược lại null.</summary>
    public async Task<string?> CheckAsync(long tenantId, string? token, CancellationToken ct)
    {
        if (await LoadAsync(tenantId, token, ct) is not { } state) return null;
        using var scope = tenant.Use(tenantId);
        return await db.ReaderAccounts.AsNoTracking().Where(a => a.Id == state.AccountId && !a.Deleted).Select(a => a.CardNo).FirstOrDefaultAsync(ct);
    }

    /// <summary>Đặt mật khẩu mới bằng mã (dùng một lần). Mã sai/hết hạn → BusinessRuleException READER_RESET_INVALID.</summary>
    public async Task ResetAsync(long tenantId, string? token, string? newPassword, CancellationToken ct)
    {
        var state = await LoadAsync(tenantId, token, ct)
            ?? throw new BusinessRuleException("READER_RESET_INVALID", "Link đặt lại mật khẩu không đúng hoặc đã hết hạn — gửi yêu cầu mới.");
        ReaderAccount.EnsurePasswordRules(newPassword);
        using var scope = tenant.Use(tenantId);
        var account = await db.ReaderAccounts.FirstOrDefaultAsync(a => a.Id == state.AccountId && !a.Deleted, ct)
            ?? throw new BusinessRuleException("READER_RESET_INVALID", "Link đặt lại mật khẩu không đúng hoặc đã hết hạn — gửi yêu cầu mới.");
        await cache.RemoveAsync(Key(token!), ct);
        account.ChangeOwnPassword(hasher.Hash(newPassword!));
        account.Credentials.Unlock();
        await audit.TenantAsync(tenantId, "READER_PASSWORD_RESET", "Reader", account.ReaderPublicId.ToString(),
            $"Bạn đọc {account.CardNo} đặt lại mật khẩu OPAC bằng link gửi qua email", ct, new IdentityAudit.Who(account.Id, $"{account.FullName} ({account.CardNo})", "reader"));
        await db.SaveChangesAsync(ct);
    }

    private async Task<State?> LoadAsync(long tenantId, string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64 || !token.All(char.IsAsciiHexDigitLower)) return null;
        var json = await cache.GetStringAsync(Key(token), ct);
        var state = json is null ? null : JsonSerializer.Deserialize<State>(json);
        return state is not null && state.TenantId == tenantId && clock.GetUtcNow() < state.ExpiresAt ? state : null;
    }

    private static string Key(string token) => "reader-reset:" + Hash(token);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
}
