using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Patron.Application;
using Elib.Patron.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Patron.Infrastructure;

public sealed class PatronDbContext(DbContextOptions<PatronDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.NamedCatalog<ReaderType>("reader_types", 250);
        modelBuilder.NamedCatalog<SchoolClass>("classes", 250);
        modelBuilder.NamedCatalog<Course>("courses", 250);

        modelBuilder.Entity<ReaderGroup>(e =>
        {
            e.ToTable("reader_groups");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Code).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false AND code IS NOT NULL");
        });

        modelBuilder.Entity<Reader>(e =>
        {
            e.ToTable("readers");
            e.Ignore(x => x.FullName);
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.LastName).HasMaxLength(150);
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.CitizenId).HasMaxLength(20);
            e.Property(x => x.CardUid).HasMaxLength(64);
            e.Property(x => x.Email).HasMaxLength(250);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.LockReason).HasMaxLength(500);
            e.Property(x => x.Version).IsConcurrencyToken();
            // Số thẻ và UID thẻ không trùng trong đơn vị (bỏ qua bạn đọc đã xoá mềm).
            e.HasIndex(x => new { x.TenantId, x.CardNo }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.CardUid }).IsUnique().HasFilter("is_deleted = false AND card_uid IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.ReaderTypeId });
            e.HasIndex(x => new { x.TenantId, x.ClassId });
            e.HasIndex(x => new { x.TenantId, x.ExpireDate });
            e.HasOne<ReaderType>().WithMany().HasForeignKey(x => x.ReaderTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SchoolClass>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Đơn vị mới: tạo loại bạn đọc mặc định (saga khởi tạo chờ patron báo TenantSeeded).</summary>
public sealed class PatronTenantSeeder(ReaderTypeResource readerTypes) : ITenantSeeder
{
    public Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken) => readerTypes.AddMissingDefaultsAsync(cancellationToken);
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class PatronDbContextDesignFactory : IDesignTimeDbContextFactory<PatronDbContext>
{
    public PatronDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PatronDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_patron")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
