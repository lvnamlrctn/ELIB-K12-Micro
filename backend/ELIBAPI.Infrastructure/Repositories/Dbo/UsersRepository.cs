using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Repositories;

public class UsersRepository
    : BaseRepository<Users, UsersSearchRequest, UsersRequest>, IUsersRepository
{
    public UsersRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public async Task ResetPasswordAsync(Guid publicId, string hashedPassword, long userId)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.PublicId == publicId && u.IsDelete != 2)
            ?? throw new KeyNotFoundException();
        user.Password       = hashedPassword;
        user.UpdateBy       = userId;
        user.LastUpdate     = DateTime.Now;
        user.UpdateRowBy    = userId;
        user.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(long userId, string oldPlainPassword, string newPlainPassword)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.Id == userId && u.IsDelete != 2)
            ?? throw new KeyNotFoundException();
        if (!PasswordHasher.Verify(oldPlainPassword, user.Password))
            throw new UnauthorizedAccessException("OldPasswordIncorrect");
        user.Password       = PasswordHasher.Hash(newPlainPassword);
        user.UpdateBy       = userId;
        user.LastUpdate     = DateTime.Now;
        user.UpdateRowBy    = userId;
        user.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    protected override IQueryable<Users> BuildQuery(UsersSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))
        {
            var kw = r.Keyword.ToLower();
            q = q.Where(x => (x.FullName  != null && x.FullName.ToLower().Contains(kw))  ||
                             (x.LoginName != null && x.LoginName.ToLower().Contains(kw)) ||
                             (x.Email     != null && x.Email.ToLower().Contains(kw)));
        }
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status  == r.Status);
        if (r.RoleId.HasValue)                  q = q.Where(x => x.RoleId  == r.RoleId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(UsersRequest r, Users e, long userId, bool isNew)
    {
        e.FullName = r.FullName; e.LoginName = r.LoginName; e.Email = r.Email;
        e.Phone = r.Phone; e.PortalId = r.PortalId; e.Language = r.Language;
        // Đổi role ảnh hưởng tới bypass admin/chỉ-xem → làm mới PermissionStamp để claim JWT cũ bị coi là cũ ngay.
        if (!isNew && e.RoleId != r.RoleId) e.PermissionStamp = Guid.NewGuid().ToString();
        e.RoleId = r.RoleId; e.PostionId = r.PostionId; e.Status = r.Status;
        e.Sex = r.Sex; e.Address = r.Address; e.BirthDate = r.BirthDate;
        e.Photo = r.Photo; e.RoleWinformId = r.RoleWinformId;
        if (!string.IsNullOrEmpty(r.Password))
            e.Password = PasswordHasher.Hash(r.Password);
        e.UpdateBy = userId; e.LastUpdate = DateTime.Now;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            e.CreatedBy = userId; e.CreatedDate = DateTime.Now;
            e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now;
        }
    }

    public async Task<bool> CheckLoginNameExistsAsync(string loginName, Guid? excludePublicId = null)
    {
        var deptId = GetCurrentTenantId();
        var q = _dbSet.Where(x => x.IsDelete != 2 && x.LoginName!.ToLower() == loginName.ToLower());
        if (deptId.HasValue)         q = q.Where(x => x.TenantId == deptId);
        if (excludePublicId.HasValue) q = q.Where(x => x.PublicId    != excludePublicId.Value);
        return await q.AnyAsync();
    }

    public async Task<List<UserPermissionResponse>> GetPermissionsAsync(Guid userPublicId)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.PublicId == userPublicId && u.IsDelete != 2)
            ?? throw new KeyNotFoundException();

        var modules = await _context.Modules
            .Where(m => m.IsDelete != 2 && m.Status == 2)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .ToListAsync();

        var roleCode = await _context.Roles
            .Where(r => r.Id == (long?)user.RoleId && r.IsDelete != 2)
            .Select(r => r.Code)
            .FirstOrDefaultAsync();

        var config = _http.HttpContext?.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        var adminRoleCodes    = config?.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        var hiddenModuleCodes = config?.GetSection("ReadOnlyPolicy:AdminModuleCodesHidden").Get<string[]>() ?? [];

        var isAdminRole = roleCode != null && adminRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
        if (!isAdminRole && hiddenModuleCodes.Length > 0)
            modules = modules.Where(m => !hiddenModuleCodes.Contains(m.ModuleCode, StringComparer.OrdinalIgnoreCase)).ToList();

        var perms = await _context.Permissions
            .Where(p => p.UserId == user.Id && p.IsDelete != 2)
            .ToListAsync();

        var permMap = perms
            .GroupBy(p => p.ModuleId ?? 0)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());

        var flat = modules.ConvertAll(m =>
        {
            permMap.TryGetValue(m.Id, out var p);
            return new UserPermissionResponse
            {
                ModuleId           = m.Id,
                ModulePublicId     = m.PublicId,
                ModuleName         = m.Name,
                ParentId           = m.ParentId,
                ModuleCode         = m.ModuleCode,
                Link               = m.Link,
                SortOrder          = m.SortOrder,
                PermissionPublicId = p?.PublicId,
                Can_Access         = p?.Can_Access,
                Can_View           = p?.Can_View,
                Can_Add            = p?.Can_Add,
                Can_Edit           = p?.Can_Edit,
                Can_Delete         = p?.Can_Delete,
            };
        });

        return BuildTree(flat, null);
    }

    public async Task SavePermissionsAsync(SavePermissionRequest request, long editorUserId)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.PublicId == request.UserId && u.IsDelete != 2)
            ?? throw new KeyNotFoundException();

        var moduleIds = request.Permissions.Select(p => (long?)p.ModuleId).ToList();

        var existing = await _context.Permissions
            .Where(p => p.UserId == user.Id && moduleIds.Contains(p.ModuleId) && p.IsDelete != 2)
            .ToListAsync();

        var existingMap = existing
            .GroupBy(p => p.ModuleId ?? 0)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());

        var now = DateTime.Now;

        foreach (var item in request.Permissions)
        {
            byte toVal(bool b) => b ? (byte)2 : (byte)1;

            if (existingMap.TryGetValue(item.ModuleId, out var perm))
            {
                perm.Can_View   = toVal(item.CanView);
                perm.Can_Add    = toVal(item.CanAdd);
                perm.Can_Edit   = toVal(item.CanEdit);
                perm.Can_Delete = toVal(item.CanDelete);
                perm.Can_Access = toVal(item.CanView);
                perm.UpdateRowBy    = editorUserId;
                perm.UpdatedRowDate = now;
            }
            else
            {
                _context.Permissions.Add(new ELIBAPI.Core.Entities.Cms.Permission
                {
                    UserId          = user.Id,
                    ModuleId        = item.ModuleId,
                    Can_View        = toVal(item.CanView),
                    Can_Add         = toVal(item.CanAdd),
                    Can_Edit        = toVal(item.CanEdit),
                    Can_Delete      = toVal(item.CanDelete),
                    Can_Access      = toVal(item.CanView),
                    PublicId        = Guid.NewGuid(),
                    CreatedRowBy    = editorUserId,
                    CreatedRowDate  = now,
                    UpdateRowBy     = editorUserId,
                    UpdatedRowDate  = now,
                });
            }
        }

        // Quyền vừa đổi → làm mới PermissionStamp: claim JWT cũ của user này lệch stamp và tự fallback về đường DB,
        // quyền mới có hiệu lực ngay ở request kế tiếp (port ELIB-LRC 09-27).
        user.PermissionStamp = Guid.NewGuid().ToString();
        await _context.SaveChangesAsync();
    }

    private static List<UserPermissionResponse> BuildTree(List<UserPermissionResponse> flat, long? parentId)
        => [.. flat
            .Where(m => parentId == null ? (m.ParentId == null || m.ParentId == 0) : m.ParentId == parentId)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.ModuleId)
            .Select(m => new UserPermissionResponse
            {
                ModuleId           = m.ModuleId,
                ModulePublicId     = m.ModulePublicId,
                ModuleName         = m.ModuleName,
                ParentId           = m.ParentId,
                ModuleCode         = m.ModuleCode,
                Link               = m.Link,
                SortOrder          = m.SortOrder,
                PermissionPublicId = m.PermissionPublicId,
                Can_Access         = m.Can_Access,
                Can_View           = m.Can_View,
                Can_Add            = m.Can_Add,
                Can_Edit           = m.Can_Edit,
                Can_Delete         = m.Can_Delete,
                Children           = BuildTree(flat, m.ModuleId)
            })];

    protected override void SoftDelete(Users e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Users e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
