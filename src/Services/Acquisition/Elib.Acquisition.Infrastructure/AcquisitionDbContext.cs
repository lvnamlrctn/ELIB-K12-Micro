using Elib.Acquisition.Application;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace Elib.Acquisition.Infrastructure;

public sealed class AcquisitionDbContext(DbContextOptions<AcquisitionDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.NamedCatalog<AcquisitionSource>("sources", 250);

        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Fax).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(250);
            e.Property(x => x.Website).HasMaxLength(250);
            e.Property(x => x.TaxCode).HasMaxLength(50);
            e.Property(x => x.BankAccount).HasMaxLength(50);
            e.Property(x => x.BankName).HasMaxLength(250);
            e.Property(x => x.Representative).HasMaxLength(250);
            e.Property(x => x.Position).HasMaxLength(250);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<Budget>(e =>
        {
            e.ToTable("budgets");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<Fund>(e =>
        {
            e.ToTable("funds");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Manager).HasMaxLength(250);
            e.Property(x => x.Purpose).HasMaxLength(500);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne<Budget>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BibSnapshot>(e =>
        {
            e.ToTable("bib_snapshots");
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Publisher).HasMaxLength(500);
            e.Property(x => x.PublishYear).HasMaxLength(4);
            e.Property(x => x.Isbns).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.CatalogedAt });
            e.HasIndex(x => new { x.TenantId, x.BibPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Mfn });
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("purchase_orders");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.OrderedOn });
            ConfigureRefs(e);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.ToTable("order_lines");
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Publisher).HasMaxLength(500);
            e.Property(x => x.PublishYear).HasMaxLength(4);
            e.Property(x => x.Isbn).HasMaxLength(100);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.Rate).HasPrecision(18, 6);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.OrderId });
            e.HasIndex(x => new { x.TenantId, x.OrderId, x.BibPublicId }).IsUnique().HasFilter("is_deleted = false AND bib_public_id IS NOT NULL");
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Receipt>(e =>
        {
            e.ToTable("receipts");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Ignore(x => x.IsCompleted);
            e.HasIndex(x => new { x.TenantId, x.ReceivedOn });
            ConfigureRefs(e);
        });

        modelBuilder.Entity<ReceiptLine>(e =>
        {
            e.ToTable("receipt_lines");
            e.Property(x => x.Title).HasMaxLength(1000);
            e.Property(x => x.Author).HasMaxLength(500);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.Rate).HasPrecision(18, 6);
            // Mỗi biểu ghi một dòng trong phiếu (monolith: SaveDetail theo Receipt_Id + Bibid).
            e.HasIndex(x => new { x.TenantId, x.ReceiptId, x.BibPublicId }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.OrderLineId });
            e.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OrderLine>().WithMany().HasForeignKey(x => x.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureRefs<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e) where T : class
    {
        e.HasOne<AcquisitionSource>().WithMany().HasForeignKey("SourceId").OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Supplier>().WithMany().HasForeignKey("SupplierId").OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Budget>().WithMany().HasForeignKey("BudgetId").OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Fund>().WithMany().HasForeignKey("FundId").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Đơn vị mới: nguồn bổ sung mặc định; đơn vị có phân hệ Bổ sung thì chép bản sao biểu ghi từ catalog (xem <see cref="BibReplicaBackfill"/>).</summary>
public sealed class AcquisitionTenantSeeder(SourceResource sources, BibReplicaBackfill backfill) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        await sources.AddMissingDefaultsAsync(cancellationToken);
        if (tenant.Modules.Any(m => m.ModuleCode == BibReplicaBackfill.Module)) await backfill.RunIfEmptyAsync(tenant.TenantId, cancellationToken);
    }
}

/// <summary>
/// Chép bản sao biểu ghi của đơn vị hiện tại từ catalog khi còn trống — đơn vị mới có phân hệ Bổ sung, mua thêm phân hệ sau, hoặc
/// acquisition triển khai sau khi catalog đã có dữ liệu. Catalog chưa trả lời được thì bỏ qua: biểu ghi vẫn tới qua event và tra
/// được theo MFN khi chọn.
/// </summary>
public sealed partial class BibReplicaBackfill(BibSnapshots bibs, ICrudDbContext db, ILogger<BibReplicaBackfill> logger)
{
    public const string Module = "ACQUISITION";

    public async Task RunIfEmptyAsync(long tenantId, CancellationToken ct)
    {
        if (await db.Set<BibSnapshot>().AnyAsync(ct)) return;
        try
        {
            var count = await bibs.BackfillAsync(ct);
            LogBackfilled(logger, tenantId, count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogBackfillFailed(logger, ex, tenantId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đơn vị {TenantId}: đã chép {Count} biểu ghi từ catalog vào bản sao của acquisition")]
    private static partial void LogBackfilled(ILogger logger, long tenantId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Đơn vị {TenantId}: chưa chép được biểu ghi từ catalog — biểu ghi tới dần qua event / tra theo MFN")]
    private static partial void LogBackfillFailed(ILogger logger, Exception exception, long tenantId);
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class AcquisitionDbContextDesignFactory : IDesignTimeDbContextFactory<AcquisitionDbContext>
{
    public AcquisitionDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AcquisitionDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_acquisition")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
