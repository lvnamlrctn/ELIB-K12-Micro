using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Identity.Application;

/// <summary>Bạn đọc đã xác thực — nguồn để dựng cookie đăng nhập và access token của OPAC.</summary>
/// <summary><see cref="External"/>: phiên đăng nhập bằng tài khoản LDAP/API của trường — không dùng/không bắt đổi mật khẩu thư viện.</summary>
public sealed record ReaderIdentity(long AccountId, long TenantId, Guid ReaderPublicId, string CardNo, string FullName, bool MustChangePassword,
    bool External = false);

public sealed record ReaderLoginResult(ReaderIdentity? Identity, LoginError? Error)
{
    public bool Succeeded => Identity is not null;
}

/// <summary>Thông tin tài khoản cho trang "Tài khoản" của OPAC — không có mật khẩu/chuỗi băm.</summary>
public sealed record ReaderMeDto(Guid ReaderPublicId, string CardNo, string FullName, string? Email, DateOnly? ExpireDate,
    bool MustChangePassword, DateTimeOffset? LastLoginAt);

/// <summary>Kiểm tra mật khẩu bạn đọc ở trạm tự phục vụ (SIP2, trường AD) — circulation gọi qua /internal.</summary>
public sealed record ReaderPasswordCheckRequest(string? CardNo, string? Password);

/// <summary>valid | invalid | no-patron | locked-out — không trả thông tin nào khác về tài khoản.</summary>
public sealed record ReaderPasswordCheckResult(string Result)
{
    public const string Valid = "valid";
    public const string Invalid = "invalid";
    public const string NoPatron = "no-patron";
    public const string LockedOut = "locked-out";
}

public sealed record ReaderChangePasswordRequest(string? CurrentPassword, string NewPassword);

