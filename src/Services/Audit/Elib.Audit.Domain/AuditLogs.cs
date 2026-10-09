using Elib.BuildingBlocks.Domain;

namespace Elib.Audit.Domain;

/// <summary>
/// Một dòng nhật ký thao tác của đơn vị (thay UserLog của monolith): ai, làm gì, trên đối tượng nào, lúc nào, từ IP nào.
/// Chỉ consumer ghi (từ event AuditRecorded của các service); không sửa, không xoá mềm — chỉ xoá theo hạn lưu.
/// </summary>
public sealed class AuditLog : Entity, ITenantOwned, IHasPublicId
{
    private AuditLog() { }

    public long TenantId { get; set; }
    public Guid PublicId { get; set; }

    /// <summary>EventId của AuditRecorded — chống ghi trùng khi event bị gửi lại.</summary>
    public Guid EventId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
    public long? ActorId { get; private set; }
    public string ActorKind { get; private set; } = "";
    public string? ActorName { get; private set; }
    public string Service { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string EntityType { get; private set; } = "";
    public string? EntityId { get; private set; }
    public string? Summary { get; private set; }
    public string? ChangesJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? CorrelationId { get; private set; }

    public static AuditLog Create(Guid eventId, DateTimeOffset occurredAt, long? actorId, string actorKind, string? actorName, string service,
        string action, string entityType, string? entityId, string? summary, string? changesJson, string? ipAddress, string? correlationId) => new()
    {
        EventId = eventId,
        OccurredAt = occurredAt,
        ActorId = actorId,
        ActorKind = AuditText.Cut(actorKind, 20) ?? "",
        ActorName = AuditText.Cut(actorName, 300),
        Service = AuditText.Cut(service, 50) ?? "",
        Action = AuditText.Cut(action, 60) ?? "",
        EntityType = AuditText.Cut(entityType, 100) ?? "",
        EntityId = AuditText.Cut(entityId, 100),
        Summary = AuditText.Cut(summary, 2000),
        ChangesJson = AuditText.Cut(changesJson, 8000),
        IpAddress = AuditText.Cut(ipAddress, 64),
        CorrelationId = AuditText.Cut(correlationId, 100),
    };
}

/// <summary>
/// Nhật ký cấp nền tảng: quản trị nền tảng đăng nhập, tạo/khoá đơn vị, bán phân hệ, vào xem đơn vị…
/// Không thuộc đơn vị (không RLS) — chỉ quản trị nền tảng đọc được. <see cref="TargetTenantId"/> = đơn vị bị tác động.
/// </summary>
public sealed class PlatformAuditLog : Entity, IHasPublicId
{
    private PlatformAuditLog() { }

    public Guid PublicId { get; set; }
    public Guid EventId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public long? ActorId { get; private set; }
    public string ActorKind { get; private set; } = "";
    public string? ActorName { get; private set; }
    public string Service { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string EntityType { get; private set; } = "";
    public string? EntityId { get; private set; }
    public long? TargetTenantId { get; private set; }
    public string? Summary { get; private set; }
    public string? IpAddress { get; private set; }

    public static PlatformAuditLog Create(Guid eventId, DateTimeOffset occurredAt, long? actorId, string actorKind, string? actorName, string service,
        string action, string entityType, string? entityId, long? targetTenantId, string? summary, string? ipAddress) => new()
    {
        EventId = eventId,
        OccurredAt = occurredAt,
        ActorId = actorId,
        ActorKind = AuditText.Cut(actorKind, 20) ?? "",
        ActorName = AuditText.Cut(actorName, 300),
        Service = AuditText.Cut(service, 50) ?? "",
        Action = AuditText.Cut(action, 60) ?? "",
        EntityType = AuditText.Cut(entityType, 100) ?? "",
        EntityId = AuditText.Cut(entityId, 100),
        TargetTenantId = targetTenantId,
        Summary = AuditText.Cut(summary, 2000),
        IpAddress = AuditText.Cut(ipAddress, 64),
    };
}

public static class AuditText
{
    public static string? Cut(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
