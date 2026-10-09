using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class StoreTypeRepository
    : BaseRepository<StoreType, StoreTypeSearchRequest, StoreTypeRequest>,
      IStoreTypeRepository
{
    public StoreTypeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<StoreType> BuildQuery(StoreTypeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(StoreTypeRequest r, StoreType e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(StoreType e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(StoreType e, int status, long userId) { }

    public async Task<List<StoreTypeTreeResponse>> GetTreeAsync(StoreTypeSearchRequest request)
    {
        var all = await SearchAllAsync(request);
        var nameMap    = all.ToDictionary(x => x.Id, x => x.Name);
        var childCount = all.GroupBy(x => x.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Count());
        var flat = all.ConvertAll(x => ToTreeResponse(x, nameMap, childCount));
        return BuildTree(flat, null);
    }

    private static StoreTypeTreeResponse ToTreeResponse(
        StoreType x, Dictionary<long, string?> nameMap, Dictionary<long, int> childCount)
    {
        var count = childCount.GetValueOrDefault(x.Id, 0);
        return new StoreTypeTreeResponse
        {
            Id          = x.Id,
            Name        = x.Name,
            ParentId    = x.ParentId,
            ParentName  = x.ParentId.HasValue ? nameMap.GetValueOrDefault(x.ParentId.Value) : null,
            PublicId    = x.PublicId,
            TenantId    = x.TenantId,
            TenantName  = x.TenantName,
            HasChildren = count > 0,
            ChildCount  = count
        };
    }

    private static List<StoreTypeTreeResponse> BuildTree(List<StoreTypeTreeResponse> flat, long? parentId)
        => [.. flat
            .Where(x => parentId == null ? (x.ParentId == null || x.ParentId == 0) : x.ParentId == parentId)
            .OrderBy(x => x.Id)
            .Select(x => new StoreTypeTreeResponse
            {
                Id          = x.Id,
                Name        = x.Name,
                ParentId    = x.ParentId,
                ParentName  = x.ParentName,
                PublicId    = x.PublicId,
                TenantId    = x.TenantId,
                TenantName  = x.TenantName,
                HasChildren = x.HasChildren,
                ChildCount  = x.ChildCount,
                Children    = BuildTree(flat, x.Id)
            })];
}
