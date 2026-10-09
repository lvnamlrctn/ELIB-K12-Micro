using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.AspNetCore.Http;

namespace Elib.Tenant.Application;

/// <summary>Nhật ký cấp nền tảng của thao tác quản trị đơn vị (tạo, tạm ngưng, bán phân hệ…) — publish trước SaveChanges (outbox).</summary>
public sealed class PlatformAudit(IPublishEndpoint publisher, ICurrentActor actor, IHttpContextAccessor http)
{
    public Task RecordAsync(Domain.Tenant tenant, string action, string summary, CancellationToken ct)
    {
        var ctx = http.HttpContext;
        return publisher.Publish(new SystemAuditRecorded
        {
            Actor = new EventActor(actor.Id, actor.Kind),
            ActorName = AuditActor.Name(ctx),
            Service = "tenant",
            Action = action,
            EntityType = "Tenant",
            EntityId = tenant.PublicId.ToString(),
            TenantId = tenant.Id,
            Summary = $"{tenant.Code}: {summary}",
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
        }, ct);
    }
}
