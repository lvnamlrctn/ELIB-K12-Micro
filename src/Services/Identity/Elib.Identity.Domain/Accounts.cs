using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Identity.Domain;

/// <summary>Thông tin đăng nhập + chống dò mật khẩu — dùng chung cho tài khoản nhân viên và tài khoản hệ thống.</summary>
[ComplexType]
public sealed class Credentials
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public string PasswordHash { get; private set; } = "";
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool MustChangePassword { get; private set; }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    /// <summary>Sai mật khẩu: đủ <see cref="MaxFailedAttempts"/> lần thì khoá tạm <see cref="LockoutDuration"/>.</summary>
    public void RecordFailure(DateTimeOffset now)
    {
        AccessFailedCount++;
        if (AccessFailedCount < MaxFailedAttempts) return;
        LockoutEnd = now + LockoutDuration;
        AccessFailedCount = 0;
    }

    public void RecordSuccess(DateTimeOffset now)
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
        LastLoginAt = now;
    }

    public void SetPasswordHash(string hash, bool mustChange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        PasswordHash = hash;
        MustChangePassword = mustChange;
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    public void Unlock()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }
}

public static partial class AccountRules
{
    public static string NormalizeUserName(string userName)
    {
        var value = (userName ?? "").Trim().ToLowerInvariant();
        return UserNamePattern().IsMatch(value)
            ? value
            : throw new BusinessRuleException("USERNAME_INVALID", "Tên đăng nhập gồm 3–64 ký tự a-z, 0-9, '.', '_', '-', '@'.");
    }

    /// <summary>Tối thiểu 8 ký tự, có cả chữ và số (docs 05 §1).</summary>
    public static void EnsureStrongPassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 128
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new BusinessRuleException("PASSWORD_WEAK", "Mật khẩu tối thiểu 8 ký tự, gồm cả chữ và số.");
    }

    public static string RequireName(string fullName) =>
        string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 200
            ? throw new BusinessRuleException("FULLNAME_INVALID", "Họ tên bắt buộc, tối đa 200 ký tự.")
            : fullName.Trim();

    public static string NewStamp() => Guid.NewGuid().ToString("N");

    [GeneratedRegex("^[a-z0-9._@-]{3,64}$")]
    private static partial Regex UserNamePattern();
}

/// <summary>Tài khoản nhân viên của một đơn vị. Thuộc đơn vị (query filter + RLS).</summary>
public sealed class StaffUser : TenantEntity
{
    private readonly List<Role> _roles = [];

    private StaffUser() { }

    public static StaffUser Create(string userName, string fullName, string? email, string? phone, string passwordHash, bool mustChangePassword)
    {
        var user = new StaffUser
        {
            UserName = AccountRules.NormalizeUserName(userName),
            FullName = AccountRules.RequireName(fullName),
            Email = email?.Trim(),
            Phone = phone?.Trim(),
            IsActive = true,
            PermissionStamp = AccountRules.NewStamp(),
        };
        user.Credentials.SetPasswordHash(passwordHash, mustChangePassword);
        return user;
    }

    public string UserName { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Đổi mỗi khi quyền/trạng thái đổi — token mang stamp cũ phải lấy lại quyền (docs 05 §2).</summary>
    public string PermissionStamp { get; private set; } = "";

    public Credentials Credentials { get; private set; } = new();
    public IReadOnlyList<Role> Roles => _roles;

    public void Update(string fullName, string? email, string? phone)
    {
        FullName = AccountRules.RequireName(fullName);
        Email = email?.Trim();
        Phone = phone?.Trim();
    }

    public void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        PermissionStamp = AccountRules.NewStamp();
    }

    public void SetRoles(IEnumerable<Role> roles)
    {
        _roles.Clear();
        _roles.AddRange(roles.DistinctBy(r => r.Id));
        PermissionStamp = AccountRules.NewStamp();
    }

    public IReadOnlySet<string> EffectivePermissions() =>
        _roles.Where(r => !r.IsDeleted).SelectMany(r => r.Permissions).ToHashSet(StringComparer.Ordinal);
}

/// <summary>Tài khoản quản trị nền tảng (không thuộc đơn vị). Toàn quyền ở ngữ cảnh hệ thống.</summary>
public sealed class SystemUser : AuditableEntity
{
    private SystemUser() { }

    public static SystemUser Create(string userName, string fullName, string? email, string passwordHash, bool mustChangePassword)
    {
        var user = new SystemUser
        {
            UserName = AccountRules.NormalizeUserName(userName),
            FullName = AccountRules.RequireName(fullName),
            Email = email?.Trim(),
            IsActive = true,
            PermissionStamp = AccountRules.NewStamp(),
        };
        user.Credentials.SetPasswordHash(passwordHash, mustChangePassword);
        return user;
    }

    public string UserName { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
    public string PermissionStamp { get; private set; } = "";
    public Credentials Credentials { get; private set; } = new();

    public void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;
        PermissionStamp = AccountRules.NewStamp();
    }

    public void SetEmail(string? email) => Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
}
