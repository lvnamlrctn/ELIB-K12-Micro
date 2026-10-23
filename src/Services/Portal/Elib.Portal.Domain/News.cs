using Elib.BuildingBlocks.Domain;

namespace Elib.Portal.Domain;

/// <summary>Chuyên mục tin (LRC: Category). IsLogin: chỉ bạn đọc đăng nhập mới xem được tin của chuyên mục.</summary>
public sealed class NewsCategory : PortalTreeItem
{
    private NewsCategory() { }

    protected override int MaxNameLength => 250;

    public string? Description { get; private set; }
    public string? Keyword { get; private set; }
    public string? PageTitle { get; private set; }
    public string? MetaDescription { get; private set; }
    public string? Link { get; private set; }
    public bool RequiresLogin { get; private set; }

    public static NewsCategory Create(NewsCategoryFields f)
    {
        var category = new NewsCategory();
        category.Update(f);
        return category;
    }

    public void Update(NewsCategoryFields f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Describe(f.Name, f.Language);
        Description = TextRules.Optional(f.Description, 1000, "DESCRIPTION_INVALID", "Mô tả");
        Keyword = TextRules.Optional(f.Keyword, 500, "KEYWORD_INVALID", "Từ khoá");
        PageTitle = TextRules.Optional(f.PageTitle, 250, "PAGE_TITLE_INVALID", "Tiêu đề trang");
        MetaDescription = TextRules.Optional(f.MetaDescription, 500, "META_DESCRIPTION_INVALID", "Mô tả SEO");
        Link = SafeLink.Validate(f.Link, "Liên kết");
        RequiresLogin = f.RequiresLogin;
    }
}

public sealed record NewsCategoryFields(
    string Name, string? Language = null, string? Description = null, string? Keyword = null, string? PageTitle = null, string? MetaDescription = null,
    string? Link = null, bool RequiresLogin = false);

/// <summary>Sự kiện (LRC: EventNews) — nhóm tin theo sự kiện.</summary>
public sealed class NewsEvent : TenantEntity
{
    private NewsEvent() { }

    public string Name { get; private set; } = "";
    public string? Language { get; private set; }

    public static NewsEvent Create(string? name, string? language)
    {
        var e = new NewsEvent();
        e.Update(name, language);
        return e;
    }

    public void Update(string? name, string? language)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên sự kiện");
        Language = TextRules.Language(language);
    }
}

/// <summary>
/// Tin tức (LRC: News). Status dùng đúng số của IHasStatus: Inactive=1 "Nháp/Ẩn", Active=2 "Đã đăng". Hiện trên OPAC khi Đã đăng, đã tới
/// <see cref="StartTime"/> và chưa quá <see cref="EndTime"/> (hẹn giờ đăng/gỡ — không cần job). Nội dung HTML đã được làm sạch ở tầng ứng dụng
/// trước khi vào entity.
/// </summary>
public sealed class News : TenantEntity, IHasStatus
{
    public const int MaxContentLength = 500_000;

    private News() { }

