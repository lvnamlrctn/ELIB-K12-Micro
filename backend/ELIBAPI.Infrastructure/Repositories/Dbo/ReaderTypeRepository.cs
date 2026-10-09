using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ReaderTypeRepository : BaseRepository<ReaderType, ReaderTypeSearchRequest, ReaderTypeRequest>
{
    public ReaderTypeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ReaderType> BuildQuery(ReaderTypeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ReaderTypeRequest r, ReaderType e, long userId, bool isNew)
    {
        e.Name = r.Name; e.PortalId = r.PortalId; e.Language = r.Language;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(ReaderType e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ReaderType e, int status, long userId) { }
}
