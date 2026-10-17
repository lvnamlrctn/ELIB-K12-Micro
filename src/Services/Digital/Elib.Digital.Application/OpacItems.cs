using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record OpacItemSearch(
    string? Keyword = null, long? CollectionId = null, long? SubjectId = null, long? TopicId = null, long? TypeId = null, int PageIndex = 1,
    int PageSize = 20);

public sealed record OpacItemSummary(
    Guid PublicId, string Title, string? Author, string? Publisher, string? PublishDate, string? CoverUrl, bool Free, bool AllowDownload,
    long? CollectionId, string? CollectionName);

public sealed record OpacItemDetail(
    Guid PublicId, string Title, string? Author, string? Publisher, string? PublishDate, string? Keyword, string? OtherTitle, string? Page,
    string? OldAuthor, string? Language, string? CoverUrl, bool Free, bool AllowDownload, int? PrintCopies, long? CollectionId,
    string? CollectionName, long? SubjectId, string? SubjectName, long? TopicId, string? TopicName, bool TopicRequiresLogin, long? TypeId,
    string? TypeName, Guid? LinkedBibPublicId);

/// <summary>
/// Duyệt/tìm tài liệu số trên OPAC (ẩn danh, công khai theo host đơn vị) — tự phục vụ bằng chính dữ liệu của digital (quyết định #1 của
/// plan, không qua search service). Chỉ tài liệu Status=Active (LRC: "Xuất bản") hiện ra; ẩn/đã xoá → không tìm thấy.
/// </summary>
public sealed class OpacItems(ICrudDbContext db)
{
    public const int MaxPageSize = 50;

    public async Task<CrudPage<OpacItemSummary>> SearchAsync(OpacItemSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = db.Set<EbookItem>().AsNoTracking().Where(x => x.Status == IHasStatus.Active);
        if (search.CollectionId is { } collection) query = query.Where(x => x.CollectionId == collection);
        if (search.SubjectId is { } subject) query = query.Where(x => x.SubjectId == subject);
        if (search.TopicId is { } topic) query = query.Where(x => x.TopicId == topic);
        if (search.TypeId is { } type) query = query.Where(x => x.TypeId == type);
        var term = string.IsNullOrWhiteSpace(search.Keyword) ? null : search.Keyword.Trim().ToLowerInvariant();
        if (term is not null)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            query = query.Where(x => x.Title.ToLower().Contains(term) || (x.Author != null && x.Author.ToLower().Contains(term))
                || (x.Keyword != null && x.Keyword.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var page = Math.Max(1, search.PageIndex);
        var size = Math.Clamp(search.PageSize, 1, MaxPageSize);
        var ordered = query.OrderByDescending(x => x.Submited).ThenByDescending(x => x.Id);
        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip((page - 1) * size).Take(size)
            .Select(x => new OpacItemSummary(x.PublicId, x.Title, x.Author, x.Publisher, x.PublishDate, x.CoverUrl, x.Free, x.AllowDownload,
                x.CollectionId, db.Set<EbookCollection>().Where(c => c.Id == x.CollectionId).Select(c => c.Name).FirstOrDefault()))
            .ToListAsync(ct);
        return new CrudPage<OpacItemSummary>(items, total, page, size);
    }

    public async Task<OpacItemDetail> GetAsync(Guid publicId, CancellationToken ct)
    {
        var item = await db.Set<EbookItem>().AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId && x.Status == IHasStatus.Active, ct)
            ?? throw new NotFoundException("Tài liệu số", publicId);
        var collectionName = item.CollectionId is { } c
            ? await db.Set<EbookCollection>().AsNoTracking().Where(x => x.Id == c).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var subjectName = item.SubjectId is { } s
            ? await db.Set<EbookSubject>().AsNoTracking().Where(x => x.Id == s).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var topic = item.TopicId is { } t
            ? await db.Set<EbookTopic>().AsNoTracking().Where(x => x.Id == t).Select(x => new { x.Name, x.RequiresLogin }).FirstOrDefaultAsync(ct) : null;
        var typeName = item.TypeId is { } ty
            ? await db.Set<EbookDigType>().AsNoTracking().Where(x => x.Id == ty).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        return new OpacItemDetail(
            item.PublicId, item.Title, item.Author, item.Publisher, item.PublishDate, item.Keyword, item.OtherTitle, item.Page, item.OldAuthor,
            item.Language, item.CoverUrl, item.Free, item.AllowDownload, item.PrintCopies, item.CollectionId, collectionName, item.SubjectId,
            subjectName, item.TopicId, topic?.Name, topic?.RequiresLogin ?? false, item.TypeId, typeName, item.LinkedBibPublicId);
    }
}

/// <summary>Một mục trong cây danh mục công khai (bộ sưu tập/môn loại/chủ đề) — chỉ mục đang hoạt động, dùng làm facet duyệt trên OPAC.</summary>
public sealed record OpacCatalogNode(long Id, string Name, long? ParentId, int Level, int SortOrder, int ChildCount)
{
    public bool HasChildren => ChildCount > 0;
}

/// <summary>Cây bộ sưu tập/môn loại/chủ đề công khai — menu duyệt của OPAC (ẩn danh).</summary>
public sealed class OpacCatalogs(ICrudDbContext db)
{
    public Task<IReadOnlyList<OpacCatalogNode>> CollectionsAsync(CancellationToken ct) => TreeAsync<EbookCollection>(ct);

    public Task<IReadOnlyList<OpacCatalogNode>> SubjectsAsync(CancellationToken ct) => TreeAsync<EbookSubject>(ct);

    public Task<IReadOnlyList<OpacCatalogNode>> TopicsAsync(CancellationToken ct) => TreeAsync<EbookTopic>(ct);

    private async Task<IReadOnlyList<OpacCatalogNode>> TreeAsync<T>(CancellationToken ct) where T : TreeCatalogItem
    {
        var all = await db.Set<T>().AsNoTracking().Where(x => x.Status == IHasStatus.Active).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var counts = all.Where(x => x.ParentId != null).GroupBy(x => x.ParentId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return [.. all.Select(x => new OpacCatalogNode(x.Id, x.Name, x.ParentId, x.Level, x.SortOrder, counts.GetValueOrDefault(x.Id)))];
    }
}
