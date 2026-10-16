using System.Text.Json.Nodes;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Crud;

/// <summary>Một thao tác ghi trên danh mục CRUD cần ghi nhật ký. <see cref="Changes"/>: diff trước/sau (<see cref="CrudDiff"/>).</summary>
public sealed record CrudAuditEntry(string EntityType, string EntityName, Guid PublicId, CrudChange Change, string? Label,
    JsonObject? Changes = null);

/// <summary>Nơi nhận nhật ký thao tác của <see cref="CrudResource{TSelf,TEntity,TSearch,TRequest,TDto}"/>.</summary>
public interface ICrudAuditSink
{
    Task RecordAsync(CrudAuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>Resource nhận <see cref="ICrudAuditSink"/> từ DI (đặt bởi AddCrudResource).</summary>
public interface ICrudAuditable
{
    ICrudAuditSink? AuditSink { set; }
}

public sealed class CrudAuditOptions
{
    public string ServiceName { get; set; } = "";
}

/// <summary>Người thao tác của một việc chạy nền: lưu lúc cán bộ bấm chạy, worker đặt lại để nhật ký/event ghi đúng người, lý do, IP.</summary>
public sealed record CrudAuditOrigin(long? ActorId, string ActorKind, string? ActorName, string? Reason, string? IpAddress);

/// <summary>
/// Ngữ cảnh nhật ký trong một scope. Request HTTP: lấy từ token/header. Worker (không có HttpContext): <see cref="RunAs"/> với
/// <see cref="CrudAuditOrigin"/> đã lưu — đặt luôn <see cref="CurrentActor"/> cho event.
/// </summary>
public sealed class CrudAuditContext(IHttpContextAccessor http, ICurrentActor actor, CurrentActor actorSetter)
{
    public CrudAuditOrigin? Origin { get; private set; }

    /// <summary>Người thao tác thật của lời gọi nội bộ giữa service (<see cref="ActingAsFilter"/>) — thay tài khoản service trong cột audit, event, nhật ký.</summary>
    public CrudAuditOrigin? ActingAs { get; private set; }

    /// <summary>Người thao tác hiện tại — để lưu kèm việc chạy nền.</summary>
    public CrudAuditOrigin Capture()
    {
        if (Origin is not null) return Origin;
        var ctx = http.HttpContext;
        return new CrudAuditOrigin(actor.Id, actor.Kind, AuditActor.Name(ctx), CrudDiff.Reason(ctx?.Request.Headers[CrudDiff.ReasonHeader].ToString()),
            ctx?.Connection.RemoteIpAddress?.ToString());
    }

    public void ActAs(CrudAuditOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ActingAs = origin;
        actorSetter.Set(origin.ActorId, origin.ActorKind);
    }

    public void RunAs(CrudAuditOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        Origin = origin;
        actorSetter.Set(origin.ActorId, origin.ActorKind);
    }
}

/// <summary>
/// Phát <see cref="AuditRecorded"/> cho service audit (thay UserLog ghi trực tiếp của monolith). Gọi TRƯỚC SaveChanges nên event
/// nằm trong outbox cùng transaction với dữ liệu — thao tác lỗi thì không có nhật ký.
/// Ngữ cảnh hệ thống (không có đơn vị) không ghi ở đây. Lý do sửa lấy từ header X-Change-Reason (mã hoá URL), ghi vào ChangesJson; chạy nền thì lấy từ <see cref="CrudAuditContext"/>.
/// </summary>
public sealed class PublishingCrudAuditSink(
    IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor, IHttpContextAccessor http, IOptions<CrudAuditOptions> options,
    CrudAuditContext? context = null)
    : ICrudAuditSink
{
    public Task RecordAsync(CrudAuditEntry entry, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId) return Task.CompletedTask;
        var ctx = http.HttpContext;
        var origin = ctx is null ? context?.Origin : context?.ActingAs;
        var (action, verb) = entry.Change switch
        {
            CrudChange.Added => ("ADD", "Thêm"),
            CrudChange.Updated => ("UPDATE", "Sửa"),
            CrudChange.Deleted => ("DELETE", "Xoá"),
            CrudChange.Imported => ("IMPORT", "Nhập từ Excel"),
            _ => ("CHANGE_STATUS", "Đổi trạng thái"),
        };
        return publisher.Publish(new AuditRecorded
        {
            TenantId = tenantId,
            Actor = new EventActor(actor.Id, actor.Kind),
            ActorName = origin is null ? AuditActor.Name(ctx) : origin.ActorName,
            Service = options.Value.ServiceName,
            Action = action,
            EntityType = entry.EntityType,
            EntityId = entry.PublicId.ToString(),
            Summary = $"{verb} {entry.EntityName.ToLowerInvariant()}" + (string.IsNullOrWhiteSpace(entry.Label) ? "" : $": {entry.Label}"),
            ChangesJson = CrudDiff.ToJson(entry.Changes, origin is null ? CrudDiff.Reason(ctx?.Request.Headers[CrudDiff.ReasonHeader].ToString()) : origin.Reason),
            IpAddress = origin is null ? ctx?.Connection.RemoteIpAddress?.ToString() : origin.IpAddress,
            CorrelationId = ctx?.TraceIdentifier,
        }, cancellationToken);
    }
}

public static class AuditActor
{
    /// <summary>"Họ tên (tên đăng nhập)" từ claim của token — audit không tra ngược identity.</summary>
    public static string? Name(HttpContext? ctx)
    {
        var user = ctx?.User;
        if (user?.Identity?.IsAuthenticated != true) return null;
        var name = user.FindFirst("name")?.Value;
        var userName = user.FindFirst("preferred_username")?.Value;
        return (name, userName) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{name} ({userName})",
            ({ Length: > 0 }, _) => name,
            (_, { Length: > 0 }) => userName,
            _ => null,
        };
    }
}
