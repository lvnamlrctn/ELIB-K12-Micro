using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class AbMoveDetailRepository : BaseRepository<AbMoveDetail, AbMoveDetailSearchRequest, AbMoveDetailRequest>
{
    public AbMoveDetailRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<AbMoveDetail> BuildQuery(AbMoveDetailSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(AbMoveDetailRequest r, AbMoveDetail e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(AbMoveDetail e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(AbMoveDetail e, int status, long userId) { }
}
