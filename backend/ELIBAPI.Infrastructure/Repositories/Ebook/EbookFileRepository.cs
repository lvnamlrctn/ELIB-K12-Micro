using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class EbookFileRepository
    : BaseRepository<EbookFile, EbookFileSearchRequest, EbookFileRequest>, IEbookFileRepository
{
    public EbookFileRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public Task<List<EbookFile>> GetFilesWithOldUrlsAsync()
        => _dbSet
            .Where(f => f.IsDelete != 2 && f.Url != null && f.Url.StartsWith("Upload"))
            .AsNoTracking()
            .ToListAsync();

    public async Task<bool> UpdateFileUrlAsync(long id, string newObjectName)
    {
        var affected = await _dbSet
            .Where(f => EF.Property<long>(f, "Id") == id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Url, newObjectName));
        return affected > 0;
    }

    protected override IQueryable<EbookFile> BuildQuery(EbookFileSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Description!.Contains(r.Keyword));
        if (r.EbookId.HasValue)
            q = q.Where(x => x.EbookId == r.EbookId);
        else if (r.EbookPublicId.HasValue)
            q = q.Where(x => _context.EbookItems
                .Where(e => e.PublicId == r.EbookPublicId && e.IsDelete != 2)
                .Select(e => (long?)e.Id)
                .Contains(x.EbookId));
        return q.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(EbookFileRequest r, EbookFile e, long userId, bool isNew)
    {
        e.Url = r.Url; e.Type = r.Type; e.IsConvert = r.IsConvert; e.EbookId = r.EbookId;
        e.CreatedDate = r.CreatedDate; e.FileType = r.FileType; e.FileSize = r.FileSize;
        e.FileExt = r.FileExt; e.Description = r.Description; e.Source = r.Source;
        e.FormatId = r.FormatId; e.CheckSumAlgorithm = r.CheckSumAlgorithm; e.SortOrder = r.SortOrder;
        e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; }
    }

    protected override void SoftDelete(EbookFile e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(EbookFile e, int status, long userId) { }
}
