using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Serials.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Serials.Infrastructure;

public sealed class SerialsDbContext(DbContextOptions<SerialsDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.NamedCatalog<PublicationType>("publication_types", 250);

        modelBuilder.Entity<Frequency>(e =>
        {
            e.ToTable("frequencies");
            e.Property(x => x.Name).HasMaxLength(250);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<NumberingPattern>(e =>
        {
            e.ToTable("numbering_patterns");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.LabelTemplate).HasMaxLength(250);
            e.Property(x => x.NameX).HasMaxLength(50);
            e.Property(x => x.NameY).HasMaxLength(50);
            e.Property(x => x.NameZ).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<BibSnapshot>(e =>
        {
            e.ToTable("bib_snapshots");
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Publisher).HasMaxLength(500);
            e.Property(x => x.Issn).HasMaxLength(50);
            e.Property(x => x.TitleKey).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.BibPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Mfn });
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.ToTable("subscriptions");
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.Issn).HasMaxLength(50);
            e.Property(x => x.StoreName).HasMaxLength(250);
            e.Property(x => x.SupplierName).HasMaxLength(250);
            e.Property(x => x.CallNumber).HasMaxLength(100);
            e.Property(x => x.PublicNote).HasMaxLength(500);
            e.Property(x => x.InternalNote).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.BibPublicId });
            e.HasOne<PublicationType>().WithMany().HasForeignKey(x => x.PublicationTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Frequency>().WithMany().HasForeignKey(x => x.FrequencyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<NumberingPattern>().WithMany().HasForeignKey(x => x.PatternId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SerialIssue>(e =>
        {
            e.ToTable("serial_issues");
            e.Property(x => x.Label).HasMaxLength(250);
            e.Property(x => x.Note).HasMaxLength(500);
            // Mỗi số X/Y/Z một kỳ (kỳ ghép số và kỳ đã xoá không tính) — chống sinh trùng.
            e.HasIndex(x => new { x.TenantId, x.SubscriptionId, x.X, x.Y, x.Z }).IsUnique().HasFilter("is_deleted = false AND is_merged = false");
            e.HasIndex(x => new { x.TenantId, x.SubscriptionId, x.Status });
            e.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Binding>(e =>
        {
            e.ToTable("bindings");
            e.Property(x => x.AccessionNo).HasMaxLength(50);
            e.Property(x => x.VolumeTitle).HasMaxLength(500);
            e.Property(x => x.StoreName).HasMaxLength(250);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.AccessionNo }).IsUnique().HasFilter("is_deleted = false");
            e.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BindingIssue>(e =>
        {
            e.ToTable("binding_issues");
            e.HasIndex(x => new { x.TenantId, x.IssueId }).IsUnique().HasFilter("is_deleted = false");
            e.HasOne<Binding>().WithMany().HasForeignKey(x => x.BindingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SerialIssue>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class SerialsDbContextDesignFactory : IDesignTimeDbContextFactory<SerialsDbContext>
{
    public SerialsDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SerialsDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_serials")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
