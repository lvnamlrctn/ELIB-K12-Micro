using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class MarcFieldRepository : BaseRepository<MarcField, MarcFieldSearchRequest, MarcFieldRequest>
{
    public MarcFieldRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    // Toàn bộ MarcField có TenantId = NULL (danh mục MARC dùng chung) — dùng bản IncludeNull
    // giống pattern MarcTypeRepository để danh mục dùng chung vẫn hiển thị cho user có TenantId.
    public override async Task<PagedResult<MarcField>> SearchAsync(MarcFieldSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q     = ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await q.Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize).Take(request.PageSize).ToListAsync();
        await FillTenantNamesAsync(items);
        return new PagedResult<MarcField> { Items = items, TotalCount = total, PageIndex = request.PageIndex, PageSize = request.PageSize };
    }

    public override async Task<List<MarcField>> SearchAllAsync(MarcFieldSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId).ToListAsync();
        await FillTenantNamesAsync(items);
        return items;
    }

    protected override IQueryable<MarcField> BuildQuery(MarcFieldSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Vndescription!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MarcFieldRequest r, MarcField e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MarcField e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MarcField e, int status, long userId) { }
}
