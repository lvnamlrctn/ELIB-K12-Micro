using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class DocumentRepository : BaseRepository<Document, DocumentSearchRequest, DocumentRequest>
{
    public DocumentRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<Document> BuildQuery(DocumentSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId       == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language       == r.Language);
        if (r.DocumentTypeId.HasValue)         q = q.Where(x => x.DocumentTypeId == r.DocumentTypeId);
        if (r.AgencyId.HasValue)               q = q.Where(x => x.AgencyId       == r.AgencyId);
        if (r.TopicId.HasValue)                q = q.Where(x => x.TopicId        == r.TopicId);
        if (r.Status.HasValue && r.Status > 0)  q = q.Where(x => x.Status         == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(DocumentRequest r, Document e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Brief = r.Brief; e.DocumentTypeId = r.DocumentTypeId; e.AgencyId = r.AgencyId;
        e.IssueDate = r.IssueDate; e.ExpireDate = r.ExpireDate; e.CreatedDate = r.CreatedDate;
        e.TypeId = r.TypeId; e.Status = r.Status; e.Sign = r.Sign; e.TopicId = r.TopicId;
        e.GovDocNumber = r.GovDocNumber; e.PortalId = r.PortalId; e.Language = r.Language;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(Document e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(Document e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
