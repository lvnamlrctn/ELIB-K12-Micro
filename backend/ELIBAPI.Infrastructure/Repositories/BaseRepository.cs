using System.Security.Claims;
using System.Text.Json;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Repositories;

public abstract class BaseRepository<TEntity, TSearch, TRequest>
    : IGenericRepository<TEntity, TSearch, TRequest>
    where TEntity  : class
    where TSearch  : SearchRequest
    where TRequest : class
{
    protected readonly ELIBAPIDbContext    _context;
    protected readonly DbSet<TEntity>      _dbSet;
    protected readonly IHttpContextAccessor _http;

    protected BaseRepository(ELIBAPIDbContext context, IHttpContextAccessor http)
    {
        _context = context;
        _dbSet   = context.Set<TEntity>();
        _http    = http;
    }

    // ── JWT helpers ───────────────────────────────────────────────────────────
    // Khi chạy trong tác vụ nền (AdminTaskService.RunNext, Đợt 10) không có HttpContext — PHẢI rơi về
    // ngữ cảnh nền tường minh (_context.BackgroundActorId/BackgroundTenantId) thay vì âm thầm trả
    // userId=0/tenantId=null, vì tenantId=null bị coi là "tài khoản đặc quyền không tenant" ở khắp nơi
    // (GetCurrentTenantId() == null bên dưới) — nếu không sửa, mọi thao tác nghiệp vụ chạy nền sẽ bỏ qua
    // toàn bộ lọc TenantId. Phát hiện lúc khảo sát port AdminTask v2 từ ELIB-LRC.
    protected long GetCurrentUserId()
    {
        if (_http.HttpContext == null) return _context.BackgroundActorId ?? 0;
        var value = _http.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : 0;
    }

    protected long? GetCurrentTenantId()
    {
        if (_http.HttpContext == null) return _context.BackgroundTenantId;
        var value = _http.HttpContext.User.FindFirstValue("TenantId");
        return long.TryParse(value, out var id) ? id : null;
    }

    private string? GetCurrentTenantCode() => _http.HttpContext?.User.FindFirstValue("TenantCode");
    private string? GetCurrentRoleCode()   => _http.HttpContext?.User.FindFirstValue("RoleCode");

    protected bool IsReadOnlyPolicyUser()
    {
        if (GetCurrentTenantId() == null) return true;

        var config = _http.HttpContext?.RequestServices
            .GetService(typeof(IConfiguration)) as IConfiguration;
        if (config == null) return false;

        var tenantCode  = GetCurrentTenantCode();
        var tenantCodes = config.GetSection("ReadOnlyPolicy:TenantCodes")
            .Get<string[]>() ?? [];
        if (tenantCode != null
            && tenantCodes.Contains(tenantCode, StringComparer.OrdinalIgnoreCase))
            return true;

        var roleCode       = GetCurrentRoleCode();
        var roleCodes      = config.GetSection("ReadOnlyPolicy:RoleCodes").Get<string[]>() ?? [];
        var adminRoleCodes = config.GetSection("ReadOnlyPolicy:AdminRoleCodes").Get<string[]>() ?? [];
        if (roleCode != null
            && roleCodes.Concat(adminRoleCodes)
                .Contains(roleCode, StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    // ── Abstract methods ──────────────────────────────────────────────────────
    protected abstract IQueryable<TEntity> BuildQuery(TSearch request);
    protected abstract void MapRequestToEntity(TRequest r, TEntity e, long userId, bool isNew);
    protected abstract void SoftDelete(TEntity e, long userId);
    protected abstract void SetStatus(TEntity e, int status, long userId);

    // ── Lịch sử thay đổi chi tiết (Đợt 16) ───────────────────────────────────
    // Mặc định rỗng — KHÔNG có tác động gì tới các entity không override (early-return trong UpdateAsync).
    // Repository muốn bật audit chi tiết (vd ReaderRepository) override 2 property này.
    protected virtual string[] AuditedFields => [];
    protected virtual HashSet<string> MaskedAuditFields => [];

    // ── TenantId helpers ──────────────────────────────────────────────────────
    protected IQueryable<TEntity> ApplyTenantFilter(IQueryable<TEntity> q, long? requestTenantId = null)
    {
        var prop = typeof(TEntity).GetProperty("TenantId");
        if (prop == null) return q;

        var jwtTenantId = GetCurrentTenantId();

        // Privileged: super-admin (no TenantId claim) hoặc IsReadOnlyPolicyUser
        if (jwtTenantId == null || IsReadOnlyPolicyUser())
        {
            // Đợt 23: có chọn cụ thể 1 đơn vị thì gộp thêm dữ liệu dùng chung (TenantId=null) —
            // trước đây chỉ lọc đúng riêng đơn vị đã chọn, thiếu dữ liệu toàn hệ thống.
            if (requestTenantId.HasValue)
                return q.Where(e => EF.Property<long?>(e, "TenantId") == requestTenantId
                                  || EF.Property<long?>(e, "TenantId") == null);
            return q;
        }

        // User thường: bỏ qua requestTenantId, ép theo JWT
        return q.Where(e => EF.Property<long?>(e, "TenantId") == jwtTenantId);
    }

    protected async Task<long?> ResolveRequestTenantIdAsync(Guid? tenantPublicId)
    {
        if (!tenantPublicId.HasValue) return null;
        var jwtTenantId = GetCurrentTenantId();
        if (jwtTenantId != null && !IsReadOnlyPolicyUser()) return null;
        return await _context.Tenants
            .Where(t => t.PublicId == tenantPublicId.Value && t.IsDelete != 2)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync();
    }

    protected IQueryable<TEntity> ApplyTenantFilterIncludeNull(IQueryable<TEntity> q, long? requestTenantId = null)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null || IsReadOnlyPolicyUser())
        {
            // Đợt 23: đồng bộ với ApplyTenantFilter — có chọn cụ thể 1 đơn vị thì lọc đơn vị đó + dữ
            // liệu dùng chung; trước đây nhánh đặc quyền luôn trả q không lọc gì (kể cả khi request có
            // chọn đơn vị), vì các repo gọi hàm này chưa từng truyền requestTenantId vào.
            if (requestTenantId.HasValue)
                return q.Where(e => EF.Property<long?>(e, "TenantId") == requestTenantId
                                  || EF.Property<long?>(e, "TenantId") == null);
            return q;
        }
        var prop = typeof(TEntity).GetProperty("TenantId");
        if (prop == null) return q;
        return q.Where(e => EF.Property<long?>(e, "TenantId") == tenantId
                         || EF.Property<long?>(e, "TenantId") == null);
    }

    private static void ApplyPublicIdOnAdd(TEntity e)
    {
        var prop = typeof(TEntity).GetProperty("PublicId");
        if (prop == null || prop.PropertyType != typeof(Guid)) return;
        var current = (Guid)(prop.GetValue(e) ?? Guid.Empty);
        if (current == Guid.Empty) prop.SetValue(e, Guid.NewGuid());
    }

    private void ApplyTenantOnAdd(TEntity e)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return;
        var prop = typeof(TEntity).GetProperty("TenantId");
        prop?.SetValue(e, tenantId);
    }

    // Đợt 23: trước đây IsAdmin()/IsAdminRoleUser() cho bỏ qua kiểm tra chủ sở hữu HOÀN TOÀN VÔ ĐIỀU
    // KIỆN — kể cả tài khoản đặc quyền theo role (RoleCode trong AdminRoleCodes) CÓ đơn vị riêng vẫn sửa/
    // xoá được bản ghi của bất kỳ đơn vị nào. Đồng bộ với quy tắc đọc (ApplyTenantFilter): chỉ tài khoản
    // hệ thống gốc thật sự (JWT không có TenantId — không có "đơn vị mình" để so sánh) mới không giới
    // hạn; tài khoản đặc quyền có đơn vị riêng chỉ được sửa/xoá đơn vị mình hoặc bản ghi dùng chung
    // (TenantId=null).
    protected Task CheckTenantOwnershipAsync(TEntity e)
    {
        var jwtTenantId = GetCurrentTenantId();
        if (jwtTenantId == null) return Task.CompletedTask;

        var prop = typeof(TEntity).GetProperty("TenantId");
        if (prop == null) return Task.CompletedTask;
        var entityTenant = prop.GetValue(e) as long?;

        if (entityTenant == jwtTenantId) return Task.CompletedTask;
        if (entityTenant == null && IsReadOnlyPolicyUser()) return Task.CompletedTask;

        throw new UnauthorizedAccessException("ForbiddenDepartment");
    }

    // Quy tắc đọc 1 bản ghi theo khoá — đồng bộ ApplyTenantFilterIncludeNull: tài khoản đặc quyền (không có TenantId
    // hoặc IsReadOnlyPolicyUser) thấy mọi bản ghi; user thường chỉ thấy bản ghi đơn vị mình hoặc dùng chung (TenantId=null).
    // Trước đây GetById/GetByPublicId không kiểm tra đơn vị → biết Id/PublicId là đọc được dữ liệu đơn vị khác.
    protected bool IsVisibleToCurrentTenant(TEntity e)
    {
        var prop = typeof(TEntity).GetProperty("TenantId");
        if (prop == null) return true;
        var jwtTenantId = GetCurrentTenantId();
        if (jwtTenantId == null || IsReadOnlyPolicyUser()) return true;
        var entityTenant = prop.GetValue(e) as long?;
        return entityTenant == null || entityTenant == jwtTenantId;
    }

    // ── GetById ───────────────────────────────────────────────────────────────
    public async Task<TEntity?> GetByIdAsync(long id)
    {
        var e = await _dbSet.FindAsync(id);
        if (e == null) return null;
        var isDeleteProp = typeof(TEntity).GetProperty("IsDelete");
        if (isDeleteProp?.GetValue(e) is int v && v == 2) return null;
        return IsVisibleToCurrentTenant(e) ? e : null;
    }

    public async Task<TEntity?> GetByPublicIdAsync(Guid publicId)
    {
        var prop = typeof(TEntity).GetProperty("PublicId");
        if (prop == null) return null;
        var e = await _dbSet
            .Where(e => EF.Property<Guid>(e, "PublicId") == publicId
                     && EF.Property<int?>(e, "IsDelete") != 2)
            .FirstOrDefaultAsync();
        return e != null && IsVisibleToCurrentTenant(e) ? e : null;
    }

    // ── Search ────────────────────────────────────────────────────────────────
    public virtual async Task<PagedResult<TEntity>> SearchAsync(TSearch request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q     = ApplyTenantFilter(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await q
            .Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();
        await FillTenantNamesAsync(items);
        return new PagedResult<TEntity>
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize
        };
    }

    public virtual async Task<List<TEntity>> SearchAllAsync(TSearch request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ApplyTenantFilter(BuildQuery(request), requestTenantId).ToListAsync();
        await FillTenantNamesAsync(items);
        return items;
    }

    protected async Task FillTenantNamesAsync<T>(List<T> items) where T : class
    {
        var tenantIdProp   = typeof(T).GetProperty("TenantId");
        var tenantNameProp = typeof(T).GetProperty("TenantName");
        if (tenantIdProp == null || tenantNameProp == null) return;

        var tenantIds = items
            .Select(item => tenantIdProp.GetValue(item) as long?)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct().ToList();
        if (!tenantIds.Any()) return;

        var tenantMap = await _context.Tenants
            .Where(t => tenantIds.Contains(t.Id) && t.IsDelete != 2)
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        foreach (var item in items)
        {
            var tenantId = tenantIdProp.GetValue(item) as long?;
            if (tenantId.HasValue && tenantMap.TryGetValue(tenantId.Value, out var name))
                tenantNameProp.SetValue(item, name);
        }
    }

    // ── Add ───────────────────────────────────────────────────────────────────
    public async Task<TEntity> AddAsync(TRequest request)
    {
        var userId = GetCurrentUserId();
        var entity = Activator.CreateInstance<TEntity>();
        ApplyPublicIdOnAdd(entity);
        MapRequestToEntity(request, entity, userId, true);
        ApplyTenantOnAdd(entity);
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
        await WriteUserLogAsync("Add", entity, userId);
        return entity;
    }

    public async Task AddRangeAsync(IEnumerable<TRequest> requests)
    {
        var userId = GetCurrentUserId();
        var entities = new List<TEntity>();
        foreach (var request in requests)
        {
            var entity = Activator.CreateInstance<TEntity>();
            ApplyPublicIdOnAdd(entity);
            MapRequestToEntity(request, entity, userId, true);
            ApplyTenantOnAdd(entity);
            entities.Add(entity);
        }
        _dbSet.AddRange(entities);
        await _context.SaveChangesAsync();
        // Ghi log đơn giản cho batch
        try
        {
            _context.UserLogs.Add(new UserLog
            {
                UserId      = userId,
                ActionType  = "Import",
                Object      = typeof(TEntity).Name,
                Action      = $"Import {entities.Count} {typeof(TEntity).Name} items",
                Submited    = DateTime.Now,
                Application = "ELIBAPI",
                TenantId = GetCurrentTenantId()
            });
            await _context.SaveChangesAsync();
        }
        catch { }
    }

    // ── Update ────────────────────────────────────────────────────────────────
    public async Task<TEntity> UpdateAsync(Guid publicId, TRequest request)
    {
        var entity = await GetByPublicIdAsync(publicId)
            ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        MapRequestToEntity(request, entity, userId, false);
        if (AuditedFields.Length > 0)
            await QueueEntityChangeLogAsync(entity, userId);
        await _context.SaveChangesAsync();
        await WriteUserLogAsync("Update", entity, userId);
        return entity;
    }

    /// <summary>Diff entity theo <see cref="AuditedFields"/> và xếp hàng 1 dòng UserLog EntityChange —
    /// gọi TRƯỚC <c>SaveChangesAsync</c> chính để atomic (xem <see cref="EntityAuditService"/>).</summary>
    private async Task QueueEntityChangeLogAsync(TEntity entity, long userId)
    {
        var changes = EntityAuditService.DiffTrackedEntity(_context, entity, AuditedFields, MaskedAuditFields);
        if (changes.Count == 0) return;

        var publicIdProp = typeof(TEntity).GetProperty("PublicId");
        if (publicIdProp?.GetValue(entity) is not Guid publicId) return;

        var actorName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName ?? u.LoginName)
            .FirstOrDefaultAsync();
        var reason = EntityAuditService.ReadReasonHeader(_http.HttpContext);
        var ip     = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

        EntityAuditService.QueueEntityChangeLog(
            _context, typeof(TEntity).Name, publicId, userId, actorName,
            GetCurrentTenantId(), "Update", reason, changes, ip);
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    public virtual async Task DeleteAsync(Guid publicId)
    {
        var entity = await GetByPublicIdAsync(publicId)
            ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        SoftDelete(entity, userId);
        await _context.SaveChangesAsync();
        await WriteUserLogAsync("Delete", entity, userId);
    }

    // ── ChangeStatus ──────────────────────────────────────────────────────────
    public async Task ChangeStatusAsync(ChangeStatusRequest request)
    {
        var entity = await GetByPublicIdAsync(request.PublicId)
            ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        SetStatus(entity, request.Status, userId);
        await _context.SaveChangesAsync();
        await WriteUserLogAsync("ChangeStatus", entity, userId);
    }

    // ── UserLog ───────────────────────────────────────────────────────────────
    private async Task WriteUserLogAsync(string actionType, TEntity entity, long userId)
    {
        try
        {
            var entityName = typeof(TEntity).Name;
            var idProp     = typeof(TEntity).GetProperty("Id");
            var idValue    = idProp?.GetValue(entity);
            var portalId   = typeof(TEntity).GetProperty("PortalId")?.GetValue(entity) as string;
            var ip         = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
            var tenantId = GetCurrentTenantId();
            _context.UserLogs.Add(new UserLog
            {
                UserId      = userId,
                ActionType  = actionType,
                Object      = entityName,
                Action      = $"{actionType} {entityName} #{idValue}",
                Submited    = DateTime.Now,
                Ip          = ip,
                Application = "ELIBAPI",
                PortalId    = portalId,
                TenantId    = tenantId
            });
            await _context.SaveChangesAsync();
        }
        catch { /* không để lỗi UserLog làm hỏng luồng chính */ }
    }
}

