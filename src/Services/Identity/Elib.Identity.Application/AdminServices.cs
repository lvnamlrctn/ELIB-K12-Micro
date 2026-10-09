using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using Elib.Identity.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Identity.Application;

/// <summary>Quyền hiệu lực — nguồn sự thật cho <see cref="IPermissionSource"/> của mọi service.</summary>
public sealed class PermissionQueries(IIdentityDb db, ITenantContext tenant)
{
    public async Task<IReadOnlySet<string>> GetEffectiveAsync(long? tenantId, long userId, CancellationToken ct)
    {
        if (tenantId is null)
        {
            using var system = tenant.UseSystem();
            var admin = await db.SystemUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            return admin is { IsActive: true } ? new HashSet<string> { PermissionCodes.All } : new HashSet<string>();
        }

        using var scope = tenant.Use(tenantId.Value);
        var user = await db.StaffUsers.AsNoTracking().Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is { IsActive: true } ? user.EffectivePermissions() : new HashSet<string>();
    }
}

/// <summary>Quản lý tài khoản nhân viên trong đơn vị hiện tại.</summary>
public sealed class UserAdminService(IIdentityDb db, ITenantContext tenant, IPasswordHasher hasher, IPublishEndpoint publisher, ICurrentActor actor, TimeProvider clock,
    IdentityAudit audit)
{
    public async Task<PagedResult<UserDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        tenant.RequireTenantId();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.StaffUsers.AsNoTracking().Include(u => u.Roles).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang SQL lower().
            query = query.Where(u => u.UserName.Contains(term) || u.FullName.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.UserName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var now = clock.GetUtcNow();
        return new PagedResult<UserDto>(users.Select(u => ToDto(u, now)).ToList(), total, page, pageSize);
    }

    public async Task<UserDto> GetAsync(Guid publicId, CancellationToken ct) =>
        ToDto(await LoadAsync(publicId, ct), clock.GetUtcNow());

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var user = await CreateInCurrentTenantAsync(request.UserName, request.FullName, request.Email, request.Phone, request.Password,
            await RolesAsync(request.RoleIds ?? [], ct), mustChangePassword: true, ct);
        return ToDto(user, clock.GetUtcNow());
    }

    internal async Task<StaffUser> CreateInCurrentTenantAsync(
        string userName, string fullName, string? email, string? phone, string password, IReadOnlyList<Role> roles, bool mustChangePassword, CancellationToken ct)
    {
        tenant.RequireTenantId();
        AccountRules.EnsureStrongPassword(password);
        var normalized = AccountRules.NormalizeUserName(userName);
        if (await db.StaffUsers.AnyAsync(u => u.UserName == normalized, ct))
            throw new ConflictException("USERNAME_EXISTS", $"Tên đăng nhập '{normalized}' đã tồn tại trong đơn vị.");

        var user = StaffUser.Create(normalized, fullName, email, phone, hasher.Hash(password), mustChangePassword);
        user.SetRoles(roles);
        db.StaffUsers.Add(user);
        user.PublicId = Guid.CreateVersion7(); // cần cho nhật ký trước khi lưu
        await audit.TenantAsync(tenant.RequireTenantId(), "USER_CREATE", "User", user.PublicId.ToString(),
            $"Tạo tài khoản {user.UserName} — {user.FullName}" + (roles.Count > 0 ? $" (vai trò: {string.Join(", ", roles.Select(r => r.Name))})" : ""), ct);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<UserDto> UpdateAsync(Guid publicId, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await LoadAsync(publicId, ct);
        var stamp = user.PermissionStamp;
        user.Update(request.FullName, request.Email, request.Phone);
        user.SetActive(request.IsActive);
        await audit.TenantAsync(tenant.RequireTenantId(), "USER_UPDATE", "User", user.PublicId.ToString(),
            $"Sửa tài khoản {user.UserName}" + (user.IsActive ? "" : " (vô hiệu hoá)"), ct);
        if (user.PermissionStamp != stamp) await PublishChangedAsync([user.Id], ct);
        await db.SaveChangesAsync(ct);
        return ToDto(user, clock.GetUtcNow());
    }

    public async Task<UserDto> SetRolesAsync(Guid publicId, SetUserRolesRequest request, CancellationToken ct)
    {
        var user = await LoadAsync(publicId, ct);
        var roles = await RolesAsync(request.RoleIds, ct);
        user.SetRoles(roles);
        await audit.TenantAsync(tenant.RequireTenantId(), "USER_ROLES", "User", user.PublicId.ToString(),
            $"Gán vai trò cho {user.UserName}: " + (roles.Count > 0 ? string.Join(", ", roles.Select(r => r.Name)) : "(không có)"), ct);
        await PublishChangedAsync([user.Id], ct);
        await db.SaveChangesAsync(ct);
        return ToDto(user, clock.GetUtcNow());
    }

    public async Task ResetPasswordAsync(Guid publicId, ResetPasswordRequest request, CancellationToken ct)
    {
        AccountRules.EnsureStrongPassword(request.NewPassword);
        var user = await LoadAsync(publicId, ct);
        user.Credentials.SetPasswordHash(hasher.Hash(request.NewPassword), mustChange: true);
        await audit.TenantAsync(tenant.RequireTenantId(), "USER_RESET_PASSWORD", "User", user.PublicId.ToString(), $"Đặt lại mật khẩu cho {user.UserName}", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlockAsync(Guid publicId, CancellationToken ct)
    {
        var user = await LoadAsync(publicId, ct);
        user.Credentials.Unlock();
        await audit.TenantAsync(tenant.RequireTenantId(), "USER_UNLOCK", "User", user.PublicId.ToString(), $"Mở khoá tài khoản {user.UserName}", ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<StaffUser> LoadAsync(Guid publicId, CancellationToken ct)
    {
        tenant.RequireTenantId();
        return await db.StaffUsers.Include(u => u.Roles).FirstOrDefaultAsync(u => u.PublicId == publicId, ct)
               ?? throw new NotFoundException("tài khoản", publicId);
    }

    private async Task<IReadOnlyList<Role>> RolesAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var roles = await db.Roles.Where(r => ids.Contains(r.PublicId)).ToListAsync(ct);
        var missing = ids.Except(roles.Select(r => r.PublicId)).ToList();
        return missing.Count == 0 ? roles : throw new BusinessRuleException("ROLE_NOT_FOUND", $"Không có vai trò: {string.Join(", ", missing)}.");
    }

    private Task PublishChangedAsync(IReadOnlyList<long> userIds, CancellationToken ct) =>
        publisher.Publish(new PermissionChanged { TenantId = tenant.RequireTenantId(), UserIds = userIds, Actor = new EventActor(actor.Id, actor.Kind) }, ct);

    internal static UserDto ToDto(StaffUser u, DateTimeOffset now) => new(
        u.PublicId, u.Id, u.UserName, u.FullName, u.Email, u.Phone, u.IsActive, u.Credentials.IsLockedOut(now), u.Credentials.LastLoginAt,
        u.Roles.Where(r => !r.IsDeleted).OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => new RoleRefDto(r.PublicId, r.Name)).ToList());
}

public sealed class RoleAdminService(IIdentityDb db, ITenantContext tenant, IPublishEndpoint publisher, ICurrentActor actor, IModuleLicenseSource licenses,
    IdentityAudit audit)
{
    /// <summary>Module quyền đơn vị hiện tại được phân (theo license) — cho màn phân quyền.</summary>
    public async Task<IReadOnlyList<PermissionModule>> CatalogAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var licensed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in PermissionCatalog.All.Select(m => m.License).OfType<string>().Distinct(StringComparer.Ordinal))
            if (await licenses.IsLicensedAsync(tenantId, module, ct)) licensed.Add(module);
        return PermissionCatalog.Available(licensed);
    }

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct)
    {
        tenant.RequireTenantId();
        return (await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<RoleDto> CreateAsync(RoleRequest request, CancellationToken ct)
    {
        tenant.RequireTenantId();
        var role = Role.Create(request.Name, request.Description, request.Permissions);
        PermissionCatalog.Validate(role.Permissions, await CatalogAsync(ct));
        await EnsureUniqueNameAsync(role.Name, null, ct);
        db.Roles.Add(role);
        role.PublicId = Guid.CreateVersion7();
        await audit.TenantAsync(tenant.RequireTenantId(), "ROLE_CREATE", "Role", role.PublicId.ToString(), $"Tạo vai trò {role.Name} ({Describe(role)})", ct);
        await db.SaveChangesAsync(ct);
        return ToDto(role);
    }

    public async Task<RoleDto> UpdateAsync(Guid publicId, RoleRequest request, CancellationToken ct)
    {
        var role = await LoadAsync(publicId, ct);
        if (role.IsBuiltIn && role.Name == Role.AdminRoleName)
        {
            // Vai trò quản trị mặc định giữ nguyên tên + toàn quyền — sửa nhầm là khoá chính mình khỏi đơn vị.
            if (Role.NormalizePermissions(request.Permissions) is not ["*"] || request.Name?.Trim() != Role.AdminRoleName)
                throw new ConflictException("ROLE_BUILT_IN_LOCKED", "Không đổi được tên và quyền của vai trò quản trị đơn vị mặc định.");
            role.Update(role.Name, request.Description, role.Permissions);
        }
        else
        {
            role.Update(request.Name, request.Description, request.Permissions);
            PermissionCatalog.Validate(role.Permissions, await CatalogAsync(ct));
        }
        await EnsureUniqueNameAsync(role.Name, role.Id, ct);
        await audit.TenantAsync(tenant.RequireTenantId(), "ROLE_UPDATE", "Role", role.PublicId.ToString(), $"Sửa vai trò {role.Name} ({Describe(role)})", ct);
        await PublishTenantWideAsync(ct);
        await db.SaveChangesAsync(ct);
        return ToDto(role);
    }

    public async Task DeleteAsync(Guid publicId, CancellationToken ct)
    {
        var role = await LoadAsync(publicId, ct);
        role.EnsureDeletable();
        db.Roles.Remove(role); // xoá mềm; quyền của vai trò đã xoá không còn được tính
        await audit.TenantAsync(tenant.RequireTenantId(), "ROLE_DELETE", "Role", role.PublicId.ToString(), $"Xoá vai trò {role.Name}", ct);
        await PublishTenantWideAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Role> LoadAsync(Guid publicId, CancellationToken ct)
    {
        tenant.RequireTenantId();
        return await db.Roles.FirstOrDefaultAsync(r => r.PublicId == publicId, ct) ?? throw new NotFoundException("vai trò", publicId);
    }

    private async Task EnsureUniqueNameAsync(string name, long? excludeId, CancellationToken ct)
    {
        if (await db.Roles.AnyAsync(r => r.Name == name && (excludeId == null || r.Id != excludeId), ct))
            throw new ConflictException("ROLE_NAME_EXISTS", $"Vai trò '{name}' đã tồn tại.");
    }

    private Task PublishTenantWideAsync(CancellationToken ct) =>
        publisher.Publish(new PermissionChanged { TenantId = tenant.RequireTenantId(), AllUsersOfTenant = true, Actor = new EventActor(actor.Id, actor.Kind) }, ct);

    private static RoleDto ToDto(Role r) => new(r.PublicId, r.Name, r.Description, r.IsBuiltIn, r.Permissions);

    /// <summary>Tóm tắt quyền cho nhật ký: "toàn quyền" hoặc danh sách mã (cắt bớt khi quá dài).</summary>
    private static string Describe(Role r) =>
        r.Permissions.Contains("*") ? "toàn quyền"
        : r.Permissions.Count == 0 ? "chưa có quyền"
        : r.Permissions.Count <= 12 ? string.Join(", ", r.Permissions)
        : string.Join(", ", r.Permissions.Take(12)) + $"… ({r.Permissions.Count} quyền)";
}

/// <summary>Vai trò mặc định của đơn vị — chạy khi khởi tạo đơn vị (TenantProvisioned) và trước khi tạo quản trị đơn vị đầu tiên.</summary>
public sealed class IdentityTenantSeeder(IIdentityDb db) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken) => await EnsureBuiltInRolesAsync(cancellationToken);

    public async Task<Role> EnsureBuiltInRolesAsync(CancellationToken ct)
    {
        var existing = await db.Roles.Where(r => r.IsBuiltIn).ToListAsync(ct);
        var admin = existing.FirstOrDefault(r => r.Name == Role.AdminRoleName);
        if (admin is null)
        {
            admin = Role.Create(Role.AdminRoleName, "Toàn quyền trong đơn vị", [PermissionCodes.All], isBuiltIn: true);
            db.Roles.Add(admin);
        }
        if (existing.All(r => r.Name != Role.LibrarianRoleName))
            db.Roles.Add(Role.Create(Role.LibrarianRoleName, "Nghiệp vụ thư viện — bổ sung quyền khi triển khai các phân hệ", [], isBuiltIn: true));
        await db.SaveChangesAsync(ct);
        return admin;
    }
}

