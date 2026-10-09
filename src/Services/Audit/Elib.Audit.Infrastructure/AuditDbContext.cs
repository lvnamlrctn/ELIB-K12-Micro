using Elib.Audit.Application;
using Elib.Audit.Domain;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Audit.Infrastructure;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), IAuditDb
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();

    public Task<Dictionary<long, string>> TenantNamesAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken) =>
        tenantIds.Count == 0
            ? Task.FromResult(new Dictionary<long, string>())
            : Set<TenantReplicaRecord>().Where(t => tenantIds.Contains(t.TenantId)).ToDictionaryAsync(t => t.TenantId, t => t.Name, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.Property(l => l.ActorKind).HasMaxLength(20);
            e.Property(l => l.ActorName).HasMaxLength(300);
            e.Property(l => l.Service).HasMaxLength(50);
            e.Property(l => l.Action).HasMaxLength(60);
            e.Property(l => l.EntityType).HasMaxLength(100);
            e.Property(l => l.EntityId).HasMaxLength(100);
            e.Property(l => l.Summary).HasMaxLength(2000);
            e.Property(l => l.ChangesJson).HasMaxLength(8000);
            e.Property(l => l.IpAddress).HasMaxLength(64);
            e.Property(l => l.CorrelationId).HasMaxLength(100);
            e.HasIndex(l => new { l.TenantId, l.EventId }).IsUnique();
            e.HasIndex(l => new { l.TenantId, l.OccurredAt });
            e.HasIndex(l => new { l.TenantId, l.Action });
        });

        modelBuilder.Entity<PlatformAuditLog>(e =>
        {
            e.ToTable("platform_audit_logs");
            e.Property(l => l.ActorKind).HasMaxLength(20);
            e.Property(l => l.ActorName).HasMaxLength(300);
            e.Property(l => l.Service).HasMaxLength(50);
            e.Property(l => l.Action).HasMaxLength(60);
            e.Property(l => l.EntityType).HasMaxLength(100);
            e.Property(l => l.EntityId).HasMaxLength(100);
            e.Property(l => l.Summary).HasMaxLength(2000);
            e.Property(l => l.IpAddress).HasMaxLength(64);
            e.HasIndex(l => l.EventId).IsUnique();
            e.HasIndex(l => l.OccurredAt);
            e.HasIndex(l => new { l.TargetTenantId, l.OccurredAt });
        });

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class AuditDbContextDesignFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_audit")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
