using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ELIBAPI.Infrastructure.Repositories;

public class DocumentFileRepository : BaseRepository<DocumentFile, DocumentFileSearchRequest, DocumentFileRequest>
{
    public DocumentFileRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    protected override IQueryable<DocumentFile> BuildQuery(DocumentFileSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (r.DocumentId.HasValue)            q = q.Where(x => x.DocumentId == r.DocumentId);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(DocumentFileRequest r, DocumentFile e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Url = r.Url; e.FileSize = r.FileSize; e.DocumentId = r.DocumentId;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(DocumentFile e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(DocumentFile e, int status, long userId) { }
}
