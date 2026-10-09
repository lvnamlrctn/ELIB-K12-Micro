using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class TheodoiBienmucEbookRepository : BaseRepository<TheodoiBienmucEbook, TheodoiBienmucEbookSearchRequest, TheodoiBienmucEbookRequest>
{
    public TheodoiBienmucEbookRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<TheodoiBienmucEbook> BuildQuery(TheodoiBienmucEbookSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.DigId.HasValue)  q = q.Where(x => x.DigId  == r.DigId);
        if (r.UserId.HasValue) q = q.Where(x => x.UserId == r.UserId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(TheodoiBienmucEbookRequest r, TheodoiBienmucEbook e, long userId, bool isNew)
    {
        e.UserId = r.UserId; e.Submited = r.Submited; e.DigId = r.DigId; e.Status = r.Status;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(TheodoiBienmucEbook e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(TheodoiBienmucEbook e, int status, long userId) { }
}
