using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class PolicyDigitalRepository
    : BaseRepository<PolicyDigital, PolicyDigitalSearchRequest, PolicyDigitalRequest>,
      IPolicyDigitalRepository
{
    public PolicyDigitalRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<PolicyDigital> BuildQuery(PolicyDigitalSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PolicyDigitalRequest r, PolicyDigital e, long userId, bool isNew)
    {
        e.ReaderTypeid = r.ReaderTypeid; e.Maxpage = r.Maxpage; e.Maxsize = r.Maxsize; e.Maxdocument = r.Maxdocument;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(PolicyDigital e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(PolicyDigital e, int status, long userId) { }

    private IQueryable<PolicyDigitalResponse> ProjectWithName(IQueryable<PolicyDigital> q)
        => from p in q
           join r in _context.ReaderTypes on (long?)p.ReaderTypeid equals r.Id into rt
           from r in rt.DefaultIfEmpty()
           select new PolicyDigitalResponse
           {
               Id             = p.Id,
               ReaderTypeid   = p.ReaderTypeid,
               ReaderTypeName = r != null ? r.Name : null,
               Maxpage        = p.Maxpage,
               Maxsize        = p.Maxsize,
               Maxdocument    = p.Maxdocument,
               IsDelete       = p.IsDelete,
               CreatedRowBy   = p.CreatedRowBy,
               UpdateRowBy    = p.UpdateRowBy,
               CreatedRowDate = p.CreatedRowDate,
               UpdatedRowDate = p.UpdatedRowDate,
               TenantId   = p.TenantId,
               PublicId       = p.PublicId
           };

    public async Task<PagedResult<PolicyDigitalResponse>> SearchWithNameAsync(PolicyDigitalSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var q       = ApplyTenantFilter(BuildQuery(request), requestTenantId);
        var total   = await q.CountAsync();
        var items   = await ProjectWithName(q)
            .Skip((Math.Max(request.PageIndex, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();
        await FillTenantNamesAsync(items);
        return new PagedResult<PolicyDigitalResponse>
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize
        };
    }

    public async Task<List<PolicyDigitalResponse>> SearchAllWithNameAsync(PolicyDigitalSearchRequest request)
    {
        var requestTenantId = await ResolveRequestTenantIdAsync(request.TenantId);
        var items = await ProjectWithName(ApplyTenantFilter(BuildQuery(request), requestTenantId)).ToListAsync();
        await FillTenantNamesAsync(items);
        return items;
    }

    public async Task<PolicyDigital> UpsertAsync(PolicyDigitalRequest request)
    {
        var deptId = GetCurrentTenantId();
        var q = _dbSet.Where(x => x.ReaderTypeid == request.ReaderTypeid && x.IsDelete != 2);
        if (deptId != null)
            q = q.Where(x => x.TenantId == deptId);

        var existing = await q.FirstOrDefaultAsync();
        if (existing != null)
            return await UpdateAsync(existing.PublicId, request);

        return await AddAsync(request);
    }
}


