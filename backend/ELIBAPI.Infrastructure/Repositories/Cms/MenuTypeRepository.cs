using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class MenuTypeRepository : BaseRepository<MenuType, MenuTypeSearchRequest, MenuTypeRequest>, IMenuTypeRepository
{
    public MenuTypeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public async Task<bool> CheckCodeExistsAsync(string code, Guid? excludePublicId = null)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2 && x.Code == code);
        if (excludePublicId.HasValue) q = q.Where(x => x.PublicId != excludePublicId.Value);
        return await q.AnyAsync();
    }

    protected override IQueryable<MenuType> BuildQuery(MenuTypeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(MenuTypeRequest r, MenuType e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Description = r.Description; e.Language = r.Language;
        e.PortalId = r.PortalId; e.Code = r.Code;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(MenuType e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(MenuType e, int status, long userId) { }
}
