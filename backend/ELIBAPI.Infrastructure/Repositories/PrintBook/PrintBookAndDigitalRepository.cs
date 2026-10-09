using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class PrintBookAndDigitalRepository : BaseRepository<PrintBookAndDigital, PrintBookAndDigitalSearchRequest, PrintBookAndDigitalRequest>
{
    public PrintBookAndDigitalRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<PrintBookAndDigital> BuildQuery(PrintBookAndDigitalSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (r.BibId.HasValue)   q = q.Where(x => x.BibId == r.BibId);
        if (r.EbookId.HasValue) q = q.Where(x => x.EbookId == r.EbookId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(PrintBookAndDigitalRequest r, PrintBookAndDigital e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(PrintBookAndDigital e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(PrintBookAndDigital e, int status, long userId) { }
}
