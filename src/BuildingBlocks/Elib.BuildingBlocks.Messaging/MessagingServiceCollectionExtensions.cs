using Elib.BuildingBlocks.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.BuildingBlocks.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>RabbitMq (mặc định) hoặc InMemory (chỉ dùng cho test/dev một process).</summary>
    public string Transport { get; set; } = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";

    /// <summary>Lấy từ secret (Messaging__Username / Messaging__Password).</summary>
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Tắt chỉ trong test không có DB. Production luôn bật (docs 03 §3.1).</summary>
    public bool UseOutbox { get; set; } = true;
}

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// MassTransit chuẩn của service: queue "{service}-{consumer}", quorum queue, retry, EF outbox (publisher) + inbox (consumer)
    /// trên <typeparamref name="TDbContext"/>, filter tenant và EventId. DbContext phải gọi <c>modelBuilder.AddElibOutbox()</c>.
    /// </summary>
    public static IServiceCollection AddElibMessaging<TDbContext>(
        this IServiceCollection services, string serviceName, IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registerConsumers = null)
        where TDbContext : ElibDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        var options = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(serviceName + "-", includeNamespace: false));
            registerConsumers?.Invoke(x);

            if (options.UseOutbox)
            {
                x.AddEntityFrameworkOutbox<TDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                    o.DuplicateDetectionWindow = TimeSpan.FromDays(7);
                });
            }

            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                if (endpoint is IRabbitMqReceiveEndpointConfigurator rabbit) rabbit.SetQuorumQueue();
                endpoint.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));
                if (options.UseOutbox) endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            if (string.Equals(options.Transport, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                x.UsingInMemory((context, bus) =>
                {
                    bus.UseElibFilters(context);
                    bus.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, bus) =>
                {
                    bus.Host(options.Host, options.Port, options.VirtualHost, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });
                    bus.UseElibFilters(context);
                    bus.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }

    /// <summary>Filter dùng chung — tách riêng để test harness áp dụng y hệt.</summary>
    public static void UseElibFilters(this IBusFactoryConfigurator bus, IRegistrationContext context)
    {
        bus.UseConsumeFilter(typeof(TenantConsumeFilter<>), context);
        bus.UsePublishFilter(typeof(EventIdPublishFilter<>), context);
        bus.UseSendFilter(typeof(EventIdSendFilter<>), context);
    }

    /// <summary>Bảng outbox/inbox của MassTransit, đặt trong schema "masstransit". Gọi trong OnModelCreating của DbContext service.</summary>
    public static ModelBuilder AddElibOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity(e => e.ToTable("inbox_state", "masstransit"));
        modelBuilder.AddOutboxMessageEntity(e => e.ToTable("outbox_message", "masstransit"));
        modelBuilder.AddOutboxStateEntity(e => e.ToTable("outbox_state", "masstransit"));
        return modelBuilder;
    }
}
