using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Payment.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Elib.Payment.Infrastructure;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options, ITenantContext tenant)
    : ElibDbContext(options, tenant), ICrudDbContext, IDataProtectionKeyContext
{
    /// <summary>Khoá Data Protection (mã hoá bí mật cổng thanh toán) — dùng chung mọi pod, không thuộc đơn vị nào.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentAccount>(e =>
        {
            e.ToTable("payment_accounts");
            e.Property(x => x.BankBin).HasMaxLength(6);
            e.Property(x => x.AccountNo).HasMaxLength(19);
            e.Property(x => x.AccountName).HasMaxLength(100);
            e.Property(x => x.ProtectedSepayApiKey).HasMaxLength(2000);
            e.Property(x => x.VnPayTmnCode).HasMaxLength(20);
            e.Property(x => x.ProtectedVnPayHashSecret).HasMaxLength(2000);
            e.Property(x => x.VnPayPaymentUrl).HasMaxLength(300);
            e.HasIndex(x => x.TenantId).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<Charge>(e =>
        {
            e.ToTable("charges");
            e.Ignore(x => x.IsOpen);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.SourceService).HasMaxLength(30);
            e.Property(x => x.CardNo).HasMaxLength(50);
            e.Property(x => x.Label).HasMaxLength(250);
            e.Property(x => x.Amount).HasPrecision(18, 0);
            e.Property(x => x.Status).HasMaxLength(10);
            e.Property(x => x.PaidTransactionCode).HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.SourceService, x.SourceId }).IsUnique().HasFilter("is_deleted = false");
            e.HasIndex(x => new { x.TenantId, x.ReaderPublicId, x.Status });
        });

        modelBuilder.Entity<ChargeSyncState>(e =>
        {
            e.ToTable("charge_sync_states");
            e.Property(x => x.SourceService).HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.SourceService }).IsUnique().HasFilter("is_deleted = false");
        });

        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.ToTable("payment_transactions");
            e.Property(x => x.TransactionCode).HasMaxLength(30);
            e.Property(x => x.Provider).HasMaxLength(10);
            e.Property(x => x.Amount).HasPrecision(18, 0);
            e.Property(x => x.PaidAmount).HasPrecision(18, 0);
            // Hai webhook cùng lúc: chỉ một bên ghi nhận được, bên kia nhận DbUpdateConcurrencyException → xử lý như đã thanh toán.
            e.Property(x => x.Status).HasMaxLength(10).IsConcurrencyToken();
            e.Property(x => x.QrContent).HasMaxLength(2000);
            e.Property(x => x.ProviderRef).HasMaxLength(100);
            e.Property(x => x.ReviewNote).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.TransactionCode }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Provider, x.ProviderRef }).IsUnique().HasFilter("provider_ref IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.ChargeId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt });
        });

        modelBuilder.Entity<WebhookLog>(e =>
        {
            e.ToTable("webhook_logs");
            e.Property(x => x.Provider).HasMaxLength(10);
            e.Property(x => x.TransactionCode).HasMaxLength(30);
            e.Property(x => x.Payload).HasMaxLength(WebhookLog.MaxPayload);
            e.Property(x => x.Result).HasMaxLength(200);
            e.Property(x => x.RemoteIp).HasMaxLength(64);
            e.HasIndex(x => new { x.TenantId, x.ReceivedAt });
        });

        modelBuilder.Entity<DataProtectionKey>(e => e.ToTable("data_protection_keys"));

        modelBuilder.AddTenantReplica();
        modelBuilder.AddElibOutbox();
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Cho `dotnet ef migrations` — không kết nối DB khi chỉ sinh migration.</summary>
public sealed class PaymentDbContextDesignFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql("Host=localhost;Database=elib_payment")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TenantContext());
}
