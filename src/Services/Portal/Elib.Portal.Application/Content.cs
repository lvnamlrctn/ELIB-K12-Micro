using System.Linq.Expressions;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Portal.Application;

public sealed record NewsCategoryRequest(
    string Name, long? ParentId = null, int? SortOrder = null, int? Status = null, string? Language = null, string? Description = null, string? Keyword = null,
    string? PageTitle = null, string? MetaDescription = null, string? Link = null, bool RequiresLogin = false) : IPortalTreeRequest;

public sealed record NewsCategoryDto(
    long Id, Guid PublicId, string Name, long? ParentId, int Level, int SortOrder, int Status, string? Language, string? Description, string? Keyword, string? PageTitle,
    string? MetaDescription, string? Link, bool RequiresLogin, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Chuyên mục tin (LRC: CategoryController, quyền PORTAL_CATEGORIES).</summary>
public sealed class NewsCategoryResource(ICrudDbContext db) : PortalTreeResource<NewsCategoryResource, NewsCategory, NewsCategoryRequest, NewsCategoryDto>(db)
{
    protected override string EntityName => "Chuyên mục tin";

    protected override string? Describe(NewsCategory entity) => entity.Name;

    protected override Expression<Func<NewsCategory, NewsCategoryDto>> Projection => x => new NewsCategoryDto(
        x.Id, x.PublicId, x.Name, x.ParentId, x.Level, x.SortOrder, x.Status, x.Language, x.Description, x.Keyword, x.PageTitle, x.MetaDescription, x.Link,
        x.RequiresLogin, x.CreatedAt, x.UpdatedAt);

    protected override NewsCategory NewEntity(NewsCategoryRequest r) => NewsCategory.Create(ToFields(r));

    protected override void ApplyFields(NewsCategory entity, NewsCategoryRequest r) => entity.Update(ToFields(r));

    protected override async Task EnsureDeletableAsync(NewsCategory entity, CancellationToken ct)
    {
        await base.EnsureDeletableAsync(entity, ct);
        if (await Db.Set<News>().AnyAsync(n => n.CategoryId == entity.Id, ct))
            throw new ConflictException("CATEGORY_HAS_NEWS", $"Chuyên mục '{entity.Name}' còn tin, không xoá được.");
        if (await Db.Set<Menu>().AnyAsync(m => m.LinkType == MenuLinkTypes.Category && m.TargetId == entity.Id, ct))
            throw new ConflictException("CATEGORY_IN_MENU", $"Chuyên mục '{entity.Name}' đang được dùng trong menu.");
    }

    private static NewsCategoryFields ToFields(NewsCategoryRequest r) =>
        new(r.Name, r.Language, r.Description, r.Keyword, r.PageTitle, r.MetaDescription, r.Link, r.RequiresLogin);
}

public sealed record NewsEventRequest(string Name, string? Language = null);

public sealed record NewsEventDto(long Id, Guid PublicId, string Name, string? Language, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Sự kiện (LRC: EventNewsController, quyền PORTAL_EVENTS).</summary>
public sealed class NewsEventResource(ICrudDbContext db) : CrudResource<NewsEventResource, NewsEvent, CrudSearch, NewsEventRequest, NewsEventDto>(db)
{
    protected override string EntityName => "Sự kiện";

    protected override string? Describe(NewsEvent entity) => entity.Name;

    protected override Expression<Func<NewsEvent, NewsEventDto>> Projection => x => new NewsEventDto(x.Id, x.PublicId, x.Name, x.Language, x.CreatedAt, x.UpdatedAt);

    protected override NewsEvent Create(NewsEventRequest r) => NewsEvent.Create(r.Name, r.Language);

    protected override void Update(NewsEvent entity, NewsEventRequest r) => entity.Update(r.Name, r.Language);

    protected override IQueryable<NewsEvent> Filter(IQueryable<NewsEvent> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<NewsEvent> Order(IQueryable<NewsEvent> query) => query.OrderBy(x => x.Name);

    protected override async Task EnsureDeletableAsync(NewsEvent entity, CancellationToken ct)
    {
        if (await Db.Set<News>().AnyAsync(n => n.EventId == entity.Id, ct))
            throw new ConflictException("EVENT_HAS_NEWS", $"Sự kiện '{entity.Name}' còn tin, không xoá được.");
    }
}

public sealed class NewsSearch : CrudSearch
{
    public long? CategoryId { get; set; }
    public long? EventId { get; set; }
    public string? Language { get; set; }
}

public sealed record NewsRequest(
    string Title, string? Slug = null, string? Brief = null, string? Content = null, string? Images = null, string? Thumb = null, DateTimeOffset? StartTime = null,
    DateTimeOffset? EndTime = null, long? CategoryId = null, long? EventId = null, string? Language = null, string? Keyword = null, string? Author = null,
    string? Source = null, string? Types = null, bool Featured = false, bool AllowComment = false, string? MetaTitle = null, string? MetaKeyword = null,
    string? MetaDescription = null, int Status = IHasStatus.Inactive);

public sealed record NewsDto(
    long Id, Guid PublicId, int Status, string Title, string Slug, string? Brief, string? Content, string? Images, string? Thumb, DateTimeOffset? StartTime,
    DateTimeOffset? EndTime, DateTimeOffset? PublishedAt, long? CategoryId, string? CategoryName, long? EventId, string? Language, string? Keyword, string? Author,
    string? Source, string? Types, bool Featured, bool AllowComment, int TotalView, string? MetaTitle, string? MetaKeyword, string? MetaDescription,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// Tin tức (LRC: NewsController, quyền PORTAL_NEWS). Nội dung HTML được làm sạch ở đây trước khi lưu (<see cref="IHtmlCleaner"/>); slug sinh từ
/// tiêu đề nếu để trống và luôn duy nhất trong đơn vị. Đăng/gỡ qua ChangeStatus cần quyền publish (xem endpoint).
/// </summary>
public sealed class NewsResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options, IHtmlCleaner cleaner, TimeProvider clock, IActorPermissions permissions)
    : CrudResource<NewsResource, News, NewsSearch, NewsRequest, NewsDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Tin tức";

    protected override string? Describe(News entity) => entity.Title;

    protected override IReadOnlyCollection<string> DiffIgnored => ["content", "totalView"];

    protected override Expression<Func<News, NewsDto>> Projection => x => new NewsDto(
        x.Id, x.PublicId, x.Status, x.Title, x.Slug, x.Brief, x.Content, x.Images, x.Thumb, x.StartTime, x.EndTime, x.PublishedAt, x.CategoryId,
        Db.Set<NewsCategory>().Where(c => c.Id == x.CategoryId).Select(c => c.Name).FirstOrDefault(), x.EventId, x.Language, x.Keyword, x.Author, x.Source, x.Types,
        x.Featured, x.AllowComment, x.TotalView, x.MetaTitle, x.MetaKeyword, x.MetaDescription, x.CreatedAt, x.UpdatedAt);

    protected override News Create(NewsRequest request) => throw new NotSupportedException("Tạo qua CreateAsync (cần slug duy nhất).");

    protected override async Task<News> CreateAsync(NewsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Status == IHasStatus.Active) await PublishGuard.RequireAsync(permissions, "PORTAL_NEWS", ct);
        var fields = await ToFieldsAsync(request, null, ct);
        return News.Create(fields, _options.MediaPublicPrefix, tenant.RequireTenantId(), clock.GetUtcNow());
    }

    protected override void Update(News entity, NewsRequest request) => throw new NotSupportedException("Sửa qua UpdateAsync (cần slug duy nhất).");

    protected override async Task UpdateAsync(News entity, NewsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (entity.Status == IHasStatus.Active || request.Status == IHasStatus.Active) await PublishGuard.RequireAsync(permissions, "PORTAL_NEWS", ct);
        entity.Update(await ToFieldsAsync(request, entity, ct), _options.MediaPublicPrefix, tenant.RequireTenantId(), clock.GetUtcNow());
    }

    protected override async Task ValidateAsync(News entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Slug == entity.Slug && x.Id != entity.Id, ct))
            throw new ConflictException("NEWS_SLUG_EXISTS", $"Đường dẫn '{entity.Slug}' đã dùng cho tin khác.");
        if (entity.CategoryId is { } category && !await Db.Set<NewsCategory>().AnyAsync(c => c.Id == category, ct))
            throw new BusinessRuleException("CATEGORY_NOT_FOUND", "Chuyên mục đã chọn không còn.");
        if (entity.EventId is { } eventId && !await Db.Set<NewsEvent>().AnyAsync(e => e.Id == eventId, ct))
            throw new BusinessRuleException("EVENT_NOT_FOUND", "Sự kiện đã chọn không còn.");
    }

    protected override IQueryable<News> Filter(IQueryable<News> query, NewsSearch search)
    {
        if (search.CategoryId is { } category) query = query.Where(x => x.CategoryId == category);
        if (search.EventId is { } eventId) query = query.Where(x => x.EventId == eventId);
        if (search.Status is { } status) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search.Language)) query = query.Where(x => x.Language == search.Language);
        // Tìm không dấu theo từng từ (mọi từ phải có) trên cột đã bỏ dấu.
        foreach (var token in TextFold.Tokens(search.Keyword))
        {
            var t = token;
            query = query.Where(x => x.SearchText.Contains(t));
        }
        return query;
    }

    protected override IOrderedQueryable<News> Order(IQueryable<News> query) => query.OrderByDescending(x => x.PublishedAt ?? x.CreatedAt).ThenByDescending(x => x.Id);

    protected override async Task OnSavingAsync(News entity, CrudChange change, CancellationToken ct)
    {
        if (change == CrudChange.StatusChanged) await PublishGuard.RequireAsync(permissions, "PORTAL_NEWS", ct);
    }

    protected override async Task EnsureDeletableAsync(News entity, CancellationToken ct)
    {
        if (entity.Status == IHasStatus.Active) await PublishGuard.RequireAsync(permissions, "PORTAL_NEWS", ct);
        // Tệp đính kèm và bình luận là con của tin: xoá cùng lần lưu.
        Db.Set<NewsAttachment>().RemoveRange(await Db.Set<NewsAttachment>().Where(a => a.NewsId == entity.Id).ToListAsync(ct));
        Db.Set<NewsComment>().RemoveRange(await Db.Set<NewsComment>().Where(c => c.NewsId == entity.Id).ToListAsync(ct));
    }

    private async Task<NewsFields> ToFieldsAsync(NewsRequest r, News? existing, CancellationToken ct)
    {
        var slug = string.IsNullOrWhiteSpace(r.Slug) ? await UniqueSlugAsync(Slugger.From(r.Title, "tin"), existing?.Id, ct) : r.Slug.Trim().ToLowerInvariant();
        return new NewsFields(
            r.Title, slug, r.Brief, cleaner.Clean(r.Content, tenant.RequireTenantId()), r.Images, r.Thumb, r.StartTime, r.EndTime, r.CategoryId, r.EventId, r.Language, r.Keyword,
            r.Author, r.Source, r.Types, r.Featured, r.AllowComment, r.MetaTitle, r.MetaKeyword, r.MetaDescription, r.Status);
    }

    internal async Task<string> UniqueSlugAsync(string baseSlug, long? exceptId, CancellationToken ct)
    {
        var slug = baseSlug;
        for (var n = 2; await Set.AnyAsync(x => x.Slug == slug && x.Id != exceptId, ct); n++)
            slug = $"{baseSlug}-{n}";
        return slug;
    }
}

