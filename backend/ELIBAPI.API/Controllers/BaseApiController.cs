using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.API.Controllers;

[ApiController]
[Authorize]
public abstract class BaseApiController : ControllerBase
{
    protected long GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : 0;
    }

    protected long? GetTenantId()
    {
        var value = User.FindFirstValue("TenantId");
        return long.TryParse(value, out var id) ? id : null;
    }

    protected string? GetRoleCode() => User.FindFirstValue("RoleCode");

    // User có quyền thao tác trên các bản ghi đã khoá (hoàn tất/đã ký) — super-admin (không tenant)
    // hoặc RoleCode nằm trong ReadOnlyPolicy:RoleCodes/AdminRoleCodes (appsettings.json).
    protected bool IsPrivilegedRole()
    {
        if (GetTenantId() == null) return true;

        var roleCode = GetRoleCode();
        if (roleCode == null) return false;

        var config = HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        var roleCodes      = config?.GetSection("ReadOnlyPolicy:RoleCodes").Get<string[]>() ?? [];
        var adminRoleCodes = config?.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        return roleCodes.Concat(adminRoleCodes).Contains(roleCode, StringComparer.OrdinalIgnoreCase);
    }
}
