using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.AspNetCore.Http;

namespace Elib.Identity.Application;

/// <summary>
/// Nhật ký thao tác của identity gửi service audit: đơn vị → <see cref="AuditRecorded"/>, cấp nền tảng → <see cref="SystemAuditRecorded"/>.
/// Publish trước SaveChanges (outbox). Người thực hiện mặc định là người đang gọi API; luồng đăng nhập truyền tường minh.
/// Không bao giờ ghi mật khẩu, mã OTP, token.
/// </summary>
public sealed class IdentityAudit(IPublishEndpoint publisher, ICurrentActor actor, IHttpContextAccessor http)
{
    public const string Service = "identity";

    public sealed record Who(long? Id, string? Name);

    public Task TenantAsync(long tenantId, string action, string entityType, string? entityId, string summary, CancellationToken ct, Who? who = null)
    {
        var ctx = http.HttpContext;
        return publisher.Publish(new AuditRecorded
        {
            TenantId = tenantId,
            Actor = Actor(who),
            ActorName = who is null ? CallerName(ctx) : who.Name,
            Service = Service,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = ctx?.TraceIdentifier,
        }, ct);
    }

    public Task SystemAsync(string action, string entityType, string? entityId, string summary, long? tenantId, CancellationToken ct, Who? who = null)
    {
        var ctx = http.HttpContext;
        return publisher.Publish(new SystemAuditRecorded
        {
            Actor = Actor(who),
            ActorName = who is null ? CallerName(ctx) : who.Name,
            Service = Service,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TenantId = tenantId,
            Summary = summary,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
        }, ct);
    }

    /// <summary>Đăng nhập/đổi mật khẩu: nhật ký của đơn vị với tài khoản đơn vị, nhật ký nền tảng với tài khoản hệ thống.</summary>
    public Task AccountAsync(long? tenantId, string action, string? entityId, string summary, Who who, CancellationToken ct) =>
        tenantId is { } t
            ? TenantAsync(t, action, "User", entityId, summary, ct, who)
            : SystemAsync(action, "SystemUser", entityId, summary, null, ct, who);

    public static Who Of(SignInIdentity identity) => new(identity.UserId, $"{identity.FullName} ({identity.UserName})");

    private EventActor Actor(Who? who) =>
        who is null ? new EventActor(actor.Id, actor.Kind) : new EventActor(who.Id, who.Id is null ? "anonymous" : ElibSubjectTypes.Staff);

    private static string? CallerName(HttpContext? ctx)
    {
        var user = ctx?.User;
        if (user?.Identity?.IsAuthenticated != true) return null;
        var name = user.FindFirst("name")?.Value;
        var userName = user.FindFirst("preferred_username")?.Value;
        return name is { Length: > 0 } ? (userName is { Length: > 0 } ? $"{name} ({userName})" : name) : userName;
    }
}
