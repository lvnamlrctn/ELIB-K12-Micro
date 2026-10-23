using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Portal.Application;

namespace Elib.Portal.Api;

/// <summary>
/// Cổng thông tin / CMS (gateway: /api/admin/portal/** → /api/**, cần license PORTAL). Mã quyền: PORTAL_NEWS (tin + tệp đính kèm, kèm "publish"),
/// PORTAL_CATEGORIES (chuyên mục + sự kiện), PORTAL_COMMENTS, PORTAL_PAGES ("publish"), PORTAL_MENUS (loại menu + menu), PORTAL_LINKS, PORTAL_BANNERS,
/// PORTAL_ADS, PORTAL_GALLERY (album, ảnh, video), PORTAL_CONTACTS (nhóm + liên hệ), PORTAL_COUNTER.
/// </summary>
public static class PortalEndpoints
{
    public const string Module = "PORTAL";

    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<NewsCategoryResource>("/news-categories", "PORTAL_CATEGORIES").WithTags("NewsCategories");
        api.MapCrud<NewsEventResource>("/news-events", "PORTAL_CATEGORIES").WithTags("NewsEvents");
        api.MapCrud<NewsResource>("/news", "PORTAL_NEWS").WithTags("News");
        api.MapCrud<NewsAttachmentResource>("/news-attachments", "PORTAL_NEWS").WithTags("NewsAttachments");
        api.MapCrud<NewsCommentResource>("/news-comments", "PORTAL_COMMENTS").WithTags("NewsComments");
        api.MapCrud<PortalPageResource>("/pages", "PORTAL_PAGES").WithTags("Pages");
        api.MapCrud<MenuTypeResource>("/menu-types", "PORTAL_MENUS").WithTags("MenuTypes");
        api.MapCrud<MenuResource>("/menus", "PORTAL_MENUS").WithTags("Menus");
        api.MapCrud<LinkGroupResource>("/link-groups", "PORTAL_LINKS").WithTags("LinkGroups");
        api.MapCrud<PortalLinkResource>("/links", "PORTAL_LINKS").WithTags("Links");
        api.MapCrud<BannerResource>("/banners", "PORTAL_BANNERS").WithTags("Banners");
        api.MapCrud<AdsGroupResource>("/ads-groups", "PORTAL_ADS").WithTags("AdsGroups");
        api.MapCrud<AdsResource>("/ads", "PORTAL_ADS").WithTags("Ads");
        api.MapCrud<PhotoAlbumResource>("/photo-albums", "PORTAL_GALLERY").WithTags("PhotoAlbums");
        api.MapCrud<PhotoResource>("/photos", "PORTAL_GALLERY").WithTags("Photos");
        api.MapCrud<VideoResource>("/videos", "PORTAL_GALLERY").WithTags("Videos");
        api.MapCrud<ContactGroupResource>("/contact-groups", "PORTAL_CONTACTS").WithTags("ContactGroups");
        var contacts = api.MapCrud<ContactResource>("/contacts", "PORTAL_CONTACTS").WithTags("Contacts");
        contacts.MapPost("/{publicId:guid}/Handle", [Permission("PORTAL_CONTACTS", "edit")] (Guid publicId, ContactRequest request, ContactResource r, ICurrentActor actor, CancellationToken ct)
            => r.HandleAsync(publicId, actor.Id, request, ct));
        api.MapGet("/counter", [Permission("PORTAL_COUNTER", "view")] (VisitCounter counter, CancellationToken ct) => counter.SummaryAsync(ct)).WithTags("Counter");
        api.MapGet("/counter/days", [Permission("PORTAL_COUNTER", "view")] (int? days, VisitCounter counter, CancellationToken ct) => counter.DaysAsync(days ?? 30, ct)).WithTags("Counter");
        return app;
    }
}