public sealed class NewsChildSearch : CrudSearch
{
    public long? NewsId { get; set; }
}

public sealed record NewsAttachmentRequest(long NewsId, string Name, string Url, double? FileSize = null);

public sealed record NewsAttachmentDto(long Id, Guid PublicId, long NewsId, string Name, string Url, double? FileSize, DateTimeOffset CreatedAt);

/// <summary>Tệp đính kèm của tin (LRC: AttachFileController, cùng quyền PORTAL_NEWS — là con của tin).</summary>
public sealed class NewsAttachmentResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options)
    : CrudResource<NewsAttachmentResource, NewsAttachment, NewsChildSearch, NewsAttachmentRequest, NewsAttachmentDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Tệp đính kèm";

    protected override string? Describe(NewsAttachment entity) => entity.Name;

    protected override Expression<Func<NewsAttachment, NewsAttachmentDto>> Projection => x => new NewsAttachmentDto(x.Id, x.PublicId, x.NewsId, x.Name, x.Url, x.FileSize, x.CreatedAt);

    protected override NewsAttachment Create(NewsAttachmentRequest r) =>
        NewsAttachment.Create(r.NewsId, r.Name, r.Url, r.FileSize, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override void Update(NewsAttachment entity, NewsAttachmentRequest r) =>
        entity.Update(r.Name, r.Url, r.FileSize, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override async Task ValidateAsync(NewsAttachment entity, CancellationToken ct)
    {
        if (!await Db.Set<News>().AnyAsync(n => n.Id == entity.NewsId, ct)) throw new BusinessRuleException("NEWS_NOT_FOUND", "Tin đã chọn không còn.");
    }

    protected override IQueryable<NewsAttachment> Filter(IQueryable<NewsAttachment> query, NewsChildSearch search) =>
        search.NewsId is { } newsId ? query.Where(x => x.NewsId == newsId) : query;

    protected override IOrderedQueryable<NewsAttachment> Order(IQueryable<NewsAttachment> query) => query.OrderBy(x => x.NewsId).ThenBy(x => x.Id);
}

public sealed record NewsCommentRequest(long NewsId, string Name, string? Email, string? Title, string Content);

public sealed record NewsCommentDto(
    long Id, Guid PublicId, long NewsId, string? NewsTitle, string Name, string? Email, string? Title, string Content, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Bình luận tin (LRC: NewsCommentController, quyền PORTAL_COMMENTS) — bạn đọc gửi ẩn danh ở OPAC, thủ thư duyệt bằng trạng thái.</summary>
public sealed class NewsCommentResource(ICrudDbContext db) : CrudResource<NewsCommentResource, NewsComment, NewsChildSearch, NewsCommentRequest, NewsCommentDto>(db)
{
    protected override string EntityName => "Bình luận";

    protected override string? Describe(NewsComment entity) => entity.Name;

    protected override Expression<Func<NewsComment, NewsCommentDto>> Projection => x => new NewsCommentDto(
        x.Id, x.PublicId, x.NewsId, Db.Set<News>().Where(n => n.Id == x.NewsId).Select(n => n.Title).FirstOrDefault(), x.Name, x.Email, x.Title, x.Content, x.Status,
        x.CreatedAt, x.UpdatedAt);

    // Bình luận chỉ sinh từ OPAC (OpacNews.CommentAsync); thủ thư sửa/duyệt/xoá, không thêm hộ.
    protected override NewsComment Create(NewsCommentRequest r) => throw new BusinessRuleException("COMMENT_FROM_OPAC_ONLY", "Bình luận do bạn đọc gửi từ OPAC, không thêm hộ.");

    protected override void Update(NewsComment entity, NewsCommentRequest r) => entity.Update(r.Name, r.Email, r.Title, r.Content);

    protected override IQueryable<NewsComment> Filter(IQueryable<NewsComment> query, NewsChildSearch search)
    {
        if (search.NewsId is { } newsId) query = query.Where(x => x.NewsId == newsId);
        if (search.Status is { } status) query = query.Where(x => x.Status == status);
        return query;
    }

    protected override IOrderedQueryable<NewsComment> Order(IQueryable<NewsComment> query) => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
}
