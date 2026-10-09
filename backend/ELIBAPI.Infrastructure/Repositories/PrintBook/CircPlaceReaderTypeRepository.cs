using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class CircPlaceReaderTypeRepository : BaseRepository<CircPlaceReaderType, CircPlaceReaderTypeSearchRequest, CircPlaceReaderTypeRequest>
{
    public CircPlaceReaderTypeRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<CircPlaceReaderType> BuildQuery(CircPlaceReaderTypeSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.CircPlaceId.HasValue && r.CircPlaceId > 0) q = q.Where(x => x.CircPlaceId == r.CircPlaceId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(CircPlaceReaderTypeRequest r, CircPlaceReaderType e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(CircPlaceReaderType e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(CircPlaceReaderType e, int status, long userId) { }
}
