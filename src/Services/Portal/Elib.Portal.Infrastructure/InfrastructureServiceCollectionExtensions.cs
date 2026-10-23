using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Portal.Application;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Portal.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "portal";

    public static IServiceCollection AddPortalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PortalDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:PortalDb (đặt qua biến môi trường ConnectionStrings__PortalDb).");

        services.Configure<PortalOptions>(configuration.GetSection(PortalOptions.SectionName));
        services.AddElibPostgres<PortalDbContext>(connectionString);
        services.AddCrudDbContext<PortalDbContext>(ServiceName);
        services.AddTenantReplica<PortalDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<PortalDbContext>(configuration);
        services.AddScoped<ITenantSeeder, PortalTenantSeeder>();
        services.AddElibMessaging<PortalDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<PortalDbContext>();
            x.AddPermissionCacheInvalidation();
        });
        services.AddHealthChecks().AddDbContextCheck<PortalDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}

/// <summary>
/// Đơn vị mới: tạo sẵn bộ khung để trang chủ OPAC có nội dung — hai loại menu (đầu trang, chân trang) kèm vài mục, một chuyên mục tin và một nhóm liên
/// hệ chung. Chạy lại không tạo trùng (đã có loại menu thì bỏ qua).
/// </summary>
public sealed class PortalTenantSeeder(PortalDbContext db) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (await db.Set<MenuType>().AnyAsync(cancellationToken)) return;

        var main = MenuType.Create("main", "Menu chính", "Thanh điều hướng đầu trang", null);
        var footer = MenuType.Create("footer", "Menu chân trang", "Liên kết cuối trang", null);
        db.Set<MenuType>().AddRange(main, footer);
        db.Set<NewsCategory>().Add(NewsCategory.Create(new NewsCategoryFields("Tin thư viện")));
        db.Set<ContactGroup>().Add(ContactGroup.Create("Liên hệ chung", null, "Góp ý, hỏi đáp chung về thư viện", null, 0, 2));
        await db.SaveChangesAsync(cancellationToken); // cần Id của loại menu trước khi tạo mục

        var items = new (MenuType Type, string Name, string Path, int Order)[]
        {
            (main, "Trang chủ", "/", 1), (main, "Tin tức", "/tin-tuc", 2), (main, "Liên hệ", "/lien-he", 3),
            (footer, "Tin tức", "/tin-tuc", 1), (footer, "Liên hệ", "/lien-he", 2),
        };
        foreach (var (type, name, path, order) in items)
        {
            var menu = Menu.Create(new MenuFields(name, type.Id, MenuLinkTypes.Path, path));
            menu.AttachTo(null);
            menu.Reorder(order);
            db.Set<Menu>().Add(menu);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