/// <summary>
/// Đăng nhập OPAC của bạn đọc (monolith: ReaderAuthService, nhánh mật khẩu nội bộ) bằng số thẻ hoặc email, chỉ trong đơn vị của host.
/// Như nhân viên: không cho biết sai số thẻ hay sai mật khẩu, khoá tạm 15 phút sau 5 lần sai, chỉ báo "thẻ bị khoá" khi mật khẩu đúng.
/// LDAP/API ngoài (READER_AUTH_CONFIG) chưa làm.
/// </summary>
public sealed class ReaderSignInService(IIdentityDb db, ITenantContext tenant, IPasswordHasher hasher, TimeProvider clock, IdentityAudit audit,
    ILoginPolicySource policies, IExternalReaderAuth external)
{
    private static string? _dummyHash;

    public async Task<ReaderLoginResult> LoginAsync(long tenantId, string? login, string? password, CancellationToken ct)
    {
        password ??= "";
        var key = (login ?? "").Trim();
        var replica = await db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (replica is null || replica.Status != "Active") return Fail(password, LoginError.TenantUnavailable);

        using var scope = tenant.Use(tenantId);
        var now = clock.GetUtcNow();

        // Xác thực qua LDAP/API của trường (READER_AUTH_CONFIG). Thành công → khớp hồ sơ theo số thẻ (hoặc email) hệ thống ngoài trả về;
        // sai / không kết nối được → tuỳ FallbackToLocal mà thử mật khẩu thư viện (như ELIB-LRC).
        if (ReaderAuthSettings.Parse((await policies.GetAsync(tenantId, ct)).ReaderAuth) is { IsExternal: true } settings && key.Length > 0)
        {
            var ext = await external.AuthenticateAsync(settings, key, password, ct);
            if (ext.Outcome == ExternalAuthOutcome.Success)
                return await ExternalLoginAsync(tenantId, key, settings, ext, now, ct);
            if (!settings.FallbackToLocal)
            {
                if (await FindAsync(key, ct) is { } failed)
                {
                    failed.Credentials.RecordFailure(now);
                    await db.SaveChangesAsync(ct);
                }
                await RecordFailedAsync(tenantId, key, ext.Outcome == ExternalAuthOutcome.Unavailable
                    ? $"không kết nối được {settings.Provider}" : $"sai tài khoản {settings.Provider}", ct);
                return new ReaderLoginResult(null, ext.Outcome == ExternalAuthOutcome.Unavailable ? LoginError.ExternalUnavailable : LoginError.InvalidCredentials);
            }
        }

        var account = key.Length == 0 ? null : await FindAsync(key, ct);
        if (account is null)
        {
            await RecordFailedAsync(tenantId, key, "sai số thẻ hoặc mật khẩu", ct);
            return Fail(password, LoginError.InvalidCredentials);
        }
        if (account.Credentials.IsLockedOut(now))
        {
            hasher.Verify(password, DummyHash());
            await RecordFailedAsync(tenantId, key, "tài khoản đang bị khoá tạm", ct);
            return new ReaderLoginResult(null, LoginError.LockedOut);
        }
        if (!account.VerifyPassword(password, hasher.Verify))
        {
            if (!account.CardNoPassword && account.Credentials.PasswordHash.Length == 0) hasher.Verify(password, DummyHash());
            account.Credentials.RecordFailure(now);
            await RecordFailedAsync(tenantId, key, "sai số thẻ hoặc mật khẩu", ct);
            return new ReaderLoginResult(null, account.Credentials.IsLockedOut(now) ? LoginError.LockedOut : LoginError.InvalidCredentials);
        }
        // Chỉ báo thẻ bị khoá khi mật khẩu đúng — không lộ trạng thái cho người đoán mật khẩu.
        if (account.Status != IHasStatus.Active)
        {
            await RecordFailedAsync(tenantId, key, "thẻ bạn đọc đang bị khoá", ct);
            return new ReaderLoginResult(null, LoginError.AccountDisabled);
        }

        account.Credentials.RecordSuccess(now);
        var who = ToIdentity(account);
        await audit.TenantAsync(tenantId, "READER_LOGIN", "Reader", account.ReaderPublicId.ToString(), $"Bạn đọc đăng nhập OPAC: {account.CardNo} — {account.FullName}", ct,
            Who(who));
        await db.SaveChangesAsync(ct);
        return new ReaderLoginResult(who, null);
    }

    /// <summary>Đúng tài khoản ngoài → hồ sơ bạn đọc khớp số thẻ/email; thẻ phải đang hoạt động; khoá tạm (sai nhiều lần) vẫn áp dụng.</summary>
    private async Task<ReaderLoginResult> ExternalLoginAsync(long tenantId, string login, ReaderAuthSettings settings, ExternalAuthResult ext,
        DateTimeOffset now, CancellationToken ct)
    {
        var match = (settings.MatchByEmail ? ext.Email : ext.CardNo ?? login)?.Trim() ?? "";
        ReaderAccount? account = null;
        if (match.Length > 0)
        {
            if (settings.MatchByEmail)
            {
                var email = match.ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang SQL lower().
                account = await db.ReaderAccounts.FirstOrDefaultAsync(a => !a.Deleted && a.Email != null && a.Email.ToLower() == email, ct);
#pragma warning restore CA1862, CA1304, CA1311
            }
            else
            {
                var cardKey = ReaderAccount.Key(match);
                account = await db.ReaderAccounts.FirstOrDefaultAsync(a => a.CardKey == cardKey && !a.Deleted, ct);
            }
        }
        if (account is null)
        {
            await RecordFailedAsync(tenantId, login, $"đúng tài khoản {settings.Provider} nhưng chưa có hồ sơ bạn đọc khớp", ct);
            return new ReaderLoginResult(null, LoginError.NoReaderProfile);
        }
        if (account.Credentials.IsLockedOut(now))
        {
            await RecordFailedAsync(tenantId, login, "tài khoản đang bị khoá tạm", ct);
            return new ReaderLoginResult(null, LoginError.LockedOut);
        }
        if (account.Status != IHasStatus.Active)
        {
            await RecordFailedAsync(tenantId, login, "thẻ bạn đọc đang bị khoá", ct);
            return new ReaderLoginResult(null, LoginError.AccountDisabled);
        }
        account.Credentials.RecordSuccess(now);
        var who = ToIdentity(account) with { MustChangePassword = false, External = true };
        await audit.TenantAsync(tenantId, "READER_LOGIN", "Reader", account.ReaderPublicId.ToString(),
            $"Bạn đọc đăng nhập OPAC qua {settings.Provider}: {account.CardNo} — {account.FullName}", ct, Who(who));
        await db.SaveChangesAsync(ct);
        return new ReaderLoginResult(who, null);
    }

    /// <summary>
    /// Mật khẩu bạn đọc nhập ở trạm tự phục vụ (SIP2). Cùng luật với đăng nhập OPAC: sai nhiều lần thì khoá tạm, ghi nhật ký lần sai;
    /// đúng thì xoá đếm sai. Không kiểm tra trạng thái thẻ (circulation tự chặn mượn khi thẻ khoá).
    /// </summary>
    public async Task<ReaderPasswordCheckResult> CheckAsync(long tenantId, string? cardNo, string? password, CancellationToken ct)
    {
        password ??= "";
        var key = (cardNo ?? "").Trim();
        using var scope = tenant.Use(tenantId);
        var account = key.Length == 0 ? null : await FindAsync(key, ct);
        var now = clock.GetUtcNow();
        if (account is null)
        {
            hasher.Verify(password, DummyHash());
            return new ReaderPasswordCheckResult(ReaderPasswordCheckResult.NoPatron);
        }
        if (account.Credentials.IsLockedOut(now))
        {
            hasher.Verify(password, DummyHash());
            return new ReaderPasswordCheckResult(ReaderPasswordCheckResult.LockedOut);
        }
        if (!account.VerifyPassword(password, hasher.Verify))
        {
            if (!account.CardNoPassword && account.Credentials.PasswordHash.Length == 0) hasher.Verify(password, DummyHash());
            account.Credentials.RecordFailure(now);
            var name = key.Length > 100 ? key[..100] : key;
            await audit.TenantAsync(tenantId, "READER_LOGIN_FAILED", "Reader", account.ReaderPublicId.ToString(),
                $"Bạn đọc nhập sai mật khẩu ở trạm tự phục vụ (SIP2) '{name}'", ct, new IdentityAudit.Who(null, name));
            await db.SaveChangesAsync(ct);
            return new ReaderPasswordCheckResult(account.Credentials.IsLockedOut(now) ? ReaderPasswordCheckResult.LockedOut : ReaderPasswordCheckResult.Invalid);
        }
        account.Credentials.RecordSuccess(now);
        await db.SaveChangesAsync(ct);
        return new ReaderPasswordCheckResult(ReaderPasswordCheckResult.Valid);
    }

    /// <summary>
    /// Tải lại bạn đọc khi phát/làm mới token: còn hồ sơ, thẻ đang hoạt động, đơn vị Active. Null = từ chối. <paramref name="external"/>: phiên
    /// đăng nhập qua LDAP/API — chỉ còn hiệu lực khi đơn vị vẫn bật xác thực ngoài; không cần mật khẩu thư viện, không bắt đổi mật khẩu.
    /// </summary>
    public async Task<ReaderIdentity?> ReloadAsync(long tenantId, long accountId, CancellationToken ct, bool external = false)
    {
        var replica = await db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (replica is null || replica.Status != "Active") return null;
        if (external && ReaderAuthSettings.Parse((await policies.GetAsync(tenantId, ct)).ReaderAuth) is not { IsExternal: true }) return null;
        using var scope = tenant.Use(tenantId);
        var account = await db.ReaderAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is not { Deleted: false, Status: IHasStatus.Active } || account.Credentials.IsLockedOut(clock.GetUtcNow())) return null;
        if (external) return ToIdentity(account) with { MustChangePassword = false, External = true };
        return account.HasPassword ? ToIdentity(account) : null;
    }

    /// <summary>"Kiểm tra kết nối" xác thực ngoài với cấu hình đang lưu (ELIB-LRC: ReaderAuthTest) — không đăng nhập, không đếm sai.</summary>
    public async Task<ReaderAuthTestResult> TestExternalAsync(long tenantId, string? login, string? password, CancellationToken ct)
    {
        var settings = ReaderAuthSettings.Parse((await policies.GetAsync(tenantId, ct)).ReaderAuth);
        if (settings is not { IsExternal: true })
            return new ReaderAuthTestResult(settings?.Provider ?? "Local", "NotConfigured", null, null, false,
                "Chưa bật xác thực ngoài — tham số READER_AUTH_CONFIG trống, sai JSON, hoặc Provider = Local.");
        var ext = await external.AuthenticateAsync(settings, (login ?? "").Trim(), password ?? "", ct);
        var found = false;
        if (ext.Outcome == ExternalAuthOutcome.Success)
        {
            using var scope = tenant.Use(tenantId);
            var match = (settings.MatchByEmail ? ext.Email : ext.CardNo)?.Trim() ?? "";
            var cardKey = ReaderAccount.Key(match);
            var email = match.ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            found = match.Length > 0 && await db.ReaderAccounts.AnyAsync(a => !a.Deleted
                && (settings.MatchByEmail ? a.Email != null && a.Email.ToLower() == email : a.CardKey == cardKey), ct);
#pragma warning restore CA1862, CA1304, CA1311
        }
        return new ReaderAuthTestResult(settings.Provider, ext.Outcome.ToString(), ext.CardNo, ext.Email, found, ext.Message);
    }

    /// <summary>Email trong hồ sơ bạn đọc (gửi OTP đăng nhập) — null nếu chưa có.</summary>
    public async Task<string?> EmailAsync(long tenantId, long accountId, CancellationToken ct)
    {
        using var scope = tenant.Use(tenantId);
        var email = await db.ReaderAccounts.AsNoTracking().Where(a => a.Id == accountId && !a.Deleted).Select(a => a.Email).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    public async Task<ReaderMeDto> MeAsync(long tenantId, long accountId, CancellationToken ct)
    {
        using var scope = tenant.Use(tenantId);
        var a = await db.ReaderAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == accountId && !x.Deleted, ct)
            ?? throw new NotFoundException("tài khoản bạn đọc", accountId);
        return new ReaderMeDto(a.ReaderPublicId, a.CardNo, a.FullName, a.Email, a.ExpireDate, a.Credentials.MustChangePassword, a.Credentials.LastLoginAt);
    }

    /// <summary>
    /// Bạn đọc tự đổi mật khẩu. <paramref name="requireCurrent"/> = false chỉ dùng ngay sau khi vừa đăng nhập đúng (bước bắt đổi lần đầu).
    /// Sai mật khẩu hiện tại tính vào số lần sai như khi đăng nhập.
    /// </summary>
    public async Task ChangePasswordAsync(long tenantId, long accountId, string? currentPassword, string? newPassword, bool requireCurrent, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        using var scope = tenant.Use(tenantId);
        var account = await db.ReaderAccounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.Deleted, ct)
            ?? throw new NotFoundException("tài khoản bạn đọc", accountId);
        if (account.Credentials.IsLockedOut(now))
            throw new BusinessRuleException("ACCOUNT_LOCKED", "Tài khoản đang bị khoá tạm do nhập sai nhiều lần. Vui lòng thử lại sau.", 423);
        if (requireCurrent && !account.VerifyPassword(currentPassword ?? "", hasher.Verify))
        {
            account.Credentials.RecordFailure(now);
            await db.SaveChangesAsync(ct);
            throw new BusinessRuleException("PASSWORD_INCORRECT", "Mật khẩu hiện tại không đúng.");
        }

        ReaderAccount.EnsurePasswordRules(newPassword);
        if (ReaderAccount.Key(newPassword) == account.CardKey)
            throw new BusinessRuleException("READER_PASSWORD_IS_CARDNO", "Mật khẩu mới không được trùng số thẻ.");
        if (account.VerifyPassword(newPassword!, hasher.Verify))
            throw new BusinessRuleException("PASSWORD_REUSED", "Mật khẩu mới phải khác mật khẩu hiện tại.");

        account.ChangeOwnPassword(hasher.Hash(newPassword!));
        await audit.TenantAsync(tenantId, "READER_PASSWORD_CHANGE", "Reader", account.ReaderPublicId.ToString(),
            $"Bạn đọc tự đổi mật khẩu OPAC: {account.CardNo}", ct, Who(ToIdentity(account)));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Số thẻ (không phân biệt hoa/thường), hoặc email nếu chỉ một bạn đọc dùng email đó.</summary>
    private async Task<ReaderAccount?> FindAsync(string key, CancellationToken ct)
    {
        var cardKey = ReaderAccount.Key(key);
        var byCard = await db.ReaderAccounts.FirstOrDefaultAsync(a => a.CardKey == cardKey && !a.Deleted, ct);
        if (byCard is not null || !key.Contains('@', StringComparison.Ordinal)) return byCard;
        var email = key.ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang SQL lower().
        var byEmail = await db.ReaderAccounts.Where(a => a.Email != null && a.Email.ToLower() == email && !a.Deleted).Take(2).ToListAsync(ct);
#pragma warning restore CA1862, CA1304, CA1311
        return byEmail.Count == 1 ? byEmail[0] : null;
    }

    /// <summary>Nhật ký đăng nhập thất bại (ghi số thẻ/email đã nhập, không ghi mật khẩu) — kèm lưu số lần sai.</summary>
    private async Task RecordFailedAsync(long tenantId, string login, string reason, CancellationToken ct)
    {
        var name = login.Length > 100 ? login[..100] : login;
        await audit.TenantAsync(tenantId, "READER_LOGIN_FAILED", "Reader", null, $"Bạn đọc đăng nhập OPAC thất bại '{name}': {reason}", ct,
            new IdentityAudit.Who(null, name));
        await db.SaveChangesAsync(ct);
    }

    private ReaderLoginResult Fail(string password, LoginError error)
    {
        hasher.Verify(password, DummyHash());
        return new ReaderLoginResult(null, error);
    }

    private string DummyHash() => _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString("N"));

    private static IdentityAudit.Who Who(ReaderIdentity r) => new(r.AccountId, $"{r.FullName} ({r.CardNo})", ElibSubjectTypes.Reader);

    private static ReaderIdentity ToIdentity(ReaderAccount a) =>
        new(a.Id, a.TenantId, a.ReaderPublicId, a.CardNo, a.FullName, a.Credentials.MustChangePassword);
}
