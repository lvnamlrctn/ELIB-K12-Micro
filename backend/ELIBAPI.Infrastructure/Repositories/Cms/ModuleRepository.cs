using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class ModuleRepository
    : BaseRepository<Module, ModuleSearchRequest, ModuleRequest>, IModuleRepository
{
    public ModuleRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Module> BuildQuery(ModuleSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ParentId.HasValue)               q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status   == r.Status);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    // cms.Module là cây menu/quyền dùng CHUNG cho mọi tenant (TenantId=null = module toàn hệ thống,
    // seed sẵn ~207 module); chỉ những module riêng do 1 tenant tự tạo mới có TenantId cụ thể.
    // BaseRepository.ApplyTenantFilter (strict) sẽ loại bỏ toàn bộ module TenantId=null đối với user
    // thường (jwtTenantId != null) — khiến GetTree/Search chỉ còn thấy đúng module riêng của tenant đó,
    // ẩn mất cả menu hệ thống. Dùng bộ lọc include-null (global + của riêng tenant) — Đợt 23: hàm dùng
    // chung giờ đã tự xử lý cả nhánh "đặc quyền có chọn đơn vị" (gộp thêm TenantId=null), bỏ ternary cũ.
    public override async Task<List<Module>> SearchAllAsync(ModuleSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId).ToListAsync();
        await FillTenantNamesAsync(items);
        return items;
    }

    public override async Task<PagedResult<Module>> SearchAsync(ModuleSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q = ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await q
            .Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();
        await FillTenantNamesAsync(items);
        return new PagedResult<Module>
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize
        };
    }

    protected override void MapRequestToEntity(ModuleRequest r, Module e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Link = r.Link; e.Icon = r.Icon; e.ParentId = r.ParentId;
        e.Language = r.Language; e.PortalId = r.PortalId; e.Group = r.Group;
        e.SortOrder = r.SortOrder; e.ModuleCode = r.ModuleCode; e.Type = r.Type;
        e.FolderPage = r.FolderPage; e.Status = r.Status;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Module e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Module e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public async Task<List<ModuleTreeResponse>> GetTreeAsync(ModuleSearchRequest request)
    {
        var treeRequest = new ModuleSearchRequest
        {
            Keyword  = request.Keyword,
            PortalId = request.PortalId,
            Language = request.Language,
            Status   = request.Status
        };
        var all        = await SearchAllAsync(treeRequest);
        var nameMap    = all.ToDictionary(m => m.Id, m => m.Name);
        var childCount = all.GroupBy(m => m.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Count());
        var flat       = all.ConvertAll(m => ToTreeResponse(m, nameMap, childCount));
        return BuildTree(flat, null);
    }

    public async Task UpdateOrderAsync(Guid publicId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException();
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        entity.SortOrder = newOrder; entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<Module> MoveAsync(Guid publicId, long? newParentId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException();
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        entity.ParentId = newParentId; entity.SortOrder = newOrder;
        entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteWithChildrenAsync(Guid publicId)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException();
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        await SoftDeleteCascadeAsync(entity.Id, userId);
        await _context.SaveChangesAsync();
    }

    private async Task SoftDeleteCascadeAsync(long id, long userId)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity == null || entity.IsDelete == 2) return;
        entity.IsDelete = 2; entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        var children = await _dbSet.Where(x => x.ParentId == id && x.IsDelete != 2).ToListAsync();
        foreach (var child in children) await SoftDeleteCascadeAsync(child.Id, userId);
    }

    private static ModuleTreeResponse ToTreeResponse(
        Module m, Dictionary<long, string?> nameMap, Dictionary<long, int> childCount)
    {
        var count = childCount.GetValueOrDefault(m.Id, 0);
        return new ModuleTreeResponse
        {
            Id         = m.Id,        Name       = m.Name,       ParentId   = m.ParentId,
            ParentName = m.ParentId.HasValue ? nameMap.GetValueOrDefault(m.ParentId.Value) : null,
            Link       = m.Link,      Icon       = m.Icon,       Language   = m.Language,
            PortalId   = m.PortalId,  Group      = m.Group,      SortOrder  = m.SortOrder,
            ModuleCode = m.ModuleCode, Type      = m.Type,       FolderPage = m.FolderPage,
            Status     = m.Status,    PublicId   = m.PublicId,
            HasChildren = count > 0,  ChildCount = count
        };
    }

    private static List<ModuleTreeResponse> BuildTree(List<ModuleTreeResponse> flat, long? parentId)
        => [.. flat
            .Where(m => parentId == null ? (m.ParentId == null || m.ParentId == 0) : m.ParentId == parentId)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .Select(m => new ModuleTreeResponse
            {
                Id = m.Id, Name = m.Name, ParentId = m.ParentId, ParentName = m.ParentName,
                Link = m.Link, Icon = m.Icon, Language = m.Language, PortalId = m.PortalId,
                Group = m.Group, SortOrder = m.SortOrder, ModuleCode = m.ModuleCode,
                Type = m.Type, FolderPage = m.FolderPage, Status = m.Status, PublicId = m.PublicId,
                HasChildren = m.HasChildren, ChildCount = m.ChildCount,
                Children = BuildTree(flat, m.Id)
            })];
}
