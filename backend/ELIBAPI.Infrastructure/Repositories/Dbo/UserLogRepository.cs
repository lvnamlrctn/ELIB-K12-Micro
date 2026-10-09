using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class UserLogRepository(ELIBAPIDbContext db) : IUserLogRepository
{
    private IQueryable<UserLogResponse> BuildQuery(UserLogSearchRequest r, long? requestTenantId, long? jwtTenantId, bool isPrivileged)
    {
        var logs = db.UserLogs.AsQueryable();
        if (!string.IsNullOrEmpty(r.Keyword))
        {
            logs = logs.Where(x => (x.Action != null && x.Action.Contains(r.Keyword)) ||
                                   (x.Object != null && x.Object.Contains(r.Keyword)));
        }
        if (!string.IsNullOrEmpty(r.PortalId))    logs = logs.Where(x => x.PortalId    == r.PortalId);
        if (!string.IsNullOrEmpty(r.ActionType))  logs = logs.Where(x => x.ActionType  == r.ActionType);
        if (!string.IsNullOrEmpty(r.Application)) logs = logs.Where(x => x.Application == r.Application);
        if (r.UserId.HasValue)                    logs = logs.Where(x => x.UserId       == r.UserId);
        if (!string.IsNullOrEmpty(r.SubmitedFrom) && DateTime.TryParse(r.SubmitedFrom, out var dFrom))
            logs = logs.Where(x => x.Submited >= dFrom);
        if (!string.IsNullOrEmpty(r.SubmitedTo) && DateTime.TryParse(r.SubmitedTo, out var dTo))
            logs = logs.Where(x => x.Submited <= dTo);

        logs = isPrivileged
            ? (requestTenantId.HasValue ? logs.Where(x => x.TenantId == requestTenantId || x.TenantId == null) : logs)
            : logs.Where(x => x.TenantId == jwtTenantId);

        return from ul in logs
               join u in db.Users on ul.UserId equals u.Id into ug
               from u in ug.DefaultIfEmpty()
               join t in db.Tenants on ul.TenantId equals (long?)t.Id into tg
               from t in tg.DefaultIfEmpty()
               orderby ul.Id descending
               select new UserLogResponse
               {
                   Id          = ul.Id,
                   UserId      = ul.UserId,
                   FullName    = u.FullName,
                   ActionType  = ul.ActionType,
                   Object      = ul.Object,
                   Action      = ul.Action,
                   Submited    = ul.Submited,
                   Ip          = ul.Ip,
                   Application = ul.Application,
                   PortalId    = ul.PortalId,
                   TenantId    = ul.TenantId,
                   TenantName  = t != null ? t.Name : null
               };
    }

    public async Task<PagedResult<UserLogResponse>> SearchAsync(UserLogSearchRequest r, long? jwtTenantId, bool isPrivileged)
    {
        var requestTenantId = await TenantScopeHelper.ResolveRequestTenantIdAsync(db, r.TenantId, jwtTenantId, isPrivileged);
        var q     = BuildQuery(r, requestTenantId, jwtTenantId, isPrivileged);
        var total = await q.CountAsync();
        var items = await q.Skip((Math.Max(r.PageIndex, 1) - 1) * r.PageSize).Take(r.PageSize).ToListAsync();
        return new PagedResult<UserLogResponse> { Items = items, TotalCount = total, PageIndex = r.PageIndex, PageSize = r.PageSize };
    }

    public async Task<List<UserLogResponse>> SearchAllAsync(UserLogSearchRequest r, long? jwtTenantId, bool isPrivileged)
    {
        var requestTenantId = await TenantScopeHelper.ResolveRequestTenantIdAsync(db, r.TenantId, jwtTenantId, isPrivileged);
        return await BuildQuery(r, requestTenantId, jwtTenantId, isPrivileged).ToListAsync();
    }
}

