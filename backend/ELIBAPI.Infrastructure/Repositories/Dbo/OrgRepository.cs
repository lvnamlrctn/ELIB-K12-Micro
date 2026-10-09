using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class OrgRepository
    : BaseRepository<Org, OrgSearchRequest, OrgRequest>, IOrgRepository
{
    public OrgRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Org> BuildQuery(OrgSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ParentId.HasValue)               q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status   == r.Status);
        return q.OrderBy(x => x.Order).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(OrgRequest r, Org e, long userId, bool isNew)
    {
        e.Name = r.Name; e.ParentId = r.ParentId; e.Level = r.Level; e.Status = r.Status;
        e.Order = r.Order; e.PortalId = r.PortalId; e.Language = r.Language; e.Link = r.Link;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Org e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Org e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public async Task<List<OrgTreeResponse>> GetTreeAsync(OrgSearchRequest request)
    {
        var treeRequest = new OrgSearchRequest
        {
            Keyword  = request.Keyword,
            PortalId = request.PortalId,
            Language = request.Language,
            Status   = request.Status,
            TenantId = request.TenantId
        };
        var all        = await SearchAllAsync(treeRequest);
        var nameMap    = all.ToDictionary(o => o.Id, o => o.Name);
        var childCount = all.GroupBy(o => o.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Count());
        var flat = all.ConvertAll(o => ToTreeResponse(o, nameMap, childCount));
        return BuildTree(flat, null);
    }

    public async Task UpdateOrderAsync(Guid publicId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException();
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        entity.Order = newOrder; entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<Org> MoveAsync(Guid publicId, long? newParentId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException();
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        long newLevel = 1;
        if (newParentId.HasValue) { var parent = await GetByIdAsync(newParentId.Value); newLevel = (parent?.Level ?? 0) + 1; }
        bool parentChanged = entity.ParentId != newParentId;
        entity.ParentId = newParentId; entity.Order = newOrder; entity.Level = newLevel;
        entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        if (parentChanged) await UpdateChildrenLevelAsync(entity.Id, newLevel);
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

    private async Task UpdateChildrenLevelAsync(long parentId, long parentLevel)
    {
        var children = await _dbSet.Where(x => x.ParentId == parentId && x.IsDelete != 2).ToListAsync();
        foreach (var child in children)
        {
            child.Level = parentLevel + 1;
            await UpdateChildrenLevelAsync(child.Id, child.Level!.Value);
        }
    }

    private static OrgTreeResponse ToTreeResponse(
        Org o, Dictionary<long, string?> nameMap, Dictionary<long, int> childCount)
    {
        var count = childCount.GetValueOrDefault(o.Id, 0);
        return new OrgTreeResponse
        {
            Id         = o.Id,   Name      = o.Name,     ParentId  = o.ParentId,
            ParentName = o.ParentId.HasValue ? nameMap.GetValueOrDefault(o.ParentId.Value) : null,
            Level      = o.Level, Status   = o.Status,  Order     = o.Order,
            PortalId   = o.PortalId, Language = o.Language, Link  = o.Link,
            PublicId   = o.PublicId, TenantId = o.TenantId, TenantName = o.TenantName,
            HasChildren = count > 0, ChildCount = count
        };
    }

    private static List<OrgTreeResponse> BuildTree(List<OrgTreeResponse> flat, long? parentId)
        => [.. flat
            .Where(o => parentId == null ? (o.ParentId == null || o.ParentId == 0) : o.ParentId == parentId)
            .OrderBy(o => o.Order).ThenBy(o => o.Id)
            .Select(o => new OrgTreeResponse
            {
                Id = o.Id, Name = o.Name, ParentId = o.ParentId, ParentName = o.ParentName,
                Level = o.Level, Status = o.Status, Order = o.Order, PortalId = o.PortalId,
                Language = o.Language, Link = o.Link, PublicId = o.PublicId,
                TenantId = o.TenantId, TenantName = o.TenantName,
                HasChildren = o.HasChildren, ChildCount = o.ChildCount,
                Children = BuildTree(flat, o.Id)
            })];
}
