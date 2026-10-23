using System.Text.RegularExpressions;
using Elib.BuildingBlocks.Domain;

namespace Elib.Portal.Domain;

/// <summary>Trang tĩnh (LRC: Page — ở LRC chỉ là mã trang; K12 giữ luôn nội dung HTML đã làm sạch). Hiện ở /trang/{slug} khi Đã đăng.</summary>
public sealed class PortalPage : TenantEntity, IHasStatus
{
    private PortalPage() { }

    public string Slug { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string? Content { get; private set; }
    public string? Language { get; private set; }
    public string? MetaDescription { get; private set; }
    public int Status { get; private set; } = IHasStatus.Inactive;

    public static PortalPage Create(PortalPageFields f)
    {
        var page = new PortalPage();
        page.Update(f);
        return page;
    }

    public void Update(PortalPageFields f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Slug = TextRules.Slug(f.Slug, "Mã trang");
        Title = TextRules.Required(f.Title, 300, "TITLE_INVALID", "Tên trang");
        Content = f.Content is { Length: > News.MaxContentLength } ? throw new BusinessRuleException("CONTENT_TOO_LONG", "Nội dung quá dài.") : f.Content;
        Language = TextRules.Language(f.Language);
        MetaDescription = TextRules.Optional(f.MetaDescription, 500, "META_DESCRIPTION_INVALID", "Mô tả SEO");
        Status = StatusRules.Validate(f.Status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

public sealed record PortalPageFields(string Slug, string Title, string? Content = null, string? Language = null, string? MetaDescription = null, int Status = IHasStatus.Inactive);

/// <summary>Loại menu (LRC: MenuType) — vị trí đặt menu: "main" (đầu trang), "footer"...</summary>
public sealed class MenuType : TenantEntity
{
    private MenuType() { }

    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public string? Language { get; private set; }

    public static MenuType Create(string? code, string? name, string? description, string? language)
    {
        var type = new MenuType();
        type.Update(code, name, description, language);
        return type;
    }

    public void Update(string? code, string? name, string? description, string? language)
    {
        Code = TextRules.Slug(code, "Mã loại menu");
        Name = TextRules.Required(name, 200, "NAME_INVALID", "Tên loại menu");
        Description = TextRules.Optional(description, 500, "DESCRIPTION_INVALID", "Mô tả");
        Language = TextRules.Language(language);
    }
}

/// <summary>Đích của mục menu: địa chỉ ngoài (http/https), đường dẫn nội bộ ("/…"), trang tĩnh hoặc chuyên mục tin của chính đơn vị.</summary>
public static class MenuLinkTypes
{
    public const string Url = "url";
    public const string Path = "path";
    public const string Page = "page";
    public const string Category = "category";

    public static readonly IReadOnlyList<string> All = [Url, Path, Page, Category];
}

/// <summary>Mục menu (LRC: Menu) — cây theo loại menu. Link/TargetId: một trong hai theo LinkType; trang/chuyên mục được kiểm thuộc đúng đơn vị ở Resource.</summary>
public sealed partial class Menu : PortalTreeItem
{
    private Menu() { }

    protected override int MaxNameLength => 200;

    public long MenuTypeId { get; private set; }
    public string LinkType { get; private set; } = MenuLinkTypes.Url;

    /// <summary>Địa chỉ ngoài hoặc đường dẫn nội bộ (LinkType url/path).</summary>
    public string? Link { get; private set; }

    /// <summary>Id trang tĩnh (LinkType page) hoặc chuyên mục tin (LinkType category).</summary>
    public long? TargetId { get; private set; }

    public bool OpenInNewTab { get; private set; }
    public bool RequiresLogin { get; private set; }
    public string? Icon { get; private set; }

    public static Menu Create(MenuFields f)
    {
        var menu = new Menu();
        menu.Update(f);
        return menu;
    }

    public void Update(MenuFields f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Describe(f.Name, f.Language);
        MenuTypeId = f.MenuTypeId;
        LinkType = MenuLinkTypes.All.FirstOrDefault(t => string.Equals(t, f.LinkType, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("MENU_LINK_TYPE_INVALID", "Kiểu liên kết menu không hợp lệ (url, path, page, category).");
        (Link, TargetId) = LinkType switch
        {
            MenuLinkTypes.Url => (RequireHttp(SafeLink.Validate(f.Link, "Địa chỉ")), (long?)null),
            MenuLinkTypes.Path => (RequirePath(SafeLink.Validate(f.Link, "Đường dẫn")), null),
            _ => (null, f.TargetId is > 0 ? f.TargetId : throw new BusinessRuleException("MENU_TARGET_REQUIRED", "Chọn trang hoặc chuyên mục đích.")),
        };
        OpenInNewTab = f.OpenInNewTab;
        RequiresLogin = f.RequiresLogin;
        Icon = string.IsNullOrWhiteSpace(f.Icon) ? null : IconPattern().IsMatch(f.Icon.Trim()) ? f.Icon.Trim()
            : throw new BusinessRuleException("ICON_INVALID", "Tên biểu tượng chỉ gồm chữ thường, số và dấu gạch dưới.");
    }

    private static string RequireHttp(string? link) =>
        link is not null && link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link : throw new BusinessRuleException("MENU_LINK_INVALID", "Địa chỉ phải bắt đầu bằng http:// hoặc https://.");

    private static string RequirePath(string? link) =>
        link is not null && link.StartsWith('/') ? link : throw new BusinessRuleException("MENU_LINK_INVALID", "Đường dẫn nội bộ phải bắt đầu bằng \"/\".");

    [GeneratedRegex("^[a-z0-9_]{1,40}$")]
    private static partial Regex IconPattern();
}

public sealed record MenuFields(
    string Name, long MenuTypeId, string LinkType, string? Link = null, long? TargetId = null, bool OpenInNewTab = false, bool RequiresLogin = false, string? Icon = null,
    string? Language = null);

/// <summary>Nhóm liên kết (LRC: LinkGroup) — ví dụ "Đối tác", "Liên kết nhanh".</summary>
public sealed class LinkGroup : TenantEntity, IHasStatus
{
    private LinkGroup() { }

    public string Name { get; private set; } = "";
    public string? Language { get; private set; }
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static LinkGroup Create(string? name, string? language, int sortOrder, int status)
    {
        var group = new LinkGroup();
        group.Update(name, language, sortOrder, status);
        return group;
    }

    public void Update(string? name, string? language, int sortOrder, int status)
    {
        Name = TextRules.Required(name, 200, "NAME_INVALID", "Tên nhóm");
        Language = TextRules.Language(language);
        SortOrder = sortOrder;
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>Liên kết (LRC: Link) — website bạn bè/đối tác, có thể kèm logo.</summary>
public sealed class PortalLink : TenantEntity, IHasStatus
{
    private PortalLink() { }

    public long LinkGroupId { get; private set; }
    public string Name { get; private set; } = "";
    public string LinkUrl { get; private set; } = "";
    public string? Description { get; private set; }
    public string? Images { get; private set; }
    public int SortOrder { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static PortalLink Create(long groupId, string? name, string? url, string? description, string? images, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        var link = new PortalLink { LinkGroupId = groupId };
        link.Update(name, url, description, images, sortOrder, status, mediaPublicPrefix, tenantId);
        return link;
    }

    public void Update(string? name, string? url, string? description, string? images, int sortOrder, int status, string mediaPublicPrefix, long tenantId)
    {
        Name = TextRules.Required(name, 250, "NAME_INVALID", "Tên liên kết");
        LinkUrl = SafeLink.Validate(url, "Địa chỉ liên kết") ?? throw new BusinessRuleException("LINK_INVALID", "Địa chỉ liên kết bắt buộc.");
        Description = TextRules.Optional(description, 500, "DESCRIPTION_INVALID", "Mô tả");
        Images = PortalMedia.Image(images, mediaPublicPrefix, tenantId, "Logo");
        SortOrder = sortOrder;
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}
