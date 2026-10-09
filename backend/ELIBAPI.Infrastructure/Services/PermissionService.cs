using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly ELIBAPIDbContext _db;
    private readonly IConfiguration   _config;

    public PermissionService(ELIBAPIDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    public async Task<bool> HasPermissionAsync(long userId, string moduleCode, string action)
    {
        // AdminRoleCodes → toàn quyền mọi module, bỏ qua permission table
        if (await IsAdminRoleAsync(userId))
            return true;

        if (action is "add" or "edit" or "delete" && await IsReadOnlyUserAsync(userId))
        {
            if (!await IsAdminModuleExemptAsync(userId, moduleCode))
                return false;
        }

        var module = await _db.Modules
            .Where(m => m.ModuleCode == moduleCode && m.IsDelete != 2)
            .FirstOrDefaultAsync();

        if (module == null) return false;

        var perm = await _db.Permissions
            .Where(p => p.UserId   == userId
                     && p.ModuleId == module.Id
                     && p.IsDelete != 2)
            .FirstOrDefaultAsync();

        if (perm == null) return false;

        return action.ToLower() switch
        {
            "add"    => perm.Can_Add    == 2,
            "edit"   => perm.Can_Edit   == 2,
            "delete" => perm.Can_Delete == 2,
            "view"   => perm.Can_View   == 2,
            "access" => perm.Can_Access == 2,
            _        => false
        };
    }

    public async Task<Dictionary<string, List<string>>> GetUserPermissionsAsync(long userId)
    {
        var rows = await (
            from p in _db.Permissions
            where p.UserId == userId && p.IsDelete != 2
            join m in _db.Modules.Where(m => m.IsDelete != 2 && m.ModuleCode != null) on p.ModuleId equals (long?)m.Id
            select new { m.ModuleCode, p.Can_View, p.Can_Add, p.Can_Edit, p.Can_Delete, p.Can_Access }
        ).ToListAsync();
        var codes = rows.Select(r => r.ModuleCode!).Distinct().ToList();
        // HasPermissionAsync lấy 1 module bất kỳ theo mã — mã trùng nhiều module thì không suy ra được, bỏ khỏi claim.
        var duplicated = (await _db.Modules.Where(m => m.IsDelete != 2 && codes.Contains(m.ModuleCode!))
                .GroupBy(m => m.ModuleCode).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync())
            .ToHashSet();

        var result = new Dictionary<string, List<string>>();
        foreach (var g in rows.GroupBy(r => r.ModuleCode!))
        {
            if (duplicated.Contains(g.Key) || g.Count() > 1) continue;
            var p = g.First();
            var actions = new List<string>();
            if (p.Can_View   == 2) actions.Add("view");
            if (p.Can_Add    == 2) actions.Add("add");
            if (p.Can_Edit   == 2) actions.Add("edit");
            if (p.Can_Delete == 2) actions.Add("delete");
            if (p.Can_Access == 2) actions.Add("access");
            result[g.Key] = actions;
        }
        return result;
    }

    public async Task<string?> GetPermissionStampAsync(long userId)
        => await _db.Users.Where(u => u.Id == userId).Select(u => u.PermissionStamp).FirstOrDefaultAsync();

    public async Task<bool> IsReadOnlyUserAsync(long userId)
    {
        var info = await (
            from u in _db.Users
            where u.Id == userId && u.IsDelete != 2
            join t in _db.Tenants on u.TenantId equals (long?)t.Id into tj
            from t in tj.DefaultIfEmpty()
            join r in _db.Roles on (long?)u.RoleId equals r.Id into rj
            from r in rj.DefaultIfEmpty()
            select new { u.TenantId, TenantCode = (string?)t.Code, RoleCode = (string?)r.Code }
        ).FirstOrDefaultAsync();

        if (info == null) return false;

        // TenantId IS NULL → tài khoản hệ thống, chỉ được xem
        if (!info.TenantId.HasValue) return true;

        // Tenant code nằm trong danh sách read-only (vd: "SVH")
        var tenantCodes = _config.GetSection("ReadOnlyPolicy:TenantCodes").Get<string[]>() ?? [];
        if (info.TenantCode != null
            && tenantCodes.Contains(info.TenantCode, StringComparer.OrdinalIgnoreCase))
            return true;

        // Role code nằm trong danh sách nhóm quản trị hệ thống
        var roleCodes = _config.GetSection("ReadOnlyPolicy:RoleCodes").Get<string[]>() ?? [];
        if (info.RoleCode != null
            && roleCodes.Contains(info.RoleCode, StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public async Task<bool> IsAdminRoleAsync(long userId)
    {
        var adminRoles = _config.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        if (adminRoles.Length == 0) return false;

        var roleCode = await (
            from u in _db.Users
            where u.Id == userId && u.IsDelete != 2
            join r in _db.Roles on (long?)u.RoleId equals r.Id into rj
            from r in rj.DefaultIfEmpty()
            select (string?)r.Code
        ).FirstOrDefaultAsync();

        return roleCode != null
            && adminRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> IsAdminModuleExemptAsync(long userId, string moduleCode)
    {
        var adminModules = _config.GetSection("ReadOnlyPolicy:AdminModuleCodes").Get<string[]>() ?? [];
        if (!adminModules.Contains(moduleCode, StringComparer.OrdinalIgnoreCase))
            return false;

        var adminRoles = _config.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var roleCode = await (
            from u in _db.Users
            where u.Id == userId && u.IsDelete != 2
            join r in _db.Roles on (long?)u.RoleId equals r.Id into rj
            from r in rj.DefaultIfEmpty()
            select (string?)r.Code
        ).FirstOrDefaultAsync();

        return roleCode != null
            && adminRoles.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
    }
}
