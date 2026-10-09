using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookItemXmlRepository : BaseRepository<EbookItemXml, EbookItemXmlSearchRequest, EbookItemXmlRequest>
{
    public EbookItemXmlRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<EbookItemXml> BuildQuery(EbookItemXmlSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Title!.Contains(r.Keyword));
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EbookItemXmlRequest r, EbookItemXml e, long userId, bool isNew)
    {
        e.Title = r.Title; e.Author = r.Author; e.Publisher = r.Publisher;
        e.PublishDate = r.PublishDate; e.Keyword = r.Keyword; e.Xml = r.Xml;
        e.OtherTitle = r.OtherTitle; e.Page = r.Page; e.OldAuthor = r.OldAuthor;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EbookItemXml e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookItemXml e, int status, long userId) { }
}
