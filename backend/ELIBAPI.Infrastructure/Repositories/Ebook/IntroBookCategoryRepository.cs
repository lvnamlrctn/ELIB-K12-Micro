using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class IntroBookCategoryRepository : BaseRepository<IntroBookCategory, IntroBookCategorySearchRequest, IntroBookCategoryRequest>
{
    public IntroBookCategoryRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<IntroBookCategory> BuildQuery(IntroBookCategorySearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.ParentId.HasValue)               q = q.Where(x => x.ParentId == r.ParentId);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status   == r.Status);
        return q.OrderBy(x => x.Order).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(IntroBookCategoryRequest r, IntroBookCategory e, long userId, bool isNew)
    {
        e.Name = r.Name; e.ParentId = r.ParentId; e.Level = r.Level; e.Status = r.Status;
        e.Order = r.Order; e.PortalId = r.PortalId; e.Language = r.Language; e.IsLogin = r.IsLogin;
        e.Description = r.Description; e.Keyword = r.Keyword; e.PageTitle = r.PageTitle;
        e.MetaDescription = r.MetaDescription; e.Link = r.Link;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(IntroBookCategory e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(IntroBookCategory e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
