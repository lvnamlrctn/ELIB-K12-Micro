using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Portal.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Portal.Application;

public sealed record OpacNewsSearch
{
    public string? Q { get; init; }
    public long? CategoryId { get; init; }
    public long? EventId { get; init; }
    public string? Language { get; init; }
    public bool FeaturedOnly { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed record OpacNewsItem(
    Guid PublicId, string Slug, string Title, string? Brief, string? Thumb, string? Images, DateTimeOffset PublishedAt, long? CategoryId, string? CategoryName, string? Author,
    bool Featured, int TotalView);

public sealed record OpacNewsPage(int Total, int Page, int PageSize, IReadOnlyList<OpacNewsItem> Items);

public sealed record OpacAttachment(Guid PublicId, string Name, string Url, double? FileSize);

public sealed record OpacNewsDetail(
    Guid PublicId, string Slug, string Title, string? Brief, string? Content, string? Images, DateTimeOffset PublishedAt, long? CategoryId, string? CategoryName, string? Author,
    string? Source, string? Keyword, bool AllowComment, int TotalView, string? MetaTitle, string? MetaKeyword, string? MetaDescription, IReadOnlyList<OpacAttachment> Attachments,
    IReadOnlyList<OpacNewsItem> Related);

public sealed record OpacCategory(long Id, string Name, long? ParentId, int Level, string? Description, string? PageTitle, string? MetaDescription);

public sealed record OpacComment(Guid PublicId, string Name, string? Title, string Content, DateTimeOffset CreatedAt);

/// <param name="Website">Ô bẫy (honeypot): người thật không thấy nên không điền; bot điền → bỏ qua lặng lẽ.</param>
public sealed record OpacCommentSubmission(string? Name, string? Email, string? Title, string? Content, string? Website = null);

public sealed record OpacPage(string Slug, string Title, string? Content, string? MetaDescription);

public sealed record OpacBanner(Guid PublicId, string Name, string Image, string? Link, int? Width, int? Height, string Position);

public sealed record OpacAd(Guid PublicId, string Name, string Image, string? Link, int? Width, int? Height);

public sealed record OpacAdGroup(Guid PublicId, string Name, string? Type, int? Width, int? Height, IReadOnlyList<OpacAd> Ads);

public sealed record OpacLink(Guid PublicId, string Name, string Url, string? Description, string? Image);

public sealed record OpacLinkGroup(Guid PublicId, string Name, IReadOnlyList<OpacLink> Links);

public sealed record OpacPhoto(Guid PublicId, string Name, string? Brief, string Image, string? Link);

public sealed record OpacAlbum(Guid PublicId, string Name, string? Description, string? Image, int PhotoCount);

public sealed record OpacVideo(Guid PublicId, string Name, string? Description, string EmbedUrl, string Provider, string? Image);

public sealed record OpacMenuItem(long Id, string Name, string Href, bool NewTab, bool RequiresLogin, string? Icon, IReadOnlyList<OpacMenuItem> Children);

public sealed record OpacMenu(string Code, string Name, IReadOnlyList<OpacMenuItem> Items);

public sealed record OpacContactGroup(long Id, string Name, string? Description);

/// <param name="Website">Ô bẫy (honeypot).</param>
public sealed record OpacContactSubmission(long? ContactGroupId, string? Name, string? Email, string? Mobile, string? Address, string? Title, string? Content, string? Website = null);

public sealed record OpacHome(
    IReadOnlyList<OpacBanner> Banners, IReadOnlyList<OpacNewsItem> Featured, IReadOnlyList<OpacNewsItem> Latest, IReadOnlyList<OpacAlbum> Albums, IReadOnlyList<OpacVideo> Videos,
    IReadOnlyList<OpacLinkGroup> LinkGroups, IReadOnlyList<OpacAdGroup> AdGroups);

/// <summary>
/// Nội dung CMS công khai cho OPAC (LRC: Public* controllers) — chỉ nội dung đã đăng/đang hoạt động và nằm trong khoảng hiển thị; theo ngôn ngữ giao diện
/// (nội dung không gắn ngôn ngữ hiện ở mọi ngôn ngữ). Tin thuộc chuyên mục yêu cầu đăng nhập không có trên đường ẩn danh.
/// </summary>
public sealed class OpacPortal(ICrudDbContext db, TimeProvider clock, IPublishEndpoint publisher, ITenantContext tenant)
{
    public const int MaxPageSize = 30;
    private const int MaxPendingComments = 200;
    private const int MaxNewContacts = 500;

    private DateTimeOffset Now => clock.GetUtcNow();

    /// <summary>Hôm nay theo lịch giờ Việt Nam — banner/quảng cáo hiển thị theo ngày lịch.</summary>
    private DateOnly Today => DateOnly.FromDateTime(Now.ToOffset(TimeSpan.FromHours(7)).DateTime);

    // ── Tin tức ──
    private IQueryable<News> PublishedNews(string? language)
    {
        var now = Now;
        var query = db.Set<News>().AsNoTracking().Where(n => n.Status == IHasStatus.Active && (n.StartTime == null || n.StartTime <= now) && (n.EndTime == null || n.EndTime > now));
        if (!string.IsNullOrWhiteSpace(language)) query = query.Where(n => n.Language == null || n.Language == language);
        // Chuyên mục yêu cầu đăng nhập: không lộ qua đường ẩn danh.
        var restricted = db.Set<NewsCategory>().Where(c => c.RequiresLogin).Select(c => c.Id);
        return query.Where(n => n.CategoryId == null || !restricted.Contains(n.CategoryId.Value));
    }

    private IQueryable<OpacNewsItem> NewsItems(IQueryable<News> query) => query.Select(n => new OpacNewsItem(
        n.PublicId, n.Slug, n.Title, n.Brief, n.Thumb, n.Images, n.PublishedAt ?? n.CreatedAt, n.CategoryId,
        db.Set<NewsCategory>().Where(c => c.Id == n.CategoryId).Select(c => c.Name).FirstOrDefault(), n.Author, n.Featured, n.TotalView));

    public async Task<OpacNewsPage> SearchNewsAsync(OpacNewsSearch search, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(search);
        var query = PublishedNews(search.Language);
        if (search.CategoryId is { } categoryId)
        {
            var ids = await CategoryBranchAsync(categoryId, ct);
            query = query.Where(n => n.CategoryId != null && ids.Contains(n.CategoryId.Value));
        }
        if (search.EventId is { } eventId) query = query.Where(n => n.EventId == eventId);
        if (search.FeaturedOnly) query = query.Where(n => n.Featured);
        foreach (var token in TextFold.Tokens(search.Q))
        {
            var t = token;
            query = query.Where(n => n.SearchText.Contains(t));
        }
        var size = Math.Clamp(search.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, search.Page);
        var total = await query.CountAsync(ct);
        var items = await NewsItems(query.OrderByDescending(n => n.PublishedAt ?? n.CreatedAt).ThenByDescending(n => n.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new OpacNewsPage(total, page, size, items);
    }

    public async Task<OpacNewsDetail> NewsAsync(string slug, string? language, CancellationToken ct)
    {
        var news = await PublishedNews(language).FirstOrDefaultAsync(n => n.Slug == slug, ct) ?? throw new NotFoundException("Tin tức", slug);
        var attachments = await db.Set<NewsAttachment>().AsNoTracking().Where(a => a.NewsId == news.Id).OrderBy(a => a.Id)
            .Select(a => new OpacAttachment(a.PublicId, a.Name, a.Url, a.FileSize)).ToListAsync(ct);
        var related = news.CategoryId is { } categoryId
            ? await NewsItems(PublishedNews(language).Where(n => n.CategoryId == categoryId && n.Id != news.Id).OrderByDescending(n => n.PublishedAt ?? n.CreatedAt).Take(5)).ToListAsync(ct)
            : [];
        var categoryName = news.CategoryId is { } id ? await db.Set<NewsCategory>().Where(c => c.Id == id).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
        return new OpacNewsDetail(news.PublicId, news.Slug, news.Title, news.Brief, news.Content, news.Images, news.PublishedAt ?? news.CreatedAt, news.CategoryId, categoryName,
            news.Author, news.Source, news.Keyword, news.AllowComment, news.TotalView, news.MetaTitle, news.MetaKeyword, news.MetaDescription, attachments, related);
    }

    /// <summary>Tăng lượt xem (gọi riêng sau khi mở tin — lỗi đếm không làm hỏng việc đọc).</summary>
    public async Task ViewAsync(string slug, CancellationToken ct)
    {
        await db.Set<News>().Where(n => n.Slug == slug && n.Status == IHasStatus.Active).ExecuteUpdateAsync(s => s.SetProperty(n => n.TotalView, n => n.TotalView + 1), ct);
    }

    public async Task<IReadOnlyList<OpacComment>> CommentsAsync(string slug, CancellationToken ct)
    {
        var newsId = await db.Set<News>().Where(n => n.Slug == slug && n.Status == IHasStatus.Active).Select(n => n.Id).FirstOrDefaultAsync(ct);
        if (newsId == 0) return [];
        return await db.Set<NewsComment>().AsNoTracking().Where(c => c.NewsId == newsId && c.Status == IHasStatus.Active).OrderByDescending(c => c.CreatedAt)
            .Select(c => new OpacComment(c.PublicId, c.Name, c.Title, c.Content, c.CreatedAt)).Take(100).ToListAsync(ct);
    }

    public async Task CommentAsync(string slug, OpacCommentSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var news = await PublishedNews(null).Where(n => n.Slug == slug).Select(n => new { n.Id, n.AllowComment }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Tin tức", slug);
        if (!news.AllowComment) throw new BusinessRuleException("COMMENTS_CLOSED", "Tin này không nhận bình luận.");
        if (!string.IsNullOrWhiteSpace(submission.Website)) return; // bot điền ô bẫy: nhận giả vờ, không lưu
        if (await db.Set<NewsComment>().CountAsync(c => c.Status == IHasStatus.Inactive, ct) >= MaxPendingComments)
            throw new BusinessRuleException("COMMENTS_BUSY", "Hệ thống đang có quá nhiều bình luận chờ duyệt, vui lòng thử lại sau.");
        db.Set<NewsComment>().Add(NewsComment.Submit(news.Id, submission.Name, submission.Email, submission.Title, submission.Content));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OpacCategory>> CategoriesAsync(string? language, CancellationToken ct)
    {
        var query = db.Set<NewsCategory>().AsNoTracking().Where(c => c.Status == IHasStatus.Active && !c.RequiresLogin);
        if (!string.IsNullOrWhiteSpace(language)) query = query.Where(c => c.Language == null || c.Language == language);
        return await query.OrderBy(c => c.Level).ThenBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new OpacCategory(c.Id, c.Name, c.ParentId, c.Level, c.Description, c.PageTitle, c.MetaDescription)).ToListAsync(ct);
    }

    private async Task<List<long>> CategoryBranchAsync(long rootId, CancellationToken ct)
    {
        var all = await db.Set<NewsCategory>().AsNoTracking().Select(c => new { c.Id, c.ParentId }).ToListAsync(ct);
        var children = all.Where(c => c.ParentId != null).ToLookup(c => c.ParentId!.Value, c => c.Id);
        var result = new List<long> { rootId };
        for (var i = 0; i < result.Count; i++) result.AddRange(children[result[i]]);
        return result;
    }

    // ── Trang tĩnh ──
    public async Task<OpacPage> PageAsync(string slug, string? language, CancellationToken ct)
    {
        var query = db.Set<PortalPage>().AsNoTracking().Where(p => p.Status == IHasStatus.Active && p.Slug == slug);
        if (!string.IsNullOrWhiteSpace(language)) query = query.Where(p => p.Language == null || p.Language == language);
        var page = await query.FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Trang", slug);
        return new OpacPage(page.Slug, page.Title, page.Content, page.MetaDescription);
    }

    // ── Menu ──
    public async Task<IReadOnlyList<OpacMenu>> MenusAsync(string? language, CancellationToken ct)
    {
        var types = await db.Set<MenuType>().AsNoTracking().Where(t => string.IsNullOrEmpty(language) || t.Language == null || t.Language == language).OrderBy(t => t.Code).ToListAsync(ct);
        var menus = await db.Set<Menu>().AsNoTracking().Where(m => m.Status == IHasStatus.Active && (string.IsNullOrEmpty(language) || m.Language == null || m.Language == language))
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id).ToListAsync(ct);
        var pages = await db.Set<PortalPage>().AsNoTracking().Where(p => p.Status == IHasStatus.Active).ToDictionaryAsync(p => p.Id, p => p.Slug, ct);
        var live = menus.Where(m => m.LinkType is MenuLinkTypes.Url or MenuLinkTypes.Path
            || (m.LinkType == MenuLinkTypes.Page && m.TargetId is { } p && pages.ContainsKey(p)) || m.LinkType == MenuLinkTypes.Category).ToList();
        var byParent = live.Where(m => m.ParentId != null).ToLookup(m => m.ParentId!.Value);

        OpacMenuItem ToItem(Menu m) => new(m.Id, m.Name, Href(m, pages), m.OpenInNewTab, m.RequiresLogin, m.Icon, [.. byParent[m.Id].Select(ToItem)]);

        return [.. types.Select(t => new OpacMenu(t.Code, t.Name, [.. live.Where(m => m.MenuTypeId == t.Id && m.ParentId == null).Select(ToItem)]))];
    }

    private static string Href(Menu menu, Dictionary<long, string> pages) => menu.LinkType switch
    {
        MenuLinkTypes.Page => $"/trang/{pages.GetValueOrDefault(menu.TargetId ?? 0)}",
        MenuLinkTypes.Category => $"/tin-tuc?chuyenmuc={menu.TargetId}",
        _ => menu.Link ?? "/",
    };

    // ── Banner / quảng cáo / liên kết / thư viện ảnh / video ──
    public async Task<IReadOnlyList<OpacBanner>> BannersAsync(string? position, string? language, CancellationToken ct)
    {
        var today = Today;
        var query = db.Set<Banner>().AsNoTracking().Where(b => b.Status == IHasStatus.Active && (b.ShowFrom == null || b.ShowFrom <= today) && (b.ShowTo == null || b.ShowTo >= today));
        if (!string.IsNullOrWhiteSpace(position)) query = query.Where(b => b.Position == position);
        if (!string.IsNullOrWhiteSpace(language)) query = query.Where(b => b.Language == null || b.Language == language);
        return await query.OrderBy(b => b.SortOrder).ThenBy(b => b.Id).Select(b => new OpacBanner(b.PublicId, b.Name, b.Image, b.Link, b.Width, b.Height, b.Position)).Take(20).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OpacAdGroup>> AdsAsync(string? language, CancellationToken ct)
    {
        var today = Today;
        var groups = await db.Set<AdsGroup>().AsNoTracking().Where(g => g.Status == IHasStatus.Active && (string.IsNullOrEmpty(language) || g.Language == null || g.Language == language))
            .OrderBy(g => g.Id).ToListAsync(ct);
        var ads = await db.Set<Ads>().AsNoTracking().Where(a => a.Status == IHasStatus.Active && (a.ShowFrom == null || a.ShowFrom <= today) && (a.ShowTo == null || a.ShowTo >= today))
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Id).ToListAsync(ct);
        return [.. groups.Select(g => new OpacAdGroup(g.PublicId, g.Name, g.Type, g.Width, g.Height,
            [.. ads.Where(a => a.AdsGroupId == g.Id).Select(a => new OpacAd(a.PublicId, a.Name, a.Image, a.Link, a.Width ?? g.Width, a.Height ?? g.Height))]))];
    }

    public async Task<IReadOnlyList<OpacLinkGroup>> LinksAsync(string? language, CancellationToken ct)
    {
        var groups = await db.Set<LinkGroup>().AsNoTracking().Where(g => g.Status == IHasStatus.Active && (string.IsNullOrEmpty(language) || g.Language == null || g.Language == language))
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync(ct);
        var links = await db.Set<PortalLink>().AsNoTracking().Where(l => l.Status == IHasStatus.Active).OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToListAsync(ct);
        return [.. groups.Select(g => new OpacLinkGroup(g.PublicId, g.Name, [.. links.Where(l => l.LinkGroupId == g.Id).Select(l => new OpacLink(l.PublicId, l.Name, l.LinkUrl, l.Description, l.Images))]))
            .Where(g => g.Links.Count > 0)];
    }

    public async Task<IReadOnlyList<OpacAlbum>> AlbumsAsync(string? language, int take, CancellationToken ct) =>
        await db.Set<PhotoAlbum>().AsNoTracking().Where(a => a.Status == IHasStatus.Active && (string.IsNullOrEmpty(language) || a.Language == null || a.Language == language))
            .OrderBy(a => a.SortOrder).ThenByDescending(a => a.Id).Take(Math.Clamp(take, 1, 50))
            .Select(a => new OpacAlbum(a.PublicId, a.Name, a.Description, a.Image, db.Set<Photo>().Count(p => p.PhotoAlbumId == a.Id && p.Status == IHasStatus.Active))).ToListAsync(ct);

    public async Task<IReadOnlyList<OpacPhoto>> PhotosAsync(Guid albumPublicId, CancellationToken ct)
    {
        var albumId = await db.Set<PhotoAlbum>().Where(a => a.PublicId == albumPublicId && a.Status == IHasStatus.Active).Select(a => a.Id).FirstOrDefaultAsync(ct);
        if (albumId == 0) throw new NotFoundException("Album ảnh", albumPublicId);
        return await db.Set<Photo>().AsNoTracking().Where(p => p.PhotoAlbumId == albumId && p.Status == IHasStatus.Active).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Take(500)
            .Select(p => new OpacPhoto(p.PublicId, p.Name, p.Brief, p.Image, p.Link)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OpacVideo>> VideosAsync(string? language, int take, CancellationToken ct)
    {
        var videos = await db.Set<Video>().AsNoTracking().Where(v => v.Status == IHasStatus.Active && (string.IsNullOrEmpty(language) || v.Language == null || v.Language == language))
            .OrderBy(v => v.SortOrder).ThenByDescending(v => v.Id).Take(Math.Clamp(take, 1, 50)).ToListAsync(ct);
        return [.. videos.Select(v => new OpacVideo(v.PublicId, v.Name, v.Description, v.EmbedUrl, v.Provider, v.Images))];
    }

    public async Task<OpacHome> HomeAsync(string? language, CancellationToken ct) => new(
        await BannersAsync(BannerPositions.Home, language, ct),
        await NewsItems(PublishedNews(language).Where(n => n.Featured).OrderByDescending(n => n.PublishedAt ?? n.CreatedAt).Take(6)).ToListAsync(ct),
        await NewsItems(PublishedNews(language).OrderByDescending(n => n.PublishedAt ?? n.CreatedAt).ThenByDescending(n => n.Id).Take(8)).ToListAsync(ct),
        await AlbumsAsync(language, 4, ct), await VideosAsync(language, 3, ct), await LinksAsync(language, ct), await AdsAsync(language, ct));

    // ── Liên hệ ──
    public async Task<IReadOnlyList<OpacContactGroup>> ContactGroupsAsync(string? language, CancellationToken ct) =>
        await db.Set<ContactGroup>().AsNoTracking().Where(g => g.Status == IHasStatus.Active && (string.IsNullOrEmpty(language) || g.Language == null || g.Language == language))
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Id).Select(g => new OpacContactGroup(g.Id, g.Name, g.Description)).ToListAsync(ct);

    /// <summary>Gửi liên hệ: chặn bot (ô bẫy), giới hạn số liên hệ chưa xử lý; báo cho email của nhóm (nếu có) qua notification.</summary>
    public async Task SubmitContactAsync(OpacContactSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (!string.IsNullOrWhiteSpace(submission.Website)) return;
        ContactGroup? group = null;
        if (submission.ContactGroupId is { } groupId)
            group = await db.Set<ContactGroup>().AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId && g.Status == IHasStatus.Active, ct)
                ?? throw new BusinessRuleException("CONTACT_GROUP_NOT_FOUND", "Nhóm liên hệ đã chọn không còn.");
        if (await db.Set<Contact>().CountAsync(c => c.Status == IHasStatus.Inactive, ct) >= MaxNewContacts)
            throw new BusinessRuleException("CONTACTS_BUSY", "Hệ thống đang nhận quá nhiều liên hệ, vui lòng thử lại sau.");

        var contact = Contact.Submit(group?.Id, submission.Name, submission.Email, submission.Mobile, submission.Address, submission.Title, submission.Content);
        db.Set<Contact>().Add(contact);
        if (!string.IsNullOrWhiteSpace(group?.Email))
        {
            // Publish TRƯỚC SaveChanges — NotificationRequested đi qua outbox, chỉ gửi được nếu cùng transaction lưu kèm liên hệ này.
            await publisher.Publish(new NotificationRequested
            {
                TenantId = tenant.RequireTenantId(),
                TemplateCode = PortalTemplates.ContactReceived,
                Recipient = new NotificationRecipient(null, null, group!.Email, null, group.Name),
                Data = new Dictionary<string, string>
                {
                    ["contact_name"] = contact.Name, ["contact_email"] = contact.Email, ["contact_title"] = contact.Title ?? "", ["contact_content"] = contact.Content,
                },
                DeduplicationKey = $"portal-contact:{Guid.CreateVersion7()}",
            }, ct);
        }
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Mã mẫu tin của portal (notification giữ nội dung mẫu — xem Notification's DefaultTemplates).</summary>
public static class PortalTemplates
{
    public const string ContactReceived = "PORTAL_CONTACT_RECEIVED";
}
