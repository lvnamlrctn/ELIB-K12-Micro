using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Hosting;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Persistence;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Notification.Application;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Notification.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ServiceName = "notification";

    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Thiếu ConnectionStrings:NotificationDb (đặt qua biến môi trường ConnectionStrings__NotificationDb).");

        services.AddElibPostgres<NotificationDbContext>(connectionString);
        services.AddScoped<INotificationDb>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddCrudDbContext<NotificationDbContext>(ServiceName);
        services.AddTenantReplica<NotificationDbContext>(ServiceName);
        services.AddScoped<ITenantSeeder, NotificationTenantSeeder>();
        services.AddElibMessaging<NotificationDbContext>(ServiceName, configuration, x =>
        {
            x.AddTenantReplicaConsumers<NotificationDbContext>();
            x.AddConsumer<NotificationRequestedConsumer>();
            x.AddConsumer<SystemNotificationRequestedConsumer>();
            x.AddPermissionCacheInvalidation();
        });

        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddDataProtection()
            .SetApplicationName("elib-notification")
            .PersistKeysToDbContext<NotificationDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<ISmtpSender, MailKitSmtpSender>();
        services.AddHealthChecks().AddDbContextCheck<NotificationDbContext>("db", tags: [ElibHealthTags.Ready]);
        return services;
    }
}
