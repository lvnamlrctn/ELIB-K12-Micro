using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class PermissionRepository : BaseRepository<Permission, PermissionSearchRequest, PermissionRequest>
{
    public PermissionRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Permission> BuildQuery(PermissionSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.UserId.HasValue) q = q.Where(x => x.UserId == r.UserId);
        if (r.ModuleId.HasValue) q = q.Where(x => x.ModuleId == r.ModuleId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PermissionRequest r, Permission e, long userId, bool isNew)
    {
        e.UserId = r.UserId; e.ModuleId = r.ModuleId; e.PortalId = r.PortalId; e.Language = r.Language;
        e.Can_Access = r.Can_Access; e.Can_Add = r.Can_Add; e.Can_Edit = r.Can_Edit;
        e.Can_Delete = r.Can_Delete; e.Can_View = r.Can_View;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Permission e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Permission e, int status, long userId) { }
}
