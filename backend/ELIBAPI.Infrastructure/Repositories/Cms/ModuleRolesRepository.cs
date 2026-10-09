using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ModuleRolesRepository : BaseRepository<ModuleRoles, ModuleRolesSearchRequest, ModuleRolesRequest>
{
    public ModuleRolesRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ModuleRoles> BuildQuery(ModuleRolesSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.RolesId.HasValue) q = q.Where(x => x.RolesId == r.RolesId);
        if (r.ModuleId.HasValue) q = q.Where(x => x.ModuleId == r.ModuleId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ModuleRolesRequest r, ModuleRoles e, long userId, bool isNew)
    {
        e.RolesId = r.RolesId; e.ModuleId = r.ModuleId; e.PortalId = r.PortalId;
        e.Language = r.Language; e.Can_access = r.Can_access; e.Can_add = r.Can_add;
        e.Can_edit = r.Can_edit; e.Can_delete = r.Can_delete; e.Can_view = r.Can_view;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(ModuleRoles e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ModuleRoles e, int status, long userId) { }
}
