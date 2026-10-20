using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Space.Infrastructure;

public sealed class SpaceDbContext(DbContextOptions<SpaceDbContext> options, ITenantContext tenant) : ElibDbContext(options, tenant), ICrudDbContext
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

        modelBuilder.Entity<MapBuilding>(e =>
        {
            e.ToTable("map_buildings");
            e.Property(x => x.Code).HasMaxLength(MapBuilding.MaxCodeLength);
            e.Property(x => x.Name).HasMaxLength(MapBuilding.MaxNameLength);
            e.Property(x => x.Description).HasMaxLength(MapBuilding.MaxDescriptionLength);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<MapFloor>(e =>
        {
            e.ToTable("map_floors");
            e.Property(x => x.Name).HasMaxLength(MapFloor.MaxNameLength);
            e.Property(x => x.LayoutImageUrl).HasMaxLength(FloorLayout.MaxUrlLength);
            e.HasIndex(x => new { x.TenantId, x.BuildingId });
            e.HasOne<MapBuilding>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookableSpace>(e =>
        {
            e.ToTable("bookable_spaces");
            e.Property(x => x.Code).HasMaxLength(BookableSpace.MaxCodeLength);
            e.Property(x => x.Name).HasMaxLength(BookableSpace.MaxNameLength);
            e.Property(x => x.Rules).HasMaxLength(BookableSpace.MaxRulesLength);
            e.Property(x => x.MaintenanceNote).HasMaxLength(BookableSpace.MaxMaintenanceNoteLength);
            e.Property(x => x.ColorHex).HasMaxLength(20);
            e.Property(x => x.IconName).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.FloorId });
            e.HasOne<MapFloor>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomOpeningHour>(e =>
        {
            e.ToTable("room_opening_hours");
            e.HasIndex(x => new { x.TenantId, x.Category, x.Weekday });
        });

        modelBuilder.Entity<RoomSpecialDay>(e =>
        {
            e.ToTable("room_special_days");
            e.Property(x => x.Reason).HasMaxLength(RoomSpecialDay.MaxReasonLength);
            e.HasIndex(x => new { x.TenantId, x.Date, x.Category }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<SpaceBookingSettings>(e =>
        {
            e.ToTable("space_booking_settings");
            e.HasIndex(x => x.TenantId).IsUnique();
        });

        modelBuilder.Entity<RoomBooking>(e =>
        {
            e.ToTable("room_bookings");
            e.Ignore(x => x.IsOpen);
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.Note).HasMaxLength(RoomBooking.MaxNoteLength);
            e.Property(x => x.RejectReason).HasMaxLength(RoomBooking.MaxRejectReasonLength);
            e.HasIndex(x => new { x.TenantId, x.SpaceId, x.StartAt, x.EndAt });
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.CardNo });
            e.HasOne<BookableSpace>().WithMany().HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomBookingMember>(e =>
        {
            e.ToTable("room_booking_members");
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.BookingId });
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId });
            e.HasOne<RoomBooking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoomBookingBan>(e =>
        {
            e.ToTable("room_booking_bans");
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.Reason).HasMaxLength(RoomBookingBan.MaxReasonLength);
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId });
            e.HasIndex(x => new { x.TenantId, x.CardNo });
        });

        modelBuilder.Entity<AccessDevice>(e =>
        {
            e.ToTable("access_devices");
            e.Property(x => x.Code).HasMaxLength(AccessDevice.MaxCodeLength);
            e.Property(x => x.Name).HasMaxLength(AccessDevice.MaxNameLength);
            e.Property(x => x.ApiKeyHash).HasMaxLength(64);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
            e.HasOne<BookableSpace>().WithMany().HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AccessStaffCard>(e =>
        {
            e.ToTable("access_staff_cards");
            e.Property(x => x.CardUid).HasMaxLength(AccessStaffCard.MaxCardUidLength);
            e.Property(x => x.Label).HasMaxLength(AccessStaffCard.MaxLabelLength);
            e.HasIndex(x => new { x.TenantId, x.CardUid }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<AccessScanLog>(e =>
        {
            e.ToTable("access_scan_logs");
            e.Property(x => x.CardUid).HasMaxLength(64);
            e.Property(x => x.Reason).HasMaxLength(AccessScanLog.MaxReasonLength);
            e.HasIndex(x => new { x.TenantId, x.DeviceId, x.ScannedAt });
            e.HasOne<AccessDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccessCommand>(e =>
        {
            e.ToTable("access_commands");
            e.HasIndex(x => new { x.TenantId, x.DeviceId, x.AckedAt, x.ExpiresAt });
            e.HasOne<AccessDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MapFixture>(e =>
        {
            e.ToTable("map_fixtures");
            e.Property(x => x.Code).HasMaxLength(MapFixture.MaxCodeLength);
            e.Property(x => x.Name).HasMaxLength(MapFixture.MaxNameLength);
            e.Property(x => x.ObjectType).HasMaxLength(20);
            e.Property(x => x.ColorHex).HasMaxLength(20);
            e.Property(x => x.IconName).HasMaxLength(50);
            e.Property(x => x.ShelfCategoryRange).HasMaxLength(MapFixture.MaxShelfTextLength);
            e.Property(x => x.ShelfSubjectName).HasMaxLength(MapFixture.MaxShelfTextLength);
            e.Property(x => x.ShelfDescription).HasMaxLength(MapFixture.MaxShelfDescriptionLength);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.FloorId });
            e.HasOne<MapFloor>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MapFixtureEquipment>(e =>
        {
            e.ToTable("map_fixture_equipment");
            e.Property(x => x.Name).HasMaxLength(MapFixtureEquipment.MaxNameLength);
            e.Property(x => x.Description).HasMaxLength(MapFixtureEquipment.MaxDescriptionLength);
            e.HasIndex(x => new { x.TenantId, x.FixtureId });
            e.HasOne<MapFixture>().WithMany().HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MapFloorUtility>(e =>
        {
            e.ToTable("map_floor_utilities");
            e.Property(x => x.Name).HasMaxLength(MapFloorUtility.MaxNameLength);
            e.Property(x => x.Description).HasMaxLength(MapFloorUtility.MaxDescriptionLength);
            e.Property(x => x.IconName).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.FloorId });
            e.HasOne<MapFloor>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Đơn vị mới có phân hệ Không gian & đặt phòng — chưa có gì cần seed sẵn ở lát này.</summary>
public sealed class SpaceTenantSeeder : ITenantSeeder
{
    public Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return Task.CompletedTask;
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class SpaceDbContextDesignFactory : IDesignTimeDbContextFactory<SpaceDbContext>
{
    public SpaceDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SpaceDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_space")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
