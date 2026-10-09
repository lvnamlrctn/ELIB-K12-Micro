using Elib.Audit.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Elib.Audit.Application;

public interface IAuditDb
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<PlatformAuditLog> PlatformAuditLogs { get; }

    /// <summary>Tên đơn vị theo bản sao (cột "Đơn vị" của nhật ký nền tảng).</summary>
    Task<Dictionary<long, string>> TenantNamesAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Nhật ký cũ hơn số ngày này bị xoá (chạy mỗi ngày). 0 = giữ mãi.</summary>
    public int RetentionDays { get; set; } = 365;
}

/// <summary>Tìm nhật ký (monolith: UserLog/Search). Keyword tìm trong nội dung, người thực hiện, IP.</summary>
public class AuditLogSearch : CrudSearch
{
    public string? Action { get; set; }
    public string? Service { get; set; }
    public long? ActorId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed class PlatformAuditLogSearch : AuditLogSearch
{
    public long? TenantId { get; set; }
}

public sealed record AuditLogDto(
    Guid PublicId, DateTimeOffset OccurredAt, long? ActorId, string ActorKind, string? ActorName, string Service, string Action,
    string EntityType, string? EntityId, string? Summary, string? IpAddress);

public sealed record PlatformAuditLogDto(
    Guid PublicId, DateTimeOffset OccurredAt, long? ActorId, string ActorKind, string? ActorName, string Service, string Action,
    string EntityType, string? EntityId, long? TenantId, string? TenantName, string? Summary, string? IpAddress);

public sealed class AuditQueries(IAuditDb db, ITenantContext tenant)
{
    private const int MaxPageSize = 200;

    /// <summary>Nhật ký của đơn vị hiện tại (lọc đơn vị do query filter + RLS).</summary>
    public async Task<CrudPage<AuditLogDto>> SearchAsync(AuditLogSearch search, CancellationToken ct)
    {
        tenant.RequireTenantId();
        var q = Filter(db.AuditLogs.AsNoTracking(), search);
        var (page, size) = Paging(search);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.Id).Skip((page - 1) * size).Take(size)
            .Select(l => new AuditLogDto(l.PublicId, l.OccurredAt, l.ActorId, l.ActorKind, l.ActorName, l.Service, l.Action,
                l.EntityType, l.EntityId, l.Summary, l.IpAddress))
            .ToListAsync(ct);
        return new CrudPage<AuditLogDto>(items, total, page, size);
    }

    /// <summary>Các loại thao tác đã có trong nhật ký của đơn vị — cho ô lọc "Thao tác".</summary>
    public async Task<IReadOnlyList<string>> ActionsAsync(CancellationToken ct)
    {
        tenant.RequireTenantId();
        return await db.AuditLogs.AsNoTracking().Select(l => l.Action).Distinct().OrderBy(a => a).ToListAsync(ct);
    }

    public async Task<CrudPage<PlatformAuditLogDto>> SearchPlatformAsync(PlatformAuditLogSearch search, CancellationToken ct)
    {
        IQueryable<PlatformAuditLog> q = db.PlatformAuditLogs.AsNoTracking();
        if (search.TenantId is { } tenantId) q = q.Where(l => l.TargetTenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search.Action)) q = q.Where(l => l.Action == search.Action.Trim());
        if (!string.IsNullOrWhiteSpace(search.Service)) q = q.Where(l => l.Service == search.Service.Trim());
        if (search.ActorId is { } actorId) q = q.Where(l => l.ActorId == actorId);
        if (search.From is { } from) q = q.Where(l => l.OccurredAt >= from);
        if (search.To is { } to) q = q.Where(l => l.OccurredAt <= to);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
            q = q.Where(l => (l.Summary != null && l.Summary.ToLower().Contains(term))
                             || (l.ActorName != null && l.ActorName.ToLower().Contains(term))
                             || (l.IpAddress != null && l.IpAddress.Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var (page, size) = Paging(search);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var names = await db.TenantNamesAsync(rows.Select(r => r.TargetTenantId).OfType<long>().Distinct().ToList(), ct);
        var items = rows.Select(l => new PlatformAuditLogDto(l.PublicId, l.OccurredAt, l.ActorId, l.ActorKind, l.ActorName, l.Service, l.Action,
            l.EntityType, l.EntityId, l.TargetTenantId, l.TargetTenantId is { } t ? names.GetValueOrDefault(t) : null, l.Summary, l.IpAddress)).ToList();
        return new CrudPage<PlatformAuditLogDto>(items, total, page, size);
    }

    public async Task<IReadOnlyList<string>> PlatformActionsAsync(CancellationToken ct) =>
        await db.PlatformAuditLogs.AsNoTracking().Select(l => l.Action).Distinct().OrderBy(a => a).ToListAsync(ct);

    private static IQueryable<AuditLog> Filter(IQueryable<AuditLog> q, AuditLogSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.Action)) q = q.Where(l => l.Action == search.Action.Trim());
        if (!string.IsNullOrWhiteSpace(search.Service)) q = q.Where(l => l.Service == search.Service.Trim());
        if (search.ActorId is { } actorId) q = q.Where(l => l.ActorId == actorId);
        if (search.From is { } from) q = q.Where(l => l.OccurredAt >= from);
        if (search.To is { } to) q = q.Where(l => l.OccurredAt <= to);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            q = q.Where(l => (l.Summary != null && l.Summary.ToLower().Contains(term))
                             || (l.ActorName != null && l.ActorName.ToLower().Contains(term))
                             || (l.IpAddress != null && l.IpAddress.Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return q;
    }

    private static (int Page, int Size) Paging(CrudSearch search) => (Math.Max(1, search.PageIndex), Math.Clamp(search.PageSize, 1, MaxPageSize));
}
