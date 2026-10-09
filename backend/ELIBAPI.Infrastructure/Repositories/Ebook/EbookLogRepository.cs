using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookLogRepository : BaseRepository<EbookLog, EbookLogSearchRequest, EbookLogRequest>
{
    public EbookLogRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<EbookLog> BuildQuery(EbookLogSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.ReaderId.HasValue) q = q.Where(x => x.ReaderId == r.ReaderId);
        if (r.Bookid.HasValue)   q = q.Where(x => x.Bookid   == r.Bookid);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EbookLogRequest r, EbookLog e, long userId, bool isNew)
    {
        e.ReaderId = r.ReaderId; e.Cardnumber = r.Cardnumber; e.Submited = r.Submited;
        e.Bookid = r.Bookid; e.Page = r.Page; e.Size = r.Size; e.Type = r.Type; e.Ip = r.Ip;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EbookLog e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookLog e, int status, long userId) { }
}
