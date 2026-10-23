using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Portal.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Portal.Infrastructure;

public sealed class PortalDbContext(DbContextOptions<PortalDbContext> options, ITenantContext tenant) : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsCategory>(e =>
        {
            e.ToTable("news_categories");
            Tree(e);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Keyword).HasMaxLength(500);
            e.Property(x => x.PageTitle).HasMaxLength(250);
            e.Property(x => x.MetaDescription).HasMaxLength(500);
            e.Property(x => x.Link).HasMaxLength(SafeLink.MaxLength);
        });

        modelBuilder.Entity<NewsEvent>(e =>
        {
            e.ToTable("news_events");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Language).HasMaxLength(5);
        });

        modelBuilder.Entity<News>(e =>
        {
            e.ToTable("news");
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.Slug).HasMaxLength(150);
            e.Property(x => x.Brief).HasMaxLength(2000);
            e.Property(x => x.Images).HasMaxLength(PortalMedia.MaxUrlLength);
            e.Property(x => x.Thumb).HasMaxLength(PortalMedia.MaxUrlLength);
            e.Property(x => x.Language).HasMaxLength(5);
            e.Property(x => x.Keyword).HasMaxLength(500);
            e.Property(x => x.Author).HasMaxLength(200);
            e.Property(x => x.Source).HasMaxLength(300);
            e.Property(x => x.Types).HasMaxLength(50);
            e.Property(x => x.MetaTitle).HasMaxLength(250);
            e.Property(x => x.MetaKeyword).HasMaxLength(500);
            e.Property(x => x.MetaDescription).HasMaxLength(500);
            e.Property(x => x.SearchText).HasMaxLength(2000);
            e.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.Status, x.PublishedAt });
            e.HasIndex(x => new { x.TenantId, x.CategoryId });
        });

        modelBuilder.Entity<NewsAttachment>(e =>
        {
            e.ToTable("news_attachments");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Url).HasMaxLength(PortalMedia.MaxUrlLength);
            e.HasIndex(x => new { x.TenantId, x.NewsId });
        });

        modelBuilder.Entity<NewsComment>(e =>
        {
            e.ToTable("news_comments");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(250);
            e.Property(x => x.Content).HasMaxLength(NewsComment.MaxContentLength);
            e.HasIndex(x => new { x.TenantId, x.NewsId, x.Status });
        });

        modelBuilder.Entity<PortalPage>(e =>
        {
            e.ToTable("portal_pages");
            e.Property(x => x.Slug).HasMaxLength(150);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Language).HasMaxLength(5);
            e.Property(x => x.MetaDescription).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<MenuType>(e =>
        {
            e.ToTable("menu_types");
            e.Property(x => x.Code).HasMaxLength(150);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Language).HasMaxLength(5);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<Menu>(e =>
        {
            e.ToTable("menus");
            Tree(e);
            e.Property(x => x.LinkType).HasMaxLength(10);
            e.Property(x => x.Link).HasMaxLength(SafeLink.MaxLength);
            e.Property(x => x.Icon).HasMaxLength(40);
            e.HasIndex(x => new { x.TenantId, x.MenuTypeId, x.ParentId, x.SortOrder });
        });

        modelBuilder.Entity<LinkGroup>(e =>
        {
            e.ToTable("link_groups");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Language).HasMaxLength(5);
        });

        modelBuilder.Entity<PortalLink>(e =>
        {
            e.ToTable("portal_links");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.LinkUrl).HasMaxLength(SafeLink.MaxLength);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Images).HasMaxLength(PortalMedia.MaxUrlLength);
            e.HasIndex(x => new { x.TenantId, x.LinkGroupId, x.SortOrder });
        });

        modelBuilder.Entity<Banner>(e =>
        {
            e.ToTable("banners");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Position).HasMaxLength(10);
            e.Property(x => x.Image).HasMaxLength(PortalMedia.MaxUrlLength);
            e.Property(x => x.Link).HasMaxLength(SafeLink.MaxLength);
            e.Property(x => x.Language).HasMaxLength(5);
            e.HasIndex(x => new { x.TenantId, x.Position, x.Status, x.SortOrder });
        });

        modelBuilder.Entity<AdsGroup>(e =>
        {
            e.ToTable("ads_groups");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.Language).HasMaxLength(5);
        });

        modelBuilder.Entity<Ads>(e =>
        {
            e.ToTable("ads");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Image).HasMaxLength(PortalMedia.MaxUrlLength);
            e.Property(x => x.Link).HasMaxLength(SafeLink.MaxLength);
            e.HasIndex(x => new { x.TenantId, x.AdsGroupId, x.SortOrder });
        });

        modelBuilder.Entity<PhotoAlbum>(e =>
        {
            e.ToTable("photo_albums");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Image).HasMaxLength(PortalMedia.MaxUrlLength);
            e.Property(x => x.Language).HasMaxLength(5);
        });

        modelBuilder.Entity<Photo>(e =>
        {
            e.ToTable("photos");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Brief).HasMaxLength(1000);
            e.Property(x => x.Image).HasMaxLength(PortalMedia.MaxUrlLength);
            e.Property(x => x.Link).HasMaxLength(SafeLink.MaxLength);
            e.HasIndex(x => new { x.TenantId, x.PhotoAlbumId, x.SortOrder });
        });

        modelBuilder.Entity<Video>(e =>
        {
            e.ToTable("videos");
            e.Ignore(x => x.EmbedUrl);
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Provider).HasMaxLength(10);
            e.Property(x => x.VideoId).HasMaxLength(20);
            e.Property(x => x.Images).HasMaxLength(PortalMedia.MaxUrlLength);
            e.Property(x => x.Language).HasMaxLength(5);
        });

        modelBuilder.Entity<ContactGroup>(e =>
        {
            e.ToTable("contact_groups");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Language).HasMaxLength(5);
        });

        modelBuilder.Entity<Contact>(e =>
        {
            e.ToTable("contacts");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Mobile).HasMaxLength(30);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Title).HasMaxLength(250);
            e.Property(x => x.Content).HasMaxLength(Contact.MaxContentLength);
            e.Property(x => x.HandleNote).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<CounterDay>(e =>
        {
            e.ToTable("counter_days");
            e.HasIndex(x => new { x.TenantId, x.Day }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }

    private static void Tree<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e) where T : PortalTreeItem
    {
        e.Property(x => x.Name).HasMaxLength(250);
        e.Property(x => x.Language).HasMaxLength(5);
        e.HasIndex(x => new { x.TenantId, x.ParentId, x.SortOrder });
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class PortalDbContextDesignFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_portal")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
