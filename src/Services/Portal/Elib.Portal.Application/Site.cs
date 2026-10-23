using System.Linq.Expressions;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Portal.Application;

public sealed class PageSearch : CrudSearch
{
    public string? Language { get; set; }
}

public sealed record PortalPageRequest(string? Slug, string Title, string? Content = null, string? Language = null, string? MetaDescription = null, int Status = IHasStatus.Inactive);

public sealed record PortalPageDto(
    long Id, Guid PublicId, string Slug, string Title, string? Content, string? Language, string? MetaDescription, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Trang tĩnh (LRC: PageController, quyền PORTAL_PAGES). Nội dung HTML làm sạch trước khi lưu; mã trang (slug) duy nhất trong đơn vị.</summary>
public sealed class PortalPageResource(ICrudDbContext db, ITenantContext tenant, IHtmlCleaner cleaner, IActorPermissions permissions)
    : CrudResource<PortalPageResource, PortalPage, PageSearch, PortalPageRequest, PortalPageDto>(db)
{
    protected override string EntityName => "Trang";

    protected override string? Describe(PortalPage entity) => entity.Title;

    protected override IReadOnlyCollection<string> DiffIgnored => ["content"];

    protected override Expression<Func<PortalPage, PortalPageDto>> Projection => x =>
        new PortalPageDto(x.Id, x.PublicId, x.Slug, x.Title, x.Content, x.Language, x.MetaDescription, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override PortalPage Create(PortalPageRequest r) => PortalPage.Create(ToFields(r));

    protected override void Update(PortalPage entity, PortalPageRequest r) => entity.Update(ToFields(r));

    public override async Task<PortalPageDto> AddAsync(PortalPageRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Status == IHasStatus.Active) await PublishGuard.RequireAsync(permissions, "PORTAL_PAGES", ct);
        return await base.AddAsync(request, ct);
    }

    protected override async Task UpdateAsync(PortalPage entity, PortalPageRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (entity.Status == IHasStatus.Active || request.Status == IHasStatus.Active) await PublishGuard.RequireAsync(permissions, "PORTAL_PAGES", ct);
        await base.UpdateAsync(entity, request, ct);
    }

    protected override async Task OnSavingAsync(PortalPage entity, CrudChange change, CancellationToken ct)
    {
        if (change == CrudChange.StatusChanged) await PublishGuard.RequireAsync(permissions, "PORTAL_PAGES", ct);
    }

    protected override async Task ValidateAsync(PortalPage entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Slug == entity.Slug && x.Id != entity.Id, ct)) throw new ConflictException("PAGE_SLUG_EXISTS", $"Mã trang '{entity.Slug}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(PortalPage entity, CancellationToken ct)
    {
        if (entity.Status == IHasStatus.Active) await PublishGuard.RequireAsync(permissions, "PORTAL_PAGES", ct);
        if (await Db.Set<Menu>().AnyAsync(m => m.LinkType == MenuLinkTypes.Page && m.TargetId == entity.Id, ct))
            throw new ConflictException("PAGE_IN_MENU", $"Trang '{entity.Title}' đang được dùng trong menu.");
    }

    protected override IQueryable<PortalPage> Filter(IQueryable<PortalPage> query, PageSearch search)
    {
        if (search.Status is { } status) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search.Language)) query = query.Where(x => x.Language == null || x.Language == search.Language);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Title.ToLower().Contains(term) || x.Slug.Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<PortalPage> Order(IQueryable<PortalPage> query) => query.OrderBy(x => x.Slug);

    private PortalPageFields ToFields(PortalPageRequest r) => new(
        string.IsNullOrWhiteSpace(r.Slug) ? Slugger.From(r.Title, "trang") : r.Slug, r.Title, cleaner.Clean(r.Content, tenant.RequireTenantId()), r.Language, r.MetaDescription, r.Status);
}

public sealed record MenuTypeRequest(string Code, string Name, string? Description = null, string? Language = null);

