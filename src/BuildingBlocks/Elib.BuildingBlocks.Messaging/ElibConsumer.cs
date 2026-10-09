using Elib.BuildingBlocks.Authorization;
using Elib.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.BuildingBlocks.Messaging;

/// <summary>
/// Base consumer cho integration event. Nếu <see cref="RequiredModule"/> có giá trị và đơn vị chưa mua module đó,
/// event được bỏ qua (ack, ghi log) thay vì xử lý — tắt module thì consumer của module cũng dừng (docs 04 §3).
/// Consumer PHẢI idempotent: inbox chống trùng theo EventId, nhưng nghiệp vụ vẫn nên upsert theo khoá + SourceVersion.
/// </summary>
public abstract partial class ElibConsumer<TEvent>(IModuleLicenseSource licenses, ILogger logger) : IConsumer<TEvent>
    where TEvent : IntegrationEvent
{
    /// <summary>Mã module cần license; null = luôn xử lý (event nền tảng).</summary>
    protected virtual string? RequiredModule => null;

    public async Task Consume(ConsumeContext<TEvent> context)
    {
        if (RequiredModule is { } module
            && !await licenses.IsLicensedAsync(context.Message.TenantId, module, context.CancellationToken))
        {
            LogSkipped(logger, typeof(TEvent).Name, context.Message.EventId, context.Message.TenantId, module);
            return;
        }

        await HandleAsync(context.Message, context);
    }

    protected abstract Task HandleAsync(TEvent message, ConsumeContext<TEvent> context);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Bỏ qua {EventType} {EventId}: đơn vị {TenantId} chưa có license {Module}")]
    private static partial void LogSkipped(ILogger logger, string eventType, Guid eventId, long tenantId, string module);
}
