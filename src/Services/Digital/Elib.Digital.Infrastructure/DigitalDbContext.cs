using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Digital.Infrastructure;

public sealed class DigitalDbContext(DbContextOptions<DigitalDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PatronReplica>(e =>
        {
            e.ToTable("patron_replicas");
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.FullName).HasMaxLength(250);
            e.Property(x => x.ReaderTypeName).HasMaxLength(250);
            e.Property(x => x.Email).HasMaxLength(250);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CardNo });
        });

        modelBuilder.Entity<EbookCollection>(e =>
        {
            e.ToTable("ebook_collections");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.Link).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        modelBuilder.Entity<EbookSubject>(e =>
        {
            e.ToTable("ebook_subjects");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.Link).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        modelBuilder.Entity<EbookTopic>(e =>
        {
            e.ToTable("ebook_topics");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Keyword).HasMaxLength(500);
            e.Property(x => x.PageTitle).HasMaxLength(250);
            e.Property(x => x.MetaDescription).HasMaxLength(500);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.ParentId });
        });

        modelBuilder.Entity<EbookDigType>(e =>
        {
            e.ToTable("ebook_dig_types");
            e.Property(x => x.Code).HasMaxLength(EbookDigType.MaxCodeLength);
            e.Property(x => x.DescriptionVn).HasMaxLength(EbookDigType.MaxDescriptionLength);
            e.Property(x => x.DescriptionEn).HasMaxLength(EbookDigType.MaxDescriptionLength);
            e.Property(x => x.Language).HasMaxLength(10);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<PolicyDigital>(e =>
        {
            e.ToTable("policy_digitals");
            e.HasIndex(x => new { x.TenantId, x.ReaderTypeId }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<PolicyDigitalByCollection>(e =>
        {
            e.ToTable("policy_digital_by_collections");
            e.HasIndex(x => new { x.TenantId, x.CollectionId, x.ReaderTypeId }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Chưa có gì cần seed riêng cho digital (danh mục mặc định — nếu có — thêm khi port EbookDigType).</summary>
public sealed class DigitalTenantSeeder : ITenantSeeder
{
    public Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return Task.CompletedTask;
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class DigitalDbContextDesignFactory : IDesignTimeDbContextFactory<DigitalDbContext>
{
    public DigitalDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<DigitalDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_digital")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