public sealed record MenuTypeDto(long Id, Guid PublicId, string Code, string Name, string? Description, string? Language, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Loại menu (LRC: MenuTypeController, quyền PORTAL_MENUS).</summary>
public sealed class MenuTypeResource(ICrudDbContext db) : CrudResource<MenuTypeResource, MenuType, CrudSearch, MenuTypeRequest, MenuTypeDto>(db)
{
    protected override string EntityName => "Loại menu";

    protected override string? Describe(MenuType entity) => entity.Code;

    protected override Expression<Func<MenuType, MenuTypeDto>> Projection => x => new MenuTypeDto(x.Id, x.PublicId, x.Code, x.Name, x.Description, x.Language, x.CreatedAt, x.UpdatedAt);

    protected override MenuType Create(MenuTypeRequest r) => MenuType.Create(r.Code, r.Name, r.Description, r.Language);

    protected override void Update(MenuType entity, MenuTypeRequest r) => entity.Update(r.Code, r.Name, r.Description, r.Language);

    protected override async Task ValidateAsync(MenuType entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct)) throw new ConflictException("MENU_TYPE_EXISTS", $"Loại menu '{entity.Code}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(MenuType entity, CancellationToken ct)
    {
        if (await Db.Set<Menu>().AnyAsync(m => m.MenuTypeId == entity.Id, ct)) throw new ConflictException("MENU_TYPE_HAS_MENUS", $"Loại menu '{entity.Code}' còn mục menu.");
    }

    protected override IQueryable<MenuType> Filter(IQueryable<MenuType> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Code.Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<MenuType> Order(IQueryable<MenuType> query) => query.OrderBy(x => x.Code);
}

public sealed record MenuRequest(
    string Name, long MenuTypeId, string LinkType, long? ParentId = null, int? SortOrder = null, int? Status = null, string? Link = null, long? TargetId = null,
    bool OpenInNewTab = false, bool RequiresLogin = false, string? Icon = null, string? Language = null) : IPortalTreeRequest;

public sealed record MenuDto(
    long Id, Guid PublicId, string Name, long MenuTypeId, string LinkType, long? ParentId, int Level, int SortOrder, int Status, string? Link, long? TargetId, bool OpenInNewTab,
    bool RequiresLogin, string? Icon, string? Language, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// Mục menu (LRC: MenuController, quyền PORTAL_MENUS). Đích chỉ nhận địa chỉ http(s), đường dẫn nội bộ "/…", hoặc trang/chuyên mục thuộc ĐÚNG đơn vị
/// (kiểm ở đây — mọi bảng đã lọc theo đơn vị nên tra theo id chỉ thấy của chính đơn vị).
/// </summary>
public sealed class MenuResource(ICrudDbContext db) : PortalTreeResource<MenuResource, Menu, MenuRequest, MenuDto>(db)
{
    protected override string EntityName => "Mục menu";

    protected override string? Describe(Menu entity) => entity.Name;

    protected override Expression<Func<Menu, MenuDto>> Projection => x => new MenuDto(
        x.Id, x.PublicId, x.Name, x.MenuTypeId, x.LinkType, x.ParentId, x.Level, x.SortOrder, x.Status, x.Link, x.TargetId, x.OpenInNewTab, x.RequiresLogin, x.Icon, x.Language,
        x.CreatedAt, x.UpdatedAt);

    protected override Menu NewEntity(MenuRequest r) => Menu.Create(ToFields(r));

    protected override void ApplyFields(Menu entity, MenuRequest r) => entity.Update(ToFields(r));

    protected override IQueryable<Menu> Filter(IQueryable<Menu> query, PortalTreeSearch search) =>
        base.Filter(search.MenuTypeId is { } typeId ? query.Where(x => x.MenuTypeId == typeId) : query, search);

    protected override async Task ValidateAsync(Menu entity, CancellationToken ct)
    {
        if (!await Db.Set<MenuType>().AnyAsync(t => t.Id == entity.MenuTypeId, ct)) throw new BusinessRuleException("MENU_TYPE_NOT_FOUND", "Loại menu đã chọn không còn.");
        if (entity.ParentId is { } parentId && !await Set.AnyAsync(m => m.Id == parentId && m.MenuTypeId == entity.MenuTypeId, ct))
            throw new BusinessRuleException("MENU_PARENT_INVALID", "Mục cha phải thuộc cùng loại menu.");
        if (entity.LinkType == MenuLinkTypes.Page && !await Db.Set<PortalPage>().AnyAsync(p => p.Id == entity.TargetId, ct))
            throw new BusinessRuleException("MENU_TARGET_NOT_FOUND", "Trang đã chọn không còn.");
        if (entity.LinkType == MenuLinkTypes.Category && !await Db.Set<NewsCategory>().AnyAsync(c => c.Id == entity.TargetId, ct))
            throw new BusinessRuleException("MENU_TARGET_NOT_FOUND", "Chuyên mục đã chọn không còn.");
    }

    private static MenuFields ToFields(MenuRequest r) => new(r.Name, r.MenuTypeId, r.LinkType, r.Link, r.TargetId, r.OpenInNewTab, r.RequiresLogin, r.Icon, r.Language);
}

public sealed record LinkGroupRequest(string Name, string? Language = null, int SortOrder = 0, int Status = IHasStatus.Active);

public sealed record LinkGroupDto(long Id, Guid PublicId, string Name, string? Language, int SortOrder, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Nhóm liên kết (LRC: LinkGroupController, quyền PORTAL_LINKS).</summary>
public sealed class LinkGroupResource(ICrudDbContext db) : CrudResource<LinkGroupResource, LinkGroup, CrudSearch, LinkGroupRequest, LinkGroupDto>(db)
{
    protected override string EntityName => "Nhóm liên kết";

    protected override string? Describe(LinkGroup entity) => entity.Name;

    protected override Expression<Func<LinkGroup, LinkGroupDto>> Projection => x => new LinkGroupDto(x.Id, x.PublicId, x.Name, x.Language, x.SortOrder, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override LinkGroup Create(LinkGroupRequest r) => LinkGroup.Create(r.Name, r.Language, r.SortOrder, r.Status);

    protected override void Update(LinkGroup entity, LinkGroupRequest r) => entity.Update(r.Name, r.Language, r.SortOrder, r.Status);

    protected override async Task EnsureDeletableAsync(LinkGroup entity, CancellationToken ct)
    {
        if (await Db.Set<PortalLink>().AnyAsync(l => l.LinkGroupId == entity.Id, ct)) throw new ConflictException("LINK_GROUP_HAS_LINKS", $"Nhóm '{entity.Name}' còn liên kết.");
    }

    protected override IQueryable<LinkGroup> Filter(IQueryable<LinkGroup> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<LinkGroup> Order(IQueryable<LinkGroup> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);
}

public sealed class LinkSearch : CrudSearch
{
    public long? LinkGroupId { get; set; }
}

public sealed record PortalLinkRequest(long LinkGroupId, string Name, string LinkUrl, string? Description = null, string? Images = null, int SortOrder = 0, int Status = IHasStatus.Active);

public sealed record PortalLinkDto(
    long Id, Guid PublicId, long LinkGroupId, string Name, string LinkUrl, string? Description, string? Images, int SortOrder, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Liên kết (LRC: LinkController, quyền PORTAL_LINKS).</summary>
public sealed class PortalLinkResource(ICrudDbContext db, ITenantContext tenant, IOptions<PortalOptions> options)
    : CrudResource<PortalLinkResource, PortalLink, LinkSearch, PortalLinkRequest, PortalLinkDto>(db)
{
    private readonly PortalOptions _options = options.Value;

    protected override string EntityName => "Liên kết";

    protected override string? Describe(PortalLink entity) => entity.Name;

    protected override Expression<Func<PortalLink, PortalLinkDto>> Projection => x =>
        new PortalLinkDto(x.Id, x.PublicId, x.LinkGroupId, x.Name, x.LinkUrl, x.Description, x.Images, x.SortOrder, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override PortalLink Create(PortalLinkRequest r) =>
        PortalLink.Create(r.LinkGroupId, r.Name, r.LinkUrl, r.Description, r.Images, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override void Update(PortalLink entity, PortalLinkRequest r) =>
        entity.Update(r.Name, r.LinkUrl, r.Description, r.Images, r.SortOrder, r.Status, _options.MediaPublicPrefix, tenant.RequireTenantId());

    protected override async Task ValidateAsync(PortalLink entity, CancellationToken ct)
    {
        if (!await Db.Set<LinkGroup>().AnyAsync(g => g.Id == entity.LinkGroupId, ct)) throw new BusinessRuleException("LINK_GROUP_NOT_FOUND", "Nhóm liên kết đã chọn không còn.");
    }

    protected override IQueryable<PortalLink> Filter(IQueryable<PortalLink> query, LinkSearch search)
    {
        if (search.LinkGroupId is { } group) query = query.Where(x => x.LinkGroupId == group);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<PortalLink> Order(IQueryable<PortalLink> query) => query.OrderBy(x => x.LinkGroupId).ThenBy(x => x.SortOrder).ThenBy(x => x.Id);
}
