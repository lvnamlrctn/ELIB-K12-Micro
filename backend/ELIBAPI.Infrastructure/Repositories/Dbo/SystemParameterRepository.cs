using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class SystemParameterRepository : BaseRepository<SystemParameter, SystemParameterSearchRequest, SystemParameterRequest>
{
    public SystemParameterRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<SystemParameter> BuildQuery(SystemParameterSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  { var kw = r.Keyword.ToLower(); q = q.Where(x => x.Code!.ToLower().Contains(kw) || x.DescriptionVn!.ToLower().Contains(kw)); }
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(SystemParameterRequest r, SystemParameter e, long userId, bool isNew)
    {
        e.Code = r.Code; e.DescriptionVn = r.DescriptionVn; e.DescriptionEn = r.DescriptionEn;
        e.Value = r.Value; e.PortalId = r.PortalId; e.Type = r.Type; e.Language = r.Language;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(SystemParameter e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(SystemParameter e, int status, long userId) { }
}
