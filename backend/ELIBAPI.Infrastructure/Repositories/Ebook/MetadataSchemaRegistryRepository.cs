using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class MetadataSchemaRegistryRepository : BaseRepository<MetadataSchemaRegistry, MetadataSchemaRegistrySearchRequest, MetadataSchemaRegistryRequest>
{
    public MetadataSchemaRegistryRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public override async Task<PagedResult<MetadataSchemaRegistry>> SearchAsync(MetadataSchemaRegistrySearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q     = ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await q.Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize).Take(request.PageSize).ToListAsync();
        if (IsReadOnlyPolicyUser()) await FillTenantNamesAsync(items);
        return new PagedResult<MetadataSchemaRegistry> { Items = items, TotalCount = total, PageIndex = request.PageIndex, PageSize = request.PageSize };
    }

    public override async Task<List<MetadataSchemaRegistry>> SearchAllAsync(MetadataSchemaRegistrySearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId).ToListAsync();
        if (IsReadOnlyPolicyUser()) await FillTenantNamesAsync(items);
        return items;
    }

    protected override IQueryable<MetadataSchemaRegistry> BuildQuery(MetadataSchemaRegistrySearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.NameSpace!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        return q.OrderByDescending(x => x.MetadataSchemaId);
    }

    protected override void MapRequestToEntity(MetadataSchemaRegistryRequest r, MetadataSchemaRegistry e, long userId, bool isNew)
    {
        e.NameSpace = r.NameSpace; e.ShortId = r.ShortId; e.PortalId = r.PortalId; e.Language = r.Language;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MetadataSchemaRegistry e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MetadataSchemaRegistry e, int status, long userId) { }
}
