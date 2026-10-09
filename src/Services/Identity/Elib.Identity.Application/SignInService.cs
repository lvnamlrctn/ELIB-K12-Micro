using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Identity.Application;

/// <summary>
/// Xác thực tài khoản. Không cho biết "sai tên đăng nhập" hay "sai mật khẩu"; khi không tìm thấy tài khoản vẫn
/// chạy BCrypt với hash giả để thời gian phản hồi không lộ tài khoản có tồn tại hay không.
/// </summary>
public sealed class SignInService(IIdentityDb db, ITenantContext tenant, IPasswordHasher hasher, TimeProvider clock, IdentityAudit audit)
{
    private static string? _dummyHash;

    public async Task<LoginResult> LoginAsync(string? tenantKey, string userName, string password, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        string normalized;
        try
        {
            normalized = AccountRules.NormalizeUserName(userName);
        }
        catch (BusinessRuleException)
        {
            return Fail(password, LoginError.InvalidCredentials);
        }

        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            using var system = tenant.UseSystem();
            var user = await db.SystemUsers.FirstOrDefaultAsync(u => u.UserName == normalized, ct);
            if (user is null) return Fail(password, LoginError.InvalidCredentials);
            var error = await CheckAsync(user.Credentials, user.IsActive, password, now, ct);
            return error is null
                ? new LoginResult(new SignInIdentity(user.Id, null, user.UserName, user.FullName, user.PermissionStamp, null, null, user.Credentials.MustChangePassword, Email: user.Email), null)
                : new LoginResult(null, error);
        }

        var replica = await FindTenantAsync(tenantKey, ct);
        if (replica is null || replica.Status != "Active") return Fail(password, LoginError.TenantUnavailable);

