using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Holdings.Application;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Holdings.Infrastructure;

public sealed class HoldingsDbContext(DbContextOptions<HoldingsDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.NamedCatalog<StoreType>("store_types", 250);

        modelBuilder.Entity<Store>(e =>
        {
            e.ToTable("stores");
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Position).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
            e.HasOne<StoreType>().WithMany().HasForeignKey(x => x.StoreTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("items");
            e.Property(x => x.Barcode).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.BarcodeKey).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.Prefix).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.Status).HasMaxLength(1);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.LoanCardNo).HasMaxLength(50);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.TenantId, x.BarcodeKey }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.Prefix, x.Number });
            e.HasIndex(x => new { x.TenantId, x.BibId });
            e.HasIndex(x => new { x.TenantId, x.StoreId, x.Status });
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BibSnapshot>(e =>
        {
            e.ToTable("bib_snapshots");
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Publisher).HasMaxLength(500);
            e.Property(x => x.PublishYear).HasMaxLength(4);
            e.Property(x => x.Isbns).HasMaxLength(1000);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.BibPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Mfn });
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Đơn vị mới có phân hệ Kho: loại kho mặc định và một kho chung (saga khởi tạo chờ holdings báo TenantSeeded).</summary>
public sealed class HoldingsTenantSeeder(StoreTypeResource storeTypes, StoreResource stores) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        await storeTypes.AddMissingDefaultsAsync(cancellationToken);
        await stores.AddDefaultAsync(cancellationToken);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class HoldingsDbContextDesignFactory : IDesignTimeDbContextFactory<HoldingsDbContext>
{
    public HoldingsDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<HoldingsDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_holdings")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