/// <summary>Quản trị hệ thống tạo tài khoản quản trị đầu tiên cho đơn vị mới.</summary>
public sealed class TenantBootstrapService(IIdentityDb db, ITenantContext tenant, IdentityTenantSeeder seeder, UserAdminService users, TimeProvider clock,
    IdentityAudit audit)
{
    public async Task<UserDto> CreateTenantAdminAsync(long tenantId, CreateTenantAdminRequest request, CancellationToken ct)
    {
        var replica = await db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct)
            ?? throw new NotFoundException("đơn vị", tenantId);
        if (replica.Status is "Suspended" or "ProvisioningFailed")
            throw new ConflictException("TENANT_UNAVAILABLE", $"Đơn vị {replica.Code} đang ở trạng thái {replica.Status}.");

        await audit.SystemAsync("TENANT_ADMIN_CREATE", "Tenant", tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"Tạo tài khoản quản trị {request.UserName} cho đơn vị {replica.Code}", tenantId, ct);
        using var scope = tenant.Use(tenantId);
        var adminRole = await seeder.EnsureBuiltInRolesAsync(ct);
        var user = await users.CreateInCurrentTenantAsync(request.UserName, request.FullName, request.Email, null, request.Password,
            [adminRole], mustChangePassword: true, ct);
        return UserAdminService.ToDto(user, clock.GetUtcNow());
    }
}
