using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookCollectionRepository
    : BaseRepository<EbookCollection, EbookCollectionSearchRequest, EbookCollectionRequest>,
      IEbookCollectionRepository
{
    public EbookCollectionRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<EbookCollection> BuildQuery(EbookCollectionSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ParentId.HasValue)               q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Status.HasValue && r.Status>0)   q = q.Where(x => x.Status   == r.Status);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EbookCollectionRequest r, EbookCollection e, long userId, bool isNew)
    {
        e.Name = r.Name; e.ParentId = r.ParentId; e.Level = r.Level; e.Status = r.Status;
        e.SortOrder = r.Order; e.PortalId = r.PortalId; e.Language = r.Language;
        e.Allowdownload = r.Allowdownload; e.Images = r.Images; e.Share = r.Share;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EbookCollection e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookCollection e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    public async Task<List<EbookCollectionTreeResponse>> GetTreeAsync(EbookCollectionSearchRequest request)
    {
        var treeRequest = new EbookCollectionSearchRequest
        {
            Keyword = request.Keyword, PortalId = request.PortalId,
            Language = request.Language, Status = request.Status,
            TenantId = request.TenantId
        };
        var all = await SearchAllAsync(treeRequest);
        var nameMap    = all.ToDictionary(c => c.Id, c => c.Name);
        var childCount = all.GroupBy(c => c.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Count());
        var flat = all.Select(c => ToCollectionTreeResponse(c, nameMap, childCount)).ToList();
        return BuildCollectionTree(flat, null);
    }

    public async Task UpdateOrderAsync(Guid publicId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        entity.SortOrder = newOrder; entity.UpdateRowBy = GetCurrentUserId(); entity.UpdatedRowDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<EbookCollection> MoveAsync(Guid publicId, long? newParentId, int newOrder)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);
        var userId = GetCurrentUserId();
        long newLevel = 1;
        if (newParentId.HasValue) { var parent = await GetByIdAsync(newParentId.Value); newLevel = (parent?.Level ?? 0) + 1; }
        bool parentChanged = entity.ParentId != newParentId;
        entity.ParentId = newParentId; entity.SortOrder = newOrder; entity.Level = newLevel;
        entity.UpdateRowBy = userId; entity.UpdatedRowDate = DateTime.Now;
        if (parentChanged) await UpdateCollectionChildrenLevelAsync(entity.Id, newLevel);
        await _context.SaveChangesAsync();
        return entity;
    }

    // Chặn xóa nếu bộ sưu tập còn bộ sưu tập con hoặc đã có tài liệu — phải xóa con/gỡ tài liệu trước.
    public override async Task DeleteAsync(Guid publicId)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException("NotFound");

        var hasChildren = await _dbSet.AnyAsync(x => x.ParentId == entity.Id && x.IsDelete != 2);
        if (hasChildren)
            throw new Exception("Không thể xóa vì bộ sưu tập còn chứa bộ sưu tập con. Vui lòng xóa các bộ sưu tập con trước.");

        var hasDocuments = await _context.EbookItems.AnyAsync(x => x.CollectionId == entity.Id && x.IsDelete != 2);
        if (hasDocuments)
            throw new Exception("Không thể xóa vì bộ sưu tập đã có tài liệu.");

        await base.DeleteAsync(publicId);
    }

    public async Task DeleteWithChildrenAsync(Guid publicId)
    {
        var entity = await GetByPublicIdAsync(publicId) ?? throw new KeyNotFoundException("NotFound");
        await CheckTenantOwnershipAsync(entity);

        var subtreeIds = await CollectSubtreeIdsAsync(entity.Id);
        var hasDocuments = await _context.EbookItems.AnyAsync(x => x.CollectionId.HasValue && subtreeIds.Contains(x.CollectionId.Value) && x.IsDelete != 2);
        if (hasDocuments)
            throw new Exception("Không thể xóa vì một số bộ sưu tập trong cây đã có tài liệu.");

        await SoftDeleteCollectionCascadeAsync(entity.Id, GetCurrentUserId());
        await _context.SaveChangesAsync();
    }

    private async Task<List<long>> CollectSubtreeIdsAsync(long id)
    {
        var result = new List<long> { id };
        var children = await _dbSet.Where(x => x.ParentId == id && x.IsDelete != 2).Select(x => x.Id).ToListAsync();
        foreach (var childId in children) result.AddRange(await CollectSubtreeIdsAsync(childId));
        return result;
    }

    private async Task SoftDeleteCollectionCascadeAsync(long id, long userId)
    {
        var e = await _dbSet.FindAsync(id);
        if (e == null || e.IsDelete == 2) return;
        e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        var children = await _dbSet.Where(x => x.ParentId == id && x.IsDelete != 2).ToListAsync();
        foreach (var child in children) await SoftDeleteCollectionCascadeAsync(child.Id, userId);
    }

    private async Task UpdateCollectionChildrenLevelAsync(long parentId, long parentLevel)
    {
        var children = await _dbSet.Where(x => x.ParentId == parentId && x.IsDelete != 2).ToListAsync();
        foreach (var child in children)
        {
            child.Level = parentLevel + 1;
            await UpdateCollectionChildrenLevelAsync(child.Id, child.Level.Value);
        }
    }

    private static EbookCollectionTreeResponse ToCollectionTreeResponse(
        EbookCollection c, Dictionary<long, string?> nameMap, Dictionary<long, int> childCount)
    {
        var count = childCount.GetValueOrDefault(c.Id, 0);
        return new EbookCollectionTreeResponse
        {
            Id = c.Id, Name = c.Name, ParentId = c.ParentId,
            ParentName = c.ParentId.HasValue ? nameMap.GetValueOrDefault(c.ParentId.Value) : null,
            Level = c.Level, Status = c.Status, SortOrder = c.SortOrder,
            PortalId = c.PortalId, Language = c.Language, Link = c.Link,
            Allowdownload = c.Allowdownload, Images = c.Images, PublicId = c.PublicId,
            TenantId = c.TenantId, TenantName = c.TenantName,
            HasChildren = count > 0, ChildCount = count
        };
    }

    private List<EbookCollectionTreeResponse> BuildCollectionTree(List<EbookCollectionTreeResponse> flat, long? parentId)
        => flat
            .Where(c => parentId == null ? (c.ParentId == null || c.ParentId == 0) : c.ParentId == parentId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new EbookCollectionTreeResponse
            {
                Id = c.Id, Name = c.Name, ParentId = c.ParentId, ParentName = c.ParentName,
                Level = c.Level, Status = c.Status, SortOrder = c.SortOrder,
                PortalId = c.PortalId, Language = c.Language, Link = c.Link,
                Allowdownload = c.Allowdownload, Images = c.Images, PublicId = c.PublicId,
                TenantId = c.TenantId, TenantName = c.TenantName,
                HasChildren = c.HasChildren, ChildCount = c.ChildCount,
                Children = BuildCollectionTree(flat, c.Id)
            })
            .ToList();
}
