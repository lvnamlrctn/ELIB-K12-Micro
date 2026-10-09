using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class AttachFileRepository : BaseRepository<AttachFile, AttachFileSearchRequest, AttachFileRequest>, IAttachFileRepository
{
    public AttachFileRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public Task<List<AttachFile>> GetLegacyAsync(long? tenantId)
        => _dbSet.Where(f => f.IsDelete != 2 && f.Url != null && f.Url.StartsWith("Upload")
                          && (!tenantId.HasValue || f.TenantId == tenantId)).AsNoTracking().ToListAsync();

    public async Task<bool> UpdateUrlAsync(long id, string newObjectName)
        => await _dbSet.Where(f => f.Id == id).ExecuteUpdateAsync(s => s.SetProperty(f => f.Url, newObjectName)) > 0;

    protected override IQueryable<AttachFile> BuildQuery(AttachFileSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Name!.Contains(r.Keyword));
        if (r.NewsId.HasValue) q = q.Where(x => x.NewsId == r.NewsId);
        else if (r.NewsPublicId.HasValue)
            q = q.Where(x => _context.News.Where(n => n.PublicId == r.NewsPublicId && n.IsDelete != 2)
                .Select(n => (long?)n.Id).Contains(x.NewsId));
        return q.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(AttachFileRequest r, AttachFile e, long userId, bool isNew)
    {
        e.Name = r.Name; e.Url = r.Url; e.FileSize = r.FileSize; e.NewsId = r.NewsId;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; e.CreatedDate = DateTime.Now; }
    }

    protected override void SoftDelete(AttachFile e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(AttachFile e, int status, long userId) { }
}
