using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using MassTransit;

namespace Elib.BuildingBlocks.Messaging;

/// <summary>
/// Đặt ngữ cảnh đơn vị + người thực hiện cho consumer từ envelope (docs 04 §2.2):
/// <see cref="IntegrationEvent"/> → đơn vị của event; <see cref="SystemEvent"/> → ngữ cảnh hệ thống;
/// message khác (nội bộ MassTransit) → để nguyên (chưa xác định, fail-closed).
/// </summary>
public sealed class TenantConsumeFilter<T>(TenantContext tenant, CurrentActor actor) : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        IDisposable? scope = null;
        switch (context.Message)
        {
            case IntegrationEvent e:
                scope = tenant.Use(e.TenantId);
                actor.Set(e.Actor.Id, e.Actor.Kind);
                break;
            case SystemEvent s:
                scope = tenant.UseSystem();
                actor.Set(s.Actor.Id, s.Actor.Kind);
                break;
        }

        try
        {
            await next.Send(context);
        }
        finally
        {
            scope?.Dispose();
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("elibTenant");
}

/// <summary>Dùng EventId làm MessageId để inbox của consumer chống trùng theo đúng định danh sự kiện.</summary>
public sealed class EventIdPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        EventIds.Apply(context);
        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("elibEventId");
}

public sealed class EventIdSendFilter<T> : IFilter<SendContext<T>> where T : class
{
    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        EventIds.Apply(context);
        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("elibEventId");
}

internal static class EventIds
{
    public static void Apply<T>(SendContext<T> context) where T : class
    {
        context.MessageId = context.Message switch
        {
            IntegrationEvent e => e.EventId,
            SystemEvent s => s.EventId,
            _ => context.MessageId,
        };
    }
}
