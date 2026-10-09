using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class MetaDataFieldRegisteryRepository : BaseRepository<MetaDataFieldRegistery, MetaDataFieldRegisterySearchRequest, MetaDataFieldRegisteryRequest>
{
    public MetaDataFieldRegisteryRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public override async Task<PagedResult<MetaDataFieldRegistery>> SearchAsync(MetaDataFieldRegisterySearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q     = ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await q.Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize).Take(request.PageSize).ToListAsync();
        if (IsReadOnlyPolicyUser()) await FillTenantNamesAsync(items);
        return new PagedResult<MetaDataFieldRegistery> { Items = items, TotalCount = total, PageIndex = request.PageIndex, PageSize = request.PageSize };
    }

    public override async Task<List<MetaDataFieldRegistery>> SearchAllAsync(MetaDataFieldRegisterySearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId).ToListAsync();
        if (IsReadOnlyPolicyUser()) await FillTenantNamesAsync(items);
        return items;
    }

    protected override IQueryable<MetaDataFieldRegistery> BuildQuery(MetaDataFieldRegisterySearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))   q = q.Where(x => x.Field!.Contains(r.Keyword));
        if (r.MetaDataSchemaId.HasValue)         q = q.Where(x => x.MetaDataSchemaId == r.MetaDataSchemaId);
        if (r.Status.HasValue && r.Status > 0)   q = q.Where(x => x.Status           == r.Status);
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.MetaDataFieldId);
    }

    protected override void MapRequestToEntity(MetaDataFieldRegisteryRequest r, MetaDataFieldRegistery e, long userId, bool isNew)
    {
        e.MetaDataSchemaId = r.MetaDataSchemaId; e.Field = r.Field; e.Subfield = r.Subfield;
        e.DescriptionVn = r.DescriptionVn; e.DescriptionEn = r.DescriptionEn; e.SortOrder = r.SortOrder;
        e.Status = r.Status; e.Input = r.Input; e.ExportField = r.ExportField;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MetaDataFieldRegistery e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MetaDataFieldRegistery e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
