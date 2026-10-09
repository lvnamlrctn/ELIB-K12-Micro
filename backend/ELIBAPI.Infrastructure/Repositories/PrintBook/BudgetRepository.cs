using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class BudgetRepository : BaseRepository<Budget, BudgetSearchRequest, BudgetRequest>
{
    public BudgetRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    // Dữ liệu Budget hiện có TenantId = NULL (dùng chung) — dùng IncludeNull như StoreRepository
    // để tránh ApplyTenantFilter mặc định trả rỗng cho mọi tenant.
    public override async Task<PagedResult<Budget>> SearchAsync(BudgetSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q     = ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId);
        var total = await q.CountAsync();
        var items = await q.Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize).Take(request.PageSize).ToListAsync();
        await FillTenantNamesAsync(items);
        return new PagedResult<Budget> { Items = items, TotalCount = total, PageIndex = request.PageIndex, PageSize = request.PageSize };
    }

    public override async Task<List<Budget>> SearchAllAsync(BudgetSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ApplyTenantFilterIncludeNull(BuildQuery(request), requestTenantId).ToListAsync();
        await FillTenantNamesAsync(items);
        return items;
    }

    protected override IQueryable<Budget> BuildQuery(BudgetSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (r.Status.HasValue && r.Status > 0) q = q.Where(x => x.Status == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(BudgetRequest r, Budget e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Budget e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Budget e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
