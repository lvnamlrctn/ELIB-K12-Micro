using Elib.Audit.Application;
using Elib.Audit.Domain;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Messaging;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elib.Audit.Infrastructure;

/// <summary>Nhật ký thao tác của đơn vị — chạy trong ngữ cảnh đơn vị của event (TenantId do interceptor gán).</summary>
public sealed class AuditRecordedConsumer(IAuditDb db, IModuleLicenseSource licenses, ILogger<AuditRecordedConsumer> logger)
    : ElibConsumer<AuditRecorded>(licenses, logger)
{
    protected override async Task HandleAsync(AuditRecorded m, ConsumeContext<AuditRecorded> context)
    {
        if (await db.AuditLogs.AnyAsync(l => l.EventId == m.EventId, context.CancellationToken)) return;
        db.AuditLogs.Add(AuditLog.Create(m.EventId, m.OccurredAt, m.Actor.Id, m.Actor.Kind, m.ActorName, m.Service, m.Action,
            m.EntityType, m.EntityId, m.Summary, m.ChangesJson, m.IpAddress, m.CorrelationId));
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Nhật ký cấp nền tảng — ngữ cảnh hệ thống, bảng không thuộc đơn vị.</summary>
public sealed class SystemAuditRecordedConsumer(IAuditDb db) : IConsumer<SystemAuditRecorded>
{
    public async Task Consume(ConsumeContext<SystemAuditRecorded> context)
    {
        var m = context.Message;
        if (await db.PlatformAuditLogs.AnyAsync(l => l.EventId == m.EventId, context.CancellationToken)) return;
        db.PlatformAuditLogs.Add(PlatformAuditLog.Create(m.EventId, m.OccurredAt, m.Actor.Id, m.Actor.Kind, m.ActorName, m.Service, m.Action,
            m.EntityType, m.EntityId, m.TenantId, m.Summary, m.IpAddress));
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Xoá nhật ký quá hạn lưu (Audit:RetentionDays) mỗi ngày — ngữ cảnh hệ thống, xoá cho mọi đơn vị một lượt.</summary>
public sealed partial class AuditRetentionService(IServiceScopeFactory scopes, IOptions<AuditOptions> options, TimeProvider clock,
    ILogger<AuditRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        if (options.Value.RetentionDays <= 0) return 0;
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        using var scope = scopes.CreateScope();
        using var system = scope.ServiceProvider.GetRequiredService<ITenantContext>().UseSystem();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var removed = await db.AuditLogs.IgnoreQueryFilters().Where(l => l.OccurredAt < cutoff).ExecuteDeleteAsync(ct)
                      + await db.PlatformAuditLogs.Where(l => l.OccurredAt < cutoff).ExecuteDeleteAsync(ct);
        if (removed > 0) LogPurged(logger, removed, options.Value.RetentionDays);
        return removed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Đã xoá {Count} dòng nhật ký cũ hơn {Days} ngày")]
    private static partial void LogPurged(ILogger logger, int count, int days);

    [LoggerMessage(Level = LogLevel.Error, Message = "Xoá nhật ký quá hạn thất bại")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
