using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.Tenant.Application;
using Elib.Tenant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Tenant.Infrastructure;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ITenantDb, ICrudDbContext
{
    public DbSet<Domain.Tenant> Tenants => Set<Domain.Tenant>();
    public DbSet<TenantModuleLicense> Licenses => Set<TenantModuleLicense>();
    public DbSet<ModuleDefinition> Modules => Set<ModuleDefinition>();
    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();
    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Nationality> Nationalities => Set<Nationality>();
    public DbSet<Ethnicity> Ethnicities => Set<Ethnicity>();
    public DbSet<AcademicTitle> AcademicTitles => Set<AcademicTitle>();
    public DbSet<Degree> Degrees => Set<Degree>();
    public DbSet<Position> Positions => Set<Position>();

    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        Database.CreateExecutionStrategy().ExecuteAsync(work, async (w, ct) =>
        {
            ChangeTracker.Clear(); // lần thử lại bắt đầu sạch, không mang entity của lần lỗi trước
            await using var transaction = await Database.BeginTransactionAsync(ct);
            var result = await w(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Tenant>(e =>
        {
            e.ToTable("tenants");
            e.Property(t => t.Code).HasMaxLength(30);
            e.Property(t => t.Name).HasMaxLength(200);
            e.Property(t => t.Subdomain).HasMaxLength(63);
            e.Property(t => t.TimeZone).HasMaxLength(64);
            e.Property(t => t.ProvisioningError).HasMaxLength(2000);
            e.Property(t => t.LogoUrl).HasMaxLength(500);
            e.Property(t => t.LogoText).HasMaxLength(50);
            e.Property(t => t.Version).IsConcurrencyToken();
            e.HasIndex(t => t.Code).IsUnique();
            e.HasIndex(t => t.Subdomain).IsUnique();
            e.HasMany(t => t.ProvisioningSteps).WithOne().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(t => t.ProvisioningSteps).HasField("_steps").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProvisioningStep>(e =>
        {
            e.ToTable("tenant_provisioning_steps");
            e.HasKey(s => new { s.TenantId, s.Service });
            e.Property(s => s.Service).HasMaxLength(50);
            e.Property(s => s.Error).HasMaxLength(1000);
        });

        modelBuilder.Entity<ModuleDefinition>(e =>
        {
            e.ToTable("modules");
            e.HasKey(m => m.Code);
            e.Property(m => m.Code).HasMaxLength(30);
            e.Property(m => m.Package).HasMaxLength(30);
            e.Property(m => m.Name).HasMaxLength(100);
            e.Property(m => m.Service).HasMaxLength(50);
            e.HasData(ModuleCatalog.All);
        });

        modelBuilder.Entity<TenantModuleLicense>(e =>
        {
            e.ToTable("tenant_module_licenses");
            e.Property(l => l.ModuleCode).HasMaxLength(30);
            // Duy nhất trong các bản ghi chưa xoá — bán lại module đã ngừng sẽ tạo bản ghi mới.
            e.HasIndex(l => new { l.TenantId, l.ModuleCode }).IsUnique().HasFilter("is_deleted = false");
            e.HasOne<ModuleDefinition>().WithMany().HasForeignKey(l => l.ModuleCode).OnDelete(DeleteBehavior.Restrict);
        });

        // Mã/tên duy nhất trong đơn vị, chỉ tính bản ghi chưa xoá (xoá rồi thêm lại được).
        modelBuilder.Entity<SystemParameter>(e =>
        {
            e.ToTable("system_parameters");
            e.Property(p => p.Code).HasMaxLength(100);
            e.Property(p => p.Value).HasMaxLength(4000);
            e.Property(p => p.Description).HasMaxLength(350);
            e.Property(p => p.DescriptionEn).HasMaxLength(350);
            e.Property(p => p.Type).HasMaxLength(50);
            e.Property(p => p.Service).HasMaxLength(50);
            e.HasIndex(p => new { p.TenantId, p.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<Org>(e =>
        {
            e.ToTable("orgs");
            e.Property(o => o.Name).HasMaxLength(300);
            e.Property(o => o.Link).HasMaxLength(200);
            e.HasIndex(o => new { o.TenantId, o.ParentId });
        });

        modelBuilder.Entity<Currency>(e =>
        {
            e.ToTable("currencies");
            e.Property(c => c.Code).HasMaxLength(10);
            e.Property(c => c.Name).HasMaxLength(150);
            e.Property(c => c.ExchangeRate).HasPrecision(18, 4);
            e.HasIndex(c => new { c.TenantId, c.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.NamedCatalog<Nationality>("nationalities", 150);
        modelBuilder.NamedCatalog<Ethnicity>("ethnicities", 250);
        modelBuilder.NamedCatalog<AcademicTitle>("academic_titles", 250);
        modelBuilder.NamedCatalog<Degree>("degrees", 150);
        modelBuilder.NamedCatalog<Position>("positions", 100);

        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class TenantDbContextDesignFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_tenant")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
