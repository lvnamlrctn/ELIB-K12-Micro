using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class MaterialsTypeRepository : BaseRepository<MaterialsType, MaterialsTypeSearchRequest, MaterialsTypeRequest>
{
    public MaterialsTypeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    // Toàn bộ MaterialsType có TenantId = NULL (danh mục MARC dùng chung) — dùng bản IncludeNull
    // giống pattern StoreRepository để danh mục dùng chung vẫn hiển thị cho user có TenantId.
    public override async Task<PagedResult<MaterialsType>> SearchAsync(MaterialsTypeSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q     = ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await q.Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize).Take(request.PageSize).ToListAsync();
        await FillTenantNamesAsync(items);
        return new PagedResult<MaterialsType> { Items = items, TotalCount = total, PageIndex = request.PageIndex, PageSize = request.PageSize };
    }

    public override async Task<List<MaterialsType>> SearchAllAsync(MaterialsTypeSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId).ToListAsync();
        await FillTenantNamesAsync(items);
        return items;
    }

    protected override IQueryable<MaterialsType> BuildQuery(MaterialsTypeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.VnDescription!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MaterialsTypeRequest r, MaterialsType e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MaterialsType e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MaterialsType e, int status, long userId) { }
}
