using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Notification.Application;
using Elib.Notification.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Notification.Infrastructure;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), INotificationDb, ICrudDbContext, IDataProtectionKeyContext
{
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    /// <summary>Khoá Data Protection (mã hoá mật khẩu SMTP) — dùng chung mọi pod, không thuộc đơn vị nào.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public Task<string?> TenantNameAsync(long tenantId, CancellationToken cancellationToken) =>
        Set<TenantReplicaRecord>().Where(t => t.TenantId == tenantId).Select(t => (string?)t.Name).FirstOrDefaultAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailSettings>(e =>
        {
            e.ToTable("email_settings");
            e.Property(s => s.Host).HasMaxLength(255);
            e.Property(s => s.UserName).HasMaxLength(255);
            e.Property(s => s.ProtectedPassword).HasMaxLength(2000);
            e.Property(s => s.FromAddress).HasMaxLength(EmailRules.MaxLength);
            e.Property(s => s.FromName).HasMaxLength(200);
            e.HasIndex(s => s.TenantId).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<EmailTemplate>(e =>
        {
            e.ToTable("email_templates");
            e.Property(t => t.Code).HasMaxLength(50);
            e.Property(t => t.Name).HasMaxLength(200);
            e.Property(t => t.Subject).HasMaxLength(300);
            e.Property(t => t.Body).HasMaxLength(EmailTemplate.MaxBodyLength);
            e.HasIndex(t => new { t.TenantId, t.Code }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<NotificationLog>(e =>
        {
            e.ToTable("notification_logs");
            e.Property(l => l.Channel).HasMaxLength(20);
            e.Property(l => l.TemplateCode).HasMaxLength(50);
            e.Property(l => l.Recipient).HasMaxLength(EmailRules.MaxLength);
            e.Property(l => l.Subject).HasMaxLength(300);
            e.Property(l => l.Error).HasMaxLength(1000);
            e.Property(l => l.DeduplicationKey).HasMaxLength(200);
            e.HasIndex(l => new { l.TenantId, l.DeduplicationKey }).HasFilter("deduplication_key IS NOT NULL");
            e.HasIndex(l => new { l.TenantId, l.CreatedAt });
        });

        modelBuilder.Entity<DataProtectionKey>(e => e.ToTable("data_protection_keys"));

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class NotificationDbContextDesignFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_notification")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
