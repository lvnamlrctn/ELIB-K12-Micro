using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.TenantReplica;

/// <summary>
/// Bản sao chỉ đọc thông tin đơn vị trong DB của từng service (docs 04 §4). Chỉ consumer của building block này được ghi.
/// Không ITenantOwned: khoá chính chính là TenantId, truy vấn luôn theo khoá.
/// </summary>
public sealed class TenantReplicaRecord
{
    public long TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Subdomain { get; set; } = "";
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>Provisioning | Active | Suspended | ProvisioningFailed</summary>
    public string Status { get; set; } = "Provisioning";

    /// <summary>Version của bản ghi đơn vị ở service tenant — chỉ ghi đè khi event mới hơn.</summary>
    public long SourceVersion { get; set; }

    /// <summary>Version của tập license (ModuleLicenseChanged.SourceVersion).</summary>
    public long LicenseVersion { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public List<TenantModuleReplica> Modules { get; set; } = [];
}

public sealed class TenantModuleReplica
{
    public long TenantId { get; set; }
    public string ModuleCode { get; set; } = "";

    /// <summary>Trial | Active | Suspended | Expired</summary>
    public string Status { get; set; } = "Active";

    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public bool IsEffectiveOn(DateOnly today) =>
        Status is "Active" or "Trial"
        && (ValidFrom is null || ValidFrom <= today)
        && (ValidTo is null || today <= ValidTo);
}

public static class TenantReplicaModelBuilderExtensions
{
    /// <summary>Gọi trong OnModelCreating của DbContext service, TRƯỚC base.OnModelCreating.</summary>
    public static ModelBuilder AddTenantReplica(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantReplicaRecord>(e =>
        {
            e.ToTable("tenant_replicas", "replica");
            e.HasKey(t => t.TenantId);
            e.Property(t => t.TenantId).ValueGeneratedNever();
            e.Property(t => t.Code).HasMaxLength(30);
            e.Property(t => t.Name).HasMaxLength(200);
            e.Property(t => t.Subdomain).HasMaxLength(63);
            e.Property(t => t.TimeZone).HasMaxLength(64);
            e.Property(t => t.Status).HasMaxLength(30);
            e.HasMany(t => t.Modules).WithOne().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantModuleReplica>(e =>
        {
            e.ToTable("tenant_module_replicas", "replica");
            e.HasKey(m => new { m.TenantId, m.ModuleCode });
            e.Property(m => m.ModuleCode).HasMaxLength(30);
            e.Property(m => m.Status).HasMaxLength(20);
        });

        return modelBuilder;
    }
}
