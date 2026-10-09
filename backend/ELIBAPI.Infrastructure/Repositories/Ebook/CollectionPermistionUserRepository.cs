using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class CollectionPermistionUserRepository : BaseRepository<CollectionPermistionUser, CollectionPermistionUserSearchRequest, CollectionPermistionUserRequest>
{
    public CollectionPermistionUserRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<CollectionPermistionUser> BuildQuery(CollectionPermistionUserSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.CollectionId.HasValue) q = q.Where(x => x.CollectionId == r.CollectionId);
        if (r.GroupUserId.HasValue)  q = q.Where(x => x.GroupUserId  == r.GroupUserId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(CollectionPermistionUserRequest r, CollectionPermistionUser e, long userId, bool isNew)
    {
        e.CollectionId = r.CollectionId; e.GroupUserId = r.GroupUserId;
        e.CanAdd = r.CanAdd; e.CanEdit = r.CanEdit; e.CanDelete = r.CanDelete; e.CanView = r.CanView;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(CollectionPermistionUser e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(CollectionPermistionUser e, int status, long userId) { }
}
