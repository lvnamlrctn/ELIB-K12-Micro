using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class GeographicAreasRepository : BaseRepository<GeographicAreas, GeographicAreasSearchRequest, GeographicAreasRequest>
{
    public GeographicAreasRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<GeographicAreas> BuildQuery(GeographicAreasSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.VnDescription!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(GeographicAreasRequest r, GeographicAreas e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(GeographicAreas e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(GeographicAreas e, int status, long userId) { }
}
