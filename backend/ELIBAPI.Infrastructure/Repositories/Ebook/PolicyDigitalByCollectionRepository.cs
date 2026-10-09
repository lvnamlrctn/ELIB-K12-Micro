using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class PolicyDigitalByCollectionRepository
    : BaseRepository<PolicyDigitalByCollection, PolicyDigitalByCollectionSearchRequest, PolicyDigitalByCollectionRequest>,
      IPolicyDigitalByCollectionRepository
{
    public PolicyDigitalByCollectionRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<PolicyDigitalByCollection> BuildQuery(PolicyDigitalByCollectionSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.CollectionId.HasValue) q = q.Where(x => x.CollectionId == r.CollectionId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PolicyDigitalByCollectionRequest r, PolicyDigitalByCollection e, long userId, bool isNew)
    {
        e.ReaderTypeid = r.ReaderTypeid; e.CollectionId = r.CollectionId; e.Maxpage = r.Maxpage;
        e.Maxsize = r.Maxsize; e.Maxdocument = r.Maxdocument; e.Read = r.Read;
        e.Comment = r.Comment; e.Download = r.Download; e.OfflineDays = r.OfflineDays;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(PolicyDigitalByCollection e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(PolicyDigitalByCollection e, int status, long userId) { }

    public async Task<List<PolicyDigitalByCollection>> GetByCollectionPublicIdAsync(Guid collectionPublicId)
    {
        var collectionId = await _context.EbookCollections
            .Where(c => c.PublicId == collectionPublicId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
        if (!collectionId.HasValue) throw new KeyNotFoundException("NotFound");
        return await _dbSet.Where(x => x.CollectionId == collectionId && x.IsDelete != 2).ToListAsync();
    }

    public async Task SaveByCollectionPublicIdAsync(Guid collectionPublicId, List<PermissionItemRequest> permissions)
    {
        var collectionId = await _context.EbookCollections
            .Where(c => c.PublicId == collectionPublicId && c.IsDelete != 2)
            .Select(c => (long?)c.Id)
            .FirstOrDefaultAsync();
        if (!collectionId.HasValue) throw new KeyNotFoundException("NotFound");

        // Lấy tất cả bộ sưu tập con (đệ quy)
        var childIds = await GetAllDescendantIdsAsync(collectionId.Value);
        var allIds   = new List<long> { collectionId.Value };
        allIds.AddRange(childIds);

        var userId = GetCurrentUserId();
        var deptId = GetCurrentTenantId();

        // Soft-delete quyền hiện tại của cha và tất cả con
        var existing = await _dbSet
            .Where(x => allIds.Contains((long)(x.CollectionId ?? 0)) && x.IsDelete != 2)
            .ToListAsync();
        foreach (var e in existing)
        {
            e.IsDelete = 2;
            e.UpdateRowBy = userId;
            e.UpdatedRowDate = DateTime.Now;
        }

        // Thêm quyền mới cho cha và tất cả con
        foreach (var cid in allIds)
        {
            foreach (var r in permissions)
            {
                _dbSet.Add(new PolicyDigitalByCollection
                {
                    PublicId       = Guid.NewGuid(),
                    CollectionId   = (int?)cid,
                    ReaderTypeid   = r.ReaderTypeId,
                    Read           = r.Read,
                    Download       = r.Download,
                    Maxdocument    = r.Maxdocument,
                    OfflineDays    = r.OfflineDays,
                    TenantId       = deptId,
                    CreatedRowBy   = userId,
                    CreatedRowDate = DateTime.Now,
                    UpdateRowBy    = userId,
                    UpdatedRowDate = DateTime.Now
                });
            }
        }

        await _context.SaveChangesAsync();
        try
        {
            _context.UserLogs.Add(new UserLog
            {
                UserId = userId,
                ActionType = "SavePermissions",
                Object = nameof(PolicyDigitalByCollection),
                Action = $"SavePermissions for EbookCollection #{collectionId} and {childIds.Count} children, {permissions.Count} permission types",
                Submited = DateTime.Now,
                Application = "ELIBAPI",
                TenantId = deptId
            });
            await _context.SaveChangesAsync();
        }
        catch { }
    }

    private async Task<List<long>> GetAllDescendantIdsAsync(long parentId)
    {
        var result = new List<long>();
        var queue  = new Queue<long>();
        queue.Enqueue(parentId);
        while (queue.Count > 0)
        {
            var current  = queue.Dequeue();
            var children = await _context.EbookCollections
                .Where(c => c.ParentId == current && c.IsDelete != 2)
                .Select(c => c.Id)
                .ToListAsync();
            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child);
            }
        }
        return result;
    }
}
