using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elib.BuildingBlocks.Crud;

/// <summary>Một thao tác ghi trên danh mục CRUD cần ghi nhật ký.</summary>
public sealed record CrudAuditEntry(string EntityType, string EntityName, Guid PublicId, CrudChange Change, string? Label);

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

/// <summary>
/// Phát <see cref="AuditRecorded"/> cho service audit (thay UserLog ghi trực tiếp của monolith). Gọi TRƯỚC SaveChanges nên event
/// nằm trong outbox cùng transaction với dữ liệu — thao tác lỗi thì không có nhật ký.
/// Ngữ cảnh hệ thống (không có đơn vị) không ghi ở đây.
/// </summary>
public sealed class PublishingCrudAuditSink(
    IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor, IHttpContextAccessor http, IOptions<CrudAuditOptions> options)
    : ICrudAuditSink
{
    public Task RecordAsync(CrudAuditEntry entry, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId) return Task.CompletedTask;
        var ctx = http.HttpContext;
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
            ActorName = AuditActor.Name(ctx),
            Service = options.Value.ServiceName,
            Action = action,
            EntityType = entry.EntityType,
            EntityId = entry.PublicId.ToString(),
            Summary = $"{verb} {entry.EntityName.ToLowerInvariant()}" + (string.IsNullOrWhiteSpace(entry.Label) ? "" : $": {entry.Label}"),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
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