        using var scope = tenant.Use(replica.TenantId);
        var staff = await db.StaffUsers.FirstOrDefaultAsync(u => u.UserName == normalized, ct);
        if (staff is null) return Fail(password, LoginError.InvalidCredentials);
        var staffError = await CheckAsync(staff.Credentials, staff.IsActive, password, now, ct);
        return staffError is null
            ? new LoginResult(ToIdentity(staff, replica), null)
            : new LoginResult(null, staffError);
    }

    /// <summary>Tải lại người dùng khi phát/làm mới token: vẫn hoạt động, không bị khoá, đơn vị vẫn Active. Null = từ chối.</summary>
    public async Task<SignInIdentity?> ReloadAsync(long? tenantId, long userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (tenantId is null)
        {
            using var system = tenant.UseSystem();
            var user = await db.SystemUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            return user is { IsActive: true } && !user.Credentials.IsLockedOut(now)
                ? new SignInIdentity(user.Id, null, user.UserName, user.FullName, user.PermissionStamp, null, null, user.Credentials.MustChangePassword, Email: user.Email)
                : null;
        }

        var replica = await db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (replica is null || replica.Status != "Active") return null;

        using var scope = tenant.Use(tenantId.Value);
        var staff = await db.StaffUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return staff is { IsActive: true } && !staff.Credentials.IsLockedOut(now) ? ToIdentity(staff, replica) : null;
    }

    /// <summary>
    /// Người dùng tự đổi mật khẩu (kể cả khi bị buộc đổi sau lần đăng nhập đầu). Sai mật khẩu hiện tại tính vào số lần sai
    /// như khi đăng nhập — token bị lộ cũng không dùng được để dò mật khẩu.
    /// </summary>
    public async Task ChangePasswordAsync(long? tenantId, long userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        using var scope = tenantId is { } id ? tenant.Use(id) : tenant.UseSystem();
        var credentials = tenantId is null
            ? (await db.SystemUsers.FirstOrDefaultAsync(u => u.Id == userId, ct))?.Credentials
            : (await db.StaffUsers.FirstOrDefaultAsync(u => u.Id == userId, ct))?.Credentials;
        if (credentials is null) throw new NotFoundException("tài khoản", userId);

        if (credentials.IsLockedOut(now))
            throw new BusinessRuleException("ACCOUNT_LOCKED", "Tài khoản đang bị khoá tạm do nhập sai nhiều lần. Vui lòng thử lại sau.", 423);

        if (!hasher.Verify(currentPassword ?? "", credentials.PasswordHash))
        {
            credentials.RecordFailure(now);
            await db.SaveChangesAsync(ct);
            throw new BusinessRuleException("PASSWORD_INCORRECT", "Mật khẩu hiện tại không đúng.");
        }

        AccountRules.EnsureStrongPassword(newPassword);
        if (hasher.Verify(newPassword, credentials.PasswordHash))
            throw new BusinessRuleException("PASSWORD_REUSED", "Mật khẩu mới phải khác mật khẩu hiện tại.");

        credentials.SetPasswordHash(hasher.Hash(newPassword), mustChange: false);
        await audit.AccountAsync(tenantId, "PASSWORD_CHANGE", userId.ToString(System.Globalization.CultureInfo.InvariantCulture), "Tự đổi mật khẩu",
            new IdentityAudit.Who(userId, null), ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Nhật ký đăng nhập thành công (sau mật khẩu, hoặc sau OTP nếu có).</summary>
    public async Task RecordLoginAsync(SignInIdentity who, bool viaOtp, CancellationToken ct)
    {
        await audit.AccountAsync(who.TenantId, "LOGIN", who.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            viaOtp ? "Đăng nhập (mật khẩu + OTP)" : "Đăng nhập", IdentityAudit.Of(who), ct);
        await db.SaveChangesAsync(ct); // đẩy outbox
    }

    /// <summary>
    /// Nhật ký đăng nhập thất bại vào đơn vị đã xác định được (host hoặc mã đơn vị nhập); không rõ đơn vị → nhật ký nền tảng.
    /// Ghi tên đăng nhập đã nhập (không ghi mật khẩu) để phát hiện dò mật khẩu.
    /// </summary>
    public async Task RecordLoginFailedAsync(long? tenantId, string? userName, LoginError error, CancellationToken ct)
    {
        var reason = error switch
        {
            LoginError.LockedOut => "tài khoản đang bị khoá tạm",
            LoginError.AccountDisabled => "tài khoản bị vô hiệu hoá",
            LoginError.TenantUnavailable => "đơn vị không tồn tại hoặc tạm ngưng",
            _ => "sai tên đăng nhập hoặc mật khẩu",
        };
        var name = (userName ?? "").Trim();
        if (name.Length > 100) name = name[..100];
        await audit.AccountAsync(tenantId, "LOGIN_FAILED", null, $"Đăng nhập thất bại '{name}': {reason}", new IdentityAudit.Who(null, name), ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<TenantReplicaRecord?> FindTenantAsync(long tenantId, CancellationToken ct) =>
        db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);

    /// <summary>Tìm đơn vị theo mã ("TH-ABC") hoặc tên miền con ("truong-abc" / "truong-abc.thuvientn.vn").</summary>
    public Task<TenantReplicaRecord?> FindTenantAsync(string tenantKey, CancellationToken ct)
    {
        var key = tenantKey.Trim();
        var code = key.ToUpperInvariant();
        var subdomain = key.Split('.')[0].ToLowerInvariant();
        return db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code || t.Subdomain == subdomain, ct);
    }

    private async Task<LoginError?> CheckAsync(Credentials credentials, bool isActive, string password, DateTimeOffset now, CancellationToken ct)
    {
        if (credentials.IsLockedOut(now))
        {
            hasher.Verify(password, DummyHash());
            return LoginError.LockedOut;
        }

        if (!hasher.Verify(password, credentials.PasswordHash))
        {
            credentials.RecordFailure(now);
            await db.SaveChangesAsync(ct);
            return credentials.IsLockedOut(now) ? LoginError.LockedOut : LoginError.InvalidCredentials;
        }

        // Chỉ báo "bị vô hiệu hoá" khi mật khẩu đúng — không lộ trạng thái tài khoản cho người đoán mật khẩu.
        if (!isActive) return LoginError.AccountDisabled;

        credentials.RecordSuccess(now);
        await db.SaveChangesAsync(ct);
        return null;
    }

    private LoginResult Fail(string password, LoginError error)
    {
        hasher.Verify(password ?? "", DummyHash());
        return new LoginResult(null, error);
    }

    private string DummyHash() => _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString("N"));

    private static SignInIdentity ToIdentity(StaffUser u, TenantReplicaRecord t) =>
        new(u.Id, u.TenantId, u.UserName, u.FullName, u.PermissionStamp, t.Code, t.Subdomain, u.Credentials.MustChangePassword, Email: u.Email);
}
