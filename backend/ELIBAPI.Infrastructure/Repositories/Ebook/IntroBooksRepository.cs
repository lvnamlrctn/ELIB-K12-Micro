using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class IntroBooksRepository : BaseRepository<IntroBooks, IntroBooksSearchRequest, IntroBooksRequest>
{
    public IntroBooksRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<IntroBooks> BuildQuery(IntroBooksSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))      q = q.Where(x => x.Title.Contains(r.Keyword));
        if (r.IntroBookCategoryId.HasValue)         q = q.Where(x => x.IntroBookCategoryId == r.IntroBookCategoryId);
        return q.OrderBy(x => x.Order).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(IntroBooksRequest r, IntroBooks e, long userId, bool isNew)
    {
        e.Title = r.Title; e.Brief = r.Brief; e.Noidung = r.Noidung; e.Image = r.Image;
        e.Submited = r.Submited; e.Order = r.Order; e.IntroBookCategoryId = r.IntroBookCategoryId; e.BibId = r.BibId;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(IntroBooks e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(IntroBooks e, int status, long userId) { }
}
