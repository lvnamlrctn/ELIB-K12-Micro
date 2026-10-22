using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Payment.Application;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Payment.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "payment";

    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PaymentDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:PaymentDb (đặt qua biến môi trường ConnectionStrings__PaymentDb).");

        services.AddElibPostgres<PaymentDbContext>(connectionString);
        services.AddCrudDbContext<PaymentDbContext>(ServiceName);
        services.AddTenantReplica<PaymentDbContext>(ServiceName);
        services.AddTenantReplicaBootstrap<PaymentDbContext>(configuration);
        services.AddScoped<ITenantSeeder, PaymentTenantSeeder>();
        services.AddElibMessaging<PaymentDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<PaymentDbContext>();
            x.AddConsumer<ChargeIssuedConsumer>();
            x.AddConsumer<ChargeWaivedConsumer>();
            x.AddConsumer<ChargeSettledConsumer>();
            x.AddPermissionCacheInvalidation();
        });

        services.AddDataProtection()
            .SetApplicationName("elib-payment")
            .PersistKeysToDbContext<PaymentDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddChargeSources(configuration);
        services.AddSingleton<WebhookLogPurgeJob>();
        services.AddHostedService(sp => sp.GetRequiredService<WebhookLogPurgeJob>());
        services.AddSingleton<PaymentExpiryJob>();
        services.AddHostedService(sp => sp.GetRequiredService<PaymentExpiryJob>());
        services.AddHealthChecks().AddDbContextCheck<PaymentDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}

/// <summary>Đơn vị mới: payment chưa có gì phải khởi tạo (đơn vị tự khai tài khoản nhận) — chỉ để báo TenantSeeded.</summary>
public sealed class PaymentTenantSeeder : ITenantSeeder
{
    public Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Mã hoá bí mật cổng thanh toán bằng Data Protection (khoá lưu trong bảng data_protection_keys của service).</summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Elib.Payment.GatewaySecret.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
