using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class BibWorksheetRepository : BaseRepository<BibWorksheet, BibWorksheetSearchRequest, BibWorksheetRequest>
{
    public BibWorksheetRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<BibWorksheet> BuildQuery(BibWorksheetSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (r.BibTypeId.HasValue) q = q.Where(x => x.Bib_Type_Id == r.BibTypeId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(BibWorksheetRequest r, BibWorksheet e, long userId, bool isNew)
    {
        ELIBAPI.Core.Common.PropertyMapper.Map(r, e);
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(BibWorksheet e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(BibWorksheet e, int status, long userId) { }
}
