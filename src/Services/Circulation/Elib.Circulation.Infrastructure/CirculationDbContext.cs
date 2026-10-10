using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Circulation.Application;
using Elib.Circulation.Domain;
using Elib.Contracts.Events.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Circulation.Infrastructure;

public sealed class CirculationDbContext(DbContextOptions<CirculationDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CircPlace>(e =>
        {
            e.ToTable("circ_places");
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(250);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<LoanPolicy>(e =>
        {
            e.ToTable("loan_policies");
            e.Ignore(x => x.Specificity);
            e.HasIndex(x => new { x.TenantId, x.ReaderTypeId, x.CircPlaceId });
            e.HasOne<CircPlace>().WithMany().HasForeignKey(x => x.CircPlaceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Loan>(e =>
        {
            e.ToTable("loans");
            e.Ignore(x => x.IsOpen);
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.Barcode).HasMaxLength(50);
            e.Property(x => x.Note).HasMaxLength(Loan.MaxNoteLength);
            e.Property(x => x.Version).IsConcurrencyToken();
            // Một bản sách chỉ có một lượt mượn đang mở — chặn cả hai quầy cùng mượn một ĐKCB.
            e.HasIndex(x => new { x.TenantId, x.ItemPublicId }).IsUnique().HasFilter("returned_at IS NULL AND is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.ReturnedAt });
            e.HasIndex(x => new { x.TenantId, x.CardNo });
            e.HasIndex(x => new { x.TenantId, x.Barcode });
            e.HasIndex(x => new { x.TenantId, x.DueAt });
            e.HasIndex(x => new { x.TenantId, x.LoanedAt });
        });

        modelBuilder.Entity<PatronReplica>(e =>
        {
            e.ToTable("patron_replicas");
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.FullName).HasMaxLength(250);
            e.Property(x => x.ReaderTypeName).HasMaxLength(250);
            e.Property(x => x.ClassName).HasMaxLength(250);
            e.Property(x => x.CourseName).HasMaxLength(250);
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CardNo });
        });

        modelBuilder.Entity<ItemReplica>(e =>
        {
            e.ToTable("item_replicas");
            e.Property(x => x.Barcode).HasMaxLength(50);
            e.Property(x => x.BarcodeKey).HasMaxLength(50);
            e.Property(x => x.StoreName).HasMaxLength(250);
            e.Property(x => x.Status).HasMaxLength(1);
            e.HasIndex(x => new { x.TenantId, x.ItemPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.BarcodeKey });
        });

        modelBuilder.Entity<BibSnapshot>(e =>
        {
            e.ToTable("bib_snapshots");
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Ddc).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.BibPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Mfn });
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Đơn vị mới có phân hệ Lưu thông: một quầy mượn trả và chính sách chung (14 ngày, gia hạn 7 ngày như monolith).</summary>
public sealed class CirculationTenantSeeder(CircPlaceResource places, LoanPolicyResource policies) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        await places.AddDefaultAsync(cancellationToken);
        await policies.AddDefaultAsync(cancellationToken);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class CirculationDbContextDesignFactory : IDesignTimeDbContextFactory<CirculationDbContext>
{
    public CirculationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<CirculationDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_circulation")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
