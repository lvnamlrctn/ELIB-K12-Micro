using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Identity.Application;
using Elib.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenIddict.EntityFrameworkCore.Models;

namespace Elib.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), IIdentityDb
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<TenantReplicaRecord> TenantReplicas => Set<TenantReplicaRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StaffUser>(e =>
        {
            e.ToTable("staff_users");
            e.Property(u => u.UserName).HasMaxLength(64);
            e.Property(u => u.FullName).HasMaxLength(200);
            e.Property(u => u.Email).HasMaxLength(200);
            e.Property(u => u.Phone).HasMaxLength(30);
            e.Property(u => u.PermissionStamp).HasMaxLength(32);
            e.ComplexProperty(u => u.Credentials, c => c.Property(x => x.PasswordHash).HasMaxLength(100));
            e.HasIndex(u => new { u.TenantId, u.UserName }).IsUnique().HasFilter("is_deleted = false");
            e.HasMany(u => u.Roles).WithMany().UsingEntity("staff_user_roles");
            e.Navigation(u => u.Roles).HasField("_roles").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SystemUser>(e =>
        {
            e.ToTable("system_users");
            e.Property(u => u.UserName).HasMaxLength(64);
            e.Property(u => u.FullName).HasMaxLength(200);
            e.Property(u => u.Email).HasMaxLength(200);
            e.Property(u => u.PermissionStamp).HasMaxLength(32);
            e.ComplexProperty(u => u.Credentials, c => c.Property(x => x.PasswordHash).HasMaxLength(100));
            e.HasIndex(u => u.UserName).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Description).HasMaxLength(500);
            e.HasIndex(r => new { r.TenantId, r.Name }).IsUnique().HasFilter("is_deleted = false");
        });

        // OpenIddict tự đặt tên bảng PascalCase — đổi về snake_case cho nhất quán.
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreApplication>().ToTable("openiddict_applications");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreAuthorization>().ToTable("openiddict_authorizations");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreScope>().ToTable("openiddict_scopes");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreToken>().ToTable("openiddict_tokens");

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class IdentityDbContextDesignFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_identity")
            .UseSnakeCaseNamingConvention()
            .UseOpenIddict()
            .Options,
        new TenantContext());
}
