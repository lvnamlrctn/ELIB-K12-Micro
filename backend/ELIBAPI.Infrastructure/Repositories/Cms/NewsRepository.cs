using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Repositories;

public class NewsRepository : BaseRepository<News, NewsSearchRequest, NewsRequest>, INewsRepository
{
    public NewsRepository(ELIBAPIDbContext ctx, IHttpContextAccessor http) : base(ctx, http) { }

    public override async Task<PagedResult<News>> SearchAsync(NewsSearchRequest request)
    {
        var result = await base.SearchAsync(request);
        await FillCategoryNamesAsync(result.Items);
        return result;
    }

    public override async Task<List<News>> SearchAllAsync(NewsSearchRequest request)
    {
        var items = await base.SearchAllAsync(request);
        await FillCategoryNamesAsync(items);
        return items;
    }

    private async Task FillCategoryNamesAsync(IEnumerable<News> items)
    {
        var ids = items
            .Where(n => n.CategoryId.HasValue)
            .Select(n => n.CategoryId!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return;

        var cats = await _context.Set<Category>()
            .Where(c => ids.Contains(c.Id) && c.IsDelete != 2)
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        foreach (var n in items.Where(n => n.CategoryId.HasValue))
            n.CategoryName = cats.GetValueOrDefault(n.CategoryId!.Value);
    }

    protected override IQueryable<News> BuildQuery(NewsSearchRequest r)
    {
        var q = _dbSet.Where(x => x.IsDelete != 2);
        if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Title!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.CategoryId.HasValue && r.CategoryId > 0) q = q.Where(x => x.CategoryId == r.CategoryId);
        if (!string.IsNullOrEmpty(r.Types)) q = q.Where(x => x.Types == r.Types);
        if (r.Status.HasValue && r.Status >0) q = q.Where(x => x.Status == r.Status);
        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(NewsRequest r, News e, long userId, bool isNew)
    {
        e.Title = r.Title;
        e.Brief = r.Brief;
        e.Content = r.Content;
        e.Images = r.Images;
        e.Thumb = r.Thumb;
        e.StartTime = r.StartTime;
        e.EndTime = r.EndTime;
        e.CategoryId = r.CategoryId;
        e.EventId = r.EventId;
        e.PortalId = r.PortalId;
        e.Language = r.Language;
        e.Keyword = r.Keyword;
        e.Author = r.Author;
        e.Source = r.Source;
        e.Types = r.Types;
        e.Clourse = r.Clourse;
        e.Status = r.Status;
        e.AllowComment = r.AllowComment;
        e.MetaTitle = r.MetaTitle;
        e.MetaKeyword = r.MetaKeyword;
        e.MetaDescription = r.MetaDescription;
        e.MaleAudio = r.MaleAudio;
        e.FaleAudio = r.FaleAudio;
        e.ContentAudio = r.ContentAudio;
        e.BriefAudio = r.BriefAudio;
        e.TitleAudio = r.TitleAudio;
        e.UpdateRowBy = userId;
        e.UpdatedRowDate = DateTime.Now;
        if (isNew) { e.CreatedRowBy = userId; e.CreatedRowDate = DateTime.Now; e.TotalView = 0; }
    }

    public Task<List<News>> GetNewsWithOldImagesAsync(string minioPublicBaseUrl)
        => _dbSet
            .Where(n => n.IsDelete != 2
                && ((n.Images != null && !n.Images.StartsWith(minioPublicBaseUrl)
                                      && !EF.Functions.Like(n.Images, "[0-9][0-9][0-9][0-9]/%"))
                 || (n.Thumb  != null && !n.Thumb.StartsWith(minioPublicBaseUrl)
                                      && !EF.Functions.Like(n.Thumb,  "[0-9][0-9][0-9][0-9]/%"))))
            .AsNoTracking()
            .ToListAsync();

    public async Task<bool> UpdateNewsImagesAsync(long id, string? images, string? thumb)
    {
        int affected = 0;
        if (images != null)
            affected += await _dbSet.Where(n => EF.Property<long>(n, "Id") == id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Images, images));
        if (thumb != null)
            affected += await _dbSet.Where(n => EF.Property<long>(n, "Id") == id)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.Thumb, thumb));
        return affected > 0;
    }

    protected override void SoftDelete(News e, long userId)
    { e.IsDelete = 2; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }

    protected override void SetStatus(News e, int status, long userId)
    { e.Status = status; e.UpdateRowBy = userId; e.UpdatedRowDate = DateTime.Now; }
}
