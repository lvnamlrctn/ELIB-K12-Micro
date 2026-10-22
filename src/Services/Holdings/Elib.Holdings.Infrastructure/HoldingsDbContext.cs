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
            e.HasIndex(x => new { x.TenantId, x.ReceiptPublicId });
            e.HasIndex(x => new { x.TenantId, x.ReceiptLinePublicId });
            e.HasIndex(x => new { x.TenantId, x.ShelfPublicId });
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShelfReplica>(e =>
        {
            e.ToTable("shelf_replicas");
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.ColorHex).HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.ShelfPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.StorePublicId });
        });

        modelBuilder.Entity<ShelfRowReplica>(e =>
        {
            e.ToTable("shelf_row_replicas");
            e.Property(x => x.DdcStart).HasMaxLength(30);
            e.Property(x => x.DdcEnd).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ShelfPublicId });
        });

        modelBuilder.Entity<FloorReplica>(e =>
        {
            e.ToTable("floor_replicas");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.BuildingName).HasMaxLength(250);
            e.Property(x => x.LayoutImageUrl).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.FloorPublicId }).IsUnique();
        });

        modelBuilder.Entity<ItemMovement>(e =>
        {
            e.ToTable("item_movements");
            e.Property(x => x.Barcode).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.Kind).HasMaxLength(12);
            e.Property(x => x.FromStatus).HasMaxLength(1);
            e.Property(x => x.ToStatus).HasMaxLength(1);
            e.Property(x => x.Reason).HasMaxLength(ItemMovement.MaxReasonLength);
            e.Property(x => x.ActorName).HasMaxLength(250);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.At });
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
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
            e.Property(x => x.Cutter).HasMaxLength(50);
            e.Property(x => x.BibTypeName).HasMaxLength(250);
            e.HasIndex(x => new { x.TenantId, x.BibPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Mfn });
        });

        modelBuilder.Entity<ReceiptSnapshot>(e =>
        {
            e.ToTable("receipt_snapshots");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.RegistrationStamp).IsConcurrencyToken();
            e.HasIndex(x => new { x.TenantId, x.ReceiptPublicId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Code });
        });

        modelBuilder.NamedCatalog<ExportReason>("export_reasons", 250);
        modelBuilder.NamedCatalog<BookOutUnit>("book_out_units", 250);
        modelBuilder.NamedCatalog<ExhibitionLocation>("exhibition_locations", 250);

        ApprovalModel.Configure(modelBuilder);

        modelBuilder.Entity<LostRecord>(e =>
        {
            e.ToTable("lost_records");
            e.Property(x => x.Barcode).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.PreviousStatus).HasMaxLength(1);
            e.Property(x => x.ReversedByName).HasMaxLength(300);
            e.Property(x => x.ReversalReason).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ItemId });
            e.HasIndex(x => new { x.TenantId, x.LostOn });
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Liquidation>(e =>
        {
            e.ToTable("liquidations");
            e.Property(x => x.Barcode).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.DecisionNo).HasMaxLength(100);
            e.Property(x => x.PreviousStatus).HasMaxLength(1);
            e.Property(x => x.ReversedByName).HasMaxLength(300);
            e.Property(x => x.ReversalReason).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ItemId });
            e.HasIndex(x => new { x.TenantId, x.LiquidatedOn });
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StoreOut>(e =>
        {
            e.ToTable("store_outs");
            e.Property(x => x.Barcode).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.DelivererName).HasMaxLength(250);
            e.Property(x => x.ReceiverName).HasMaxLength(250);
            e.Ignore(x => x.IsOpen);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.InAt });
            e.HasIndex(x => new { x.TenantId, x.OutAt });
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ExportReason>().WithMany().HasForeignKey(x => x.ReasonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<BookOutUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ExhibitionLocation>().WithMany().HasForeignKey(x => x.ExhibitionLocationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Inventory>(e =>
        {
            e.ToTable("inventories");
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Ignore(x => x.IsClosed);
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryScan>(e =>
        {
            e.ToTable("inventory_scans");
            e.Property(x => x.Barcode).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.BarcodeKey).HasMaxLength(Item.MaxBarcodeLength);
            e.Property(x => x.ItemStatus).HasMaxLength(1);
            e.Ignore(x => x.Abnormal);
            // Mỗi mã một lần trong đợt — quét lại trả dòng cũ.
            e.HasIndex(x => new { x.TenantId, x.InventoryId, x.BarcodeKey }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.InventoryId, x.ItemId });
            e.HasOne<Inventory>().WithMany().HasForeignKey(x => x.InventoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StoreMove>(e =>
        {
            e.ToTable("store_moves");
            e.Property(x => x.DelivererName).HasMaxLength(250);
            e.Property(x => x.ReceiverName).HasMaxLength(250);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Ignore(x => x.IsCompleted);
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.FromStoreId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.ToStoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StoreMoveLine>(e =>
        {
            e.ToTable("store_move_lines");
            e.Property(x => x.SkipReason).HasMaxLength(250);
            e.HasIndex(x => new { x.TenantId, x.MoveId, x.ItemId }).IsUnique().HasFilter("is_deleted = false");
            e.HasOne<StoreMove>().WithMany().HasForeignKey(x => x.MoveId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Delivery>(e =>
        {
            e.ToTable("deliveries");
            e.Property(x => x.DelivererName).HasMaxLength(250);
            e.Property(x => x.DelivererAddress).HasMaxLength(500);
            e.Property(x => x.ReceiverName).HasMaxLength(250);
            e.Property(x => x.ReceiverAddress).HasMaxLength(500);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Ignore(x => x.IsSigned);
            e.HasIndex(x => new { x.TenantId, x.ReceiptPublicId });
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryLine>(e =>
        {
            e.ToTable("delivery_lines");
            // Mỗi bản sách chỉ nằm ở một phiếu giao nhận (monolith ELIB-LRC 10-03: hai người cùng chọn một cuốn).
            e.HasIndex(x => new { x.TenantId, x.ItemId }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.DeliveryId });
            e.HasOne<Delivery>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Đơn vị mới có phân hệ Kho: loại kho mặc định, một kho chung, lý do xuất kho mặc định (saga khởi tạo chờ holdings báo TenantSeeded).
/// </summary>
public sealed class HoldingsTenantSeeder(StoreTypeResource storeTypes, StoreResource stores, ExportReasonResource exportReasons) : ITenantSeeder
{
    public async Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        await storeTypes.AddMissingDefaultsAsync(cancellationToken);
        await stores.AddDefaultAsync(cancellationToken);
        await exportReasons.AddMissingDefaultsAsync(cancellationToken);
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
