using Elib.BuildingBlocks.Crud;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Portal.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPortalApplication(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlCleaner, HtmlCleaner>();
        services.AddCrudResource<NewsCategoryResource>();
        services.AddCrudResource<NewsEventResource>();
        services.AddCrudResource<NewsResource>();
        services.AddCrudResource<NewsAttachmentResource>();
        services.AddCrudResource<NewsCommentResource>();
        services.AddCrudResource<PortalPageResource>();
        services.AddCrudResource<MenuTypeResource>();
        services.AddCrudResource<MenuResource>();
        services.AddCrudResource<LinkGroupResource>();
        services.AddCrudResource<PortalLinkResource>();
        services.AddCrudResource<BannerResource>();
        services.AddCrudResource<AdsGroupResource>();
        services.AddCrudResource<AdsResource>();
        services.AddCrudResource<PhotoAlbumResource>();
        services.AddCrudResource<PhotoResource>();
        services.AddCrudResource<VideoResource>();
        services.AddCrudResource<ContactGroupResource>();
        services.AddCrudResource<ContactResource>();
        services.AddScoped<OpacPortal>();
        services.AddScoped<VisitCounter>();
        services.AddScoped<PortalDashboardService>();
        return services;
    }
}
