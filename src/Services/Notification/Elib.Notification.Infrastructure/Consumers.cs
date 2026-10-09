using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.TenantReplica;
using Elib.Contracts.Events.Platform;
using Elib.Notification.Application;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Elib.Notification.Infrastructure;

/// <summary>
/// Service nghiệp vụ yêu cầu gửi tin (docs 03 §3.2). GĐ0 chỉ có kênh email; yêu cầu chỉ định kênh khác thì bỏ qua.
/// Lỗi SMTP được ghi vào nhật ký, không ném ra (không retry gửi lại thư có thể đã tới nơi).
/// </summary>
public sealed class NotificationRequestedConsumer(
    EmailDispatcher dispatcher, IModuleLicenseSource licenses, ILogger<NotificationRequestedConsumer> logger)
    : ElibConsumer<NotificationRequested>(licenses, logger)
{
    protected override async Task HandleAsync(NotificationRequested message, ConsumeContext<NotificationRequested> context)
    {
        if (message.Channels.Count > 0 && !message.Channels.Contains("email", StringComparer.OrdinalIgnoreCase)) return;

        var data = new Dictionary<string, string>(message.Data, StringComparer.Ordinal);
        if (message.Recipient.DisplayName is { } name) data.TryAdd("full_name", name);

        await dispatcher.SendAsync(new EmailRequest(message.TemplateCode, message.Recipient.Email, message.Recipient.DisplayName,
            data, message.DeduplicationKey, message.EventId), context.CancellationToken);
    }
}

/// <summary>Thư cấp nền tảng (OTP của quản trị nền tảng…) — không có đơn vị, chỉ ghi log.</summary>
public sealed partial class SystemNotificationRequestedConsumer(EmailDispatcher dispatcher, ILogger<SystemNotificationRequestedConsumer> logger)
    : IConsumer<SystemNotificationRequested>
{
    public async Task Consume(ConsumeContext<SystemNotificationRequested> context)
    {
        var m = context.Message;
        var data = new Dictionary<string, string>(m.Data, StringComparer.Ordinal);
        if (m.Recipient.DisplayName is { } name) data.TryAdd("full_name", name);
        var error = await dispatcher.SendSystemAsync(m.TemplateCode, m.Recipient.Email, m.Recipient.DisplayName, data, context.CancellationToken);
        if (error is null) LogSent(logger, m.TemplateCode, m.EventId);
        else LogFailed(logger, m.TemplateCode, m.EventId, error);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã gửi thư nền tảng {TemplateCode} ({EventId})")]
    private static partial void LogSent(ILogger logger, string templateCode, Guid eventId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không gửi được thư nền tảng {TemplateCode} ({EventId}): {Error}")]
    private static partial void LogFailed(ILogger logger, string templateCode, Guid eventId, string error);
}

/// <summary>Đơn vị mới: thêm các mẫu email mặc định để admin đơn vị chỉnh được.</summary>
public sealed class NotificationTenantSeeder(EmailTemplateResource templates) : ITenantSeeder
{
    public Task SeedAsync(TenantProvisioned tenant, CancellationToken cancellationToken) =>
        templates.AddMissingDefaultsAsync(cancellationToken);
}
