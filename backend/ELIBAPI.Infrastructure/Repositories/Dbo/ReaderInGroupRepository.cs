using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class ReaderInGroupRepository : BaseRepository<ReaderInGroup, ReaderInGroupSearchRequest, ReaderInGroupRequest>
{
    public ReaderInGroupRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<ReaderInGroup> BuildQuery(ReaderInGroupSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.GroupReaderId.HasValue) q = q.Where(x => x.GroupReaderId == r.GroupReaderId);
        if (r.ReaderId.HasValue)      q = q.Where(x => x.ReaderId      == r.ReaderId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(ReaderInGroupRequest r, ReaderInGroup e, long userId, bool isNew)
    {
        e.ReaderId = r.ReaderId; e.GroupReaderId = r.GroupReaderId;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(ReaderInGroup e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(ReaderInGroup e, int status, long userId) { }
}