    public string Title { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public string? Brief { get; private set; }
    public string? Content { get; private set; }
    public string? Images { get; private set; }
    public string? Thumb { get; private set; }
    public DateTimeOffset? StartTime { get; private set; }
    public DateTimeOffset? EndTime { get; private set; }
    public long? CategoryId { get; private set; }
    public long? EventId { get; private set; }
    public string? Language { get; private set; }
    public string? Keyword { get; private set; }
    public string? Author { get; private set; }
    public string? Source { get; private set; }
    public string? Types { get; private set; }
    public bool Featured { get; private set; }
    public bool AllowComment { get; private set; }
    public int TotalView { get; private set; }
    public string? MetaTitle { get; private set; }
    public string? MetaKeyword { get; private set; }
    public string? MetaDescription { get; private set; }
    public int Status { get; private set; } = IHasStatus.Inactive;

    /// <summary>Lần đầu được đăng — sắp xếp "tin mới" theo ngày này (không đổi khi sửa lại).</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    public long Version { get; private set; } = 1;

    /// <summary>Tiêu đề + tóm tắt + từ khoá + tác giả đã bỏ dấu — tìm không dấu ("khai mac" ra "Khai mạc").</summary>
    public string SearchText { get; private set; } = "";

    public static News Create(NewsFields f, string mediaPublicPrefix, long tenantId, DateTimeOffset now)
    {
        var news = new News();
        news.Update(f, mediaPublicPrefix, tenantId, now);
        return news;
    }

    public void Update(NewsFields f, string mediaPublicPrefix, long tenantId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(f);
        Title = TextRules.Required(f.Title, 500, "TITLE_INVALID", "Tiêu đề");
        Slug = TextRules.Slug(f.Slug, "Đường dẫn");
        Brief = TextRules.Optional(f.Brief, 2000, "BRIEF_INVALID", "Tóm tắt");
        Content = f.Content is { Length: > MaxContentLength } ? throw new BusinessRuleException("CONTENT_TOO_LONG", "Nội dung quá dài.") : f.Content;
        Images = PortalMedia.Image(f.Images, mediaPublicPrefix, tenantId, "Ảnh đại diện");
        Thumb = PortalMedia.Image(f.Thumb, mediaPublicPrefix, tenantId, "Ảnh nhỏ");
        if (f.StartTime is { } start && f.EndTime is { } end && end <= start) throw new BusinessRuleException("TIME_RANGE_INVALID", "Giờ gỡ phải sau giờ đăng.");
        StartTime = f.StartTime;
        EndTime = f.EndTime;
        CategoryId = f.CategoryId;
        EventId = f.EventId;
        Language = TextRules.Language(f.Language);
        Keyword = TextRules.Optional(f.Keyword, 500, "KEYWORD_INVALID", "Từ khoá");
        Author = TextRules.Optional(f.Author, 200, "AUTHOR_INVALID", "Tác giả");
        Source = TextRules.Optional(f.Source, 300, "SOURCE_INVALID", "Nguồn");
        Types = TextRules.Optional(f.Types, 50, "TYPES_INVALID", "Loại tin");
        Featured = f.Featured;
        AllowComment = f.AllowComment;
        MetaTitle = TextRules.Optional(f.MetaTitle, 250, "META_TITLE_INVALID", "Tiêu đề SEO");
        MetaKeyword = TextRules.Optional(f.MetaKeyword, 500, "META_KEYWORD_INVALID", "Từ khoá SEO");
        MetaDescription = TextRules.Optional(f.MetaDescription, 500, "META_DESCRIPTION_INVALID", "Mô tả SEO");
        var folded = TextFold.Fold($"{Title} {Brief} {Keyword} {Author}");
        SearchText = folded.Length > 2000 ? folded[..2000] : folded;
        if (Status != f.Status) ChangeStatus(f.Status, now);
        Version++;
    }

    public void ChangeStatus(int status) => ChangeStatus(status, DateTimeOffset.UtcNow);

    public void ChangeStatus(int status, DateTimeOffset now)
    {
        Status = StatusRules.Validate(status);
        if (Status == IHasStatus.Active) PublishedAt ??= StartTime ?? now;
        Version++;
    }

    public void RecordView() => TotalView++;

    /// <summary>Đang hiện trên OPAC tại <paramref name="now"/>.</summary>
    public bool IsVisible(DateTimeOffset now) => Status == IHasStatus.Active && (StartTime is null || StartTime <= now) && (EndTime is null || EndTime > now);
}

public sealed record NewsFields(
    string Title, string Slug, string? Brief = null, string? Content = null, string? Images = null, string? Thumb = null, DateTimeOffset? StartTime = null,
    DateTimeOffset? EndTime = null, long? CategoryId = null, long? EventId = null, string? Language = null, string? Keyword = null, string? Author = null,
    string? Source = null, string? Types = null, bool Featured = false, bool AllowComment = false, string? MetaTitle = null, string? MetaKeyword = null,
    string? MetaDescription = null, int Status = IHasStatus.Inactive);

/// <summary>Tệp đính kèm của tin (LRC: AttachFile) — tệp công khai đã upload qua media (mục đích cms-attachment).</summary>
public sealed class NewsAttachment : TenantEntity
{
    private NewsAttachment() { }

    public long NewsId { get; private set; }
    public string Name { get; private set; } = "";
    public string Url { get; private set; } = "";
    public double? FileSize { get; private set; }

    public static NewsAttachment Create(long newsId, string? name, string? url, double? fileSize, string mediaPublicPrefix, long tenantId)
    {
        var file = new NewsAttachment { NewsId = newsId };
        file.Update(name, url, fileSize, mediaPublicPrefix, tenantId);
        return file;
    }

    public void Update(string? name, string? url, double? fileSize, string mediaPublicPrefix, long tenantId)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên tệp");
        Url = PortalMedia.Attachment(url, mediaPublicPrefix, tenantId, "Tệp đính kèm") ?? throw new BusinessRuleException("MEDIA_URL_INVALID", "Tệp đính kèm bắt buộc.");
        FileSize = fileSize is null or >= 0 ? fileSize : throw new BusinessRuleException("FILE_SIZE_INVALID", "Dung lượng không được âm.");
    }
}

/// <summary>Bình luận tin (LRC: NewsComment) — gửi ẩn danh từ OPAC, luôn Chờ duyệt (Inactive); chỉ Đã duyệt (Active) mới hiện. Chỉ tin cho phép bình luận.</summary>
public sealed class NewsComment : TenantEntity, IHasStatus
{
    public const int MaxContentLength = 2000;

    private NewsComment() { }

    public long NewsId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Email { get; private set; }
    public string? Title { get; private set; }
    public string Content { get; private set; } = "";
    public int Status { get; private set; } = IHasStatus.Inactive;

    public static NewsComment Submit(long newsId, string? name, string? email, string? title, string? content)
    {
        var comment = new NewsComment { NewsId = newsId };
        comment.Update(name, email, title, content);
        return comment;
    }

    public void Update(string? name, string? email, string? title, string? content)
    {
        Name = TextRules.Required(name, 200, "NAME_INVALID", "Tên");
        Email = TextRules.Optional(email, 200, "EMAIL_INVALID", "Email");
        Title = TextRules.Optional(title, 250, "TITLE_INVALID", "Tiêu đề");
        Content = TextRules.Required(content, MaxContentLength, "CONTENT_INVALID", "Nội dung bình luận");
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}
