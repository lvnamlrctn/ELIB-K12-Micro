using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class BookOutRepository : BaseRepository<BookOut, BookOutSearchRequest, BookOutRequest>
{
    public BookOutRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<BookOut> BuildQuery(BookOutSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Barcode!.Contains(r.Keyword));
        if (r.ReaderId.HasValue) q = q.Where(x => x.ReaderId == r.ReaderId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(BookOutRequest r, BookOut e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(BookOut e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(BookOut e, int status, long userId) { }
}
