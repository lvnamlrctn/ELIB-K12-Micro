using Elib.BuildingBlocks.TenantReplica;
using Elib.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Identity.Application;

public interface IIdentityDb
{
    DbSet<StaffUser> StaffUsers { get; }
    DbSet<SystemUser> SystemUsers { get; }
    DbSet<Role> Roles { get; }
    DbSet<TenantReplicaRecord> TenantReplicas { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Người dùng đã xác thực — nguồn để dựng claim cho cookie đăng nhập và access token.</summary>
/// <remarks><see cref="ReadOnlyImpersonation"/>: quản trị nền tảng (UserId là tài khoản hệ thống) đang xem đơn vị TenantId, chỉ đọc.</remarks>
public sealed record SignInIdentity(
    long UserId, long? TenantId, string UserName, string FullName, string PermissionStamp,
    string? TenantCode, string? TenantSubdomain, bool MustChangePassword, bool ReadOnlyImpersonation = false, string? Email = null)
{
    public bool IsSystem => TenantId is null;
}

public enum LoginError
{
    InvalidCredentials,
    LockedOut,
    AccountDisabled,
    TenantUnavailable,
}

public sealed record LoginResult(SignInIdentity? Identity, LoginError? Error)
{
    public bool Succeeded => Identity is not null;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record RoleRefDto(Guid PublicId, string Name);

public sealed record UserDto(
    Guid PublicId, long Id, string UserName, string FullName, string? Email, string? Phone, bool IsActive,
    bool LockedOut, DateTimeOffset? LastLoginAt, IReadOnlyList<RoleRefDto> Roles);

public sealed record RoleDto(Guid PublicId, string Name, string? Description, bool IsBuiltIn, IReadOnlyList<string> Permissions);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary><see cref="ReadOnly"/> = đang đóng vai đơn vị (chỉ đọc), <see cref="ExpiresAt"/> = hạn của phiên đóng vai.</summary>
public sealed record MeDto(long UserId, long? TenantId, string UserName, string FullName, bool MustChangePassword, IReadOnlyList<string> Permissions,
    bool ReadOnly = false, DateTimeOffset? ExpiresAt = null);

public sealed record CreateUserRequest(string UserName, string FullName, string? Email, string? Phone, string Password, IReadOnlyList<Guid>? RoleIds);

public sealed record UpdateUserRequest(string FullName, string? Email, string? Phone, bool IsActive);

public sealed record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record RoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record CreateTenantAdminRequest(string UserName, string FullName, string? Email, string Password);
