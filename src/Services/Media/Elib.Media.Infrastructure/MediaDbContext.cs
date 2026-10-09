using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.Media.Application;
using Elib.Media.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Media.Infrastructure;

public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), IMediaDb
{
    public DbSet<MediaFile> Files => Set<MediaFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaFile>(e =>
        {
            e.ToTable("media_files");
            e.Property(f => f.Purpose).HasMaxLength(40);
            e.Property(f => f.FileName).HasMaxLength(255);
            e.Property(f => f.ContentType).HasMaxLength(100);
            e.Property(f => f.Bucket).HasMaxLength(63);
            e.Property(f => f.ObjectKey).HasMaxLength(300);
            e.HasIndex(f => f.PublicId).IsUnique();
            e.HasIndex(f => new { f.Status, f.CreatedAt });
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class MediaDbContextDesignFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_media")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
