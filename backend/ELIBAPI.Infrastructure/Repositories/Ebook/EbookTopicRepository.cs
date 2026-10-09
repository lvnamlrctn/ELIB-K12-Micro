using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookTopicRepository
    : BaseRepository<EbookTopic, EbookTopicSearchRequest, EbookTopicRequest>,
      IEbookTopicRepository
{
    public EbookTopicRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<EbookTopic> BuildQuery(EbookTopicSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ParentId.HasValue)               q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Status.HasValue && r.Status>0)   q = q.Where(x => x.Status   == r.Status);
        return q.OrderBy(x => x.Order).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EbookTopicRequest r, EbookTopic e, long userId, bool isNew)
    {
        e.Name = r.Name; e.ParentId = r.ParentId; e.Level = r.Level; e.Status = r.Status;
        e.Order = r.Order; e.PortalId = r.PortalId; e.Language = r.Language; e.IsLogin = r.IsLogin;
        e.Description = r.Description; e.Keyword = r.Keyword; e.PageTitle = r.PageTitle;
        e.MetaDescription = r.MetaDescription; e.DDC = r.DDC;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EbookTopic e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookTopic e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public async Task<List<EbookTopicTreeResponse>> GetTreeAsync(EbookTopicSearchRequest request)
    {
        var treeRequest = new EbookTopicSearchRequest
        {
            Keyword = request.Keyword, PortalId = request.PortalId,
            Language = request.Language, Status = request.Status,
            TenantId = request.TenantId
        };
        var all = await SearchAllAsync(treeRequest);
        var nameMap    = all.ToDictionary(c => c.Id, c => c.Name);
        var childCount = all.GroupBy(c => c.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Count());
        var flat = all.Select(c => ToTopicTreeResponse(c, nameMap, childCount)).ToList();
        return BuildTopicTree(flat, null);
    }

    public async Task UpdateOrderAsync(Guid publicId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        entity.Order = newOrder; entity.UpdateRowBy = GetCurrentUserId(); entity.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<EbookTopic> MoveAsync(Guid publicId, long? newParentId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        long newLevel = 1;
        if (newParentId.HasValue) { var parent = await GetByIdAsync(newParentId.Value); newLevel = (parent?.Level ?? 0) + 1; }
        bool parentChanged = entity.ParentId != newParentId;
        entity.ParentId = newParentId; entity.Order = newOrder; entity.Level = newLevel;
        entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        if (parentChanged) await UpdateTopicChildrenLevelAsync(entity.Id, newLevel);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteWithChildrenAsync(Guid publicId)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        await SoftDeleteTopicCascadeAsync(entity.Id, GetCurrentUserId());
        await _context.SaveChangesAsync();
    }

    private async Task SoftDeleteTopicCascadeAsync(long id, long userId)
    {
        var e = await _dbSet.FindAsync(id);
        if (e == null || e.IsDelete == 2) return;
        e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        var children = await _dbSet.Where(x => x.ParentId == id && x.IsDelete != 2).ToListAsync();
        foreach (var child in children) await SoftDeleteTopicCascadeAsync(child.Id, userId);
    }

    private async Task UpdateTopicChildrenLevelAsync(long parentId, long parentLevel)
    {
        var children = await _dbSet.Where(x => x.ParentId == parentId && x.IsDelete != 2).ToListAsync();
        foreach (var child in children)
        {
            child.Level = parentLevel + 1;
            await UpdateTopicChildrenLevelAsync(child.Id, child.Level.Value);
        }
    }

    private static EbookTopicTreeResponse ToTopicTreeResponse(
        EbookTopic c, Dictionary<long, string?> nameMap, Dictionary<long, int> childCount)
    {
        var count = childCount.GetValueOrDefault(c.Id, 0);
        return new EbookTopicTreeResponse
        {
            Id = c.Id, Name = c.Name, ParentId = c.ParentId,
            ParentName = c.ParentId.HasValue ? nameMap.GetValueOrDefault(c.ParentId.Value) : null,
            Level = c.Level, Status = c.Status, Order = c.Order,
            PortalId = c.PortalId, Language = c.Language, IsLogin = c.IsLogin,
            Description = c.Description, Keyword = c.Keyword, PageTitle = c.PageTitle,
            MetaDescription = c.MetaDescription, DDC = c.DDC, PublicId = c.PublicId,
            TenantId = c.TenantId, TenantName = c.TenantName,
            HasChildren = count > 0, ChildCount = count
        };
    }

    private List<EbookTopicTreeResponse> BuildTopicTree(List<EbookTopicTreeResponse> flat, long? parentId)
        => flat
            .Where(c => parentId == null ? (c.ParentId == null || c.ParentId == 0) : c.ParentId == parentId)
            .OrderBy(c => c.Order).ThenBy(c => c.Id)
            .Select(c => new EbookTopicTreeResponse
            {
                Id = c.Id, Name = c.Name, ParentId = c.ParentId, ParentName = c.ParentName,
                Level = c.Level, Status = c.Status, Order = c.Order,
                PortalId = c.PortalId, Language = c.Language, IsLogin = c.IsLogin,
                Description = c.Description, Keyword = c.Keyword, PageTitle = c.PageTitle,
                MetaDescription = c.MetaDescription, DDC = c.DDC, PublicId = c.PublicId,
                TenantId = c.TenantId, TenantName = c.TenantName,
                HasChildren = c.HasChildren, ChildCount = c.ChildCount,
                Children = BuildTopicTree(flat, c.Id)
            })
            .ToList();
}
