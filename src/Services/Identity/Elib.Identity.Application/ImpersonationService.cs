using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Platform;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Elib.Identity.Application;

public sealed class ImpersonationOptions
{
    public const string SectionName = "Identity:Impersonation";

    /// <summary>
    /// Origin host đơn vị, '*' thay bằng subdomain — ví dụ https://*.thuvientn.vn. Bỏ trống = suy từ mẫu redirect URI
    /// của client elib-admin (https://*.thuvientn.vn/admin/callback).
    /// </summary>
    public string? TenantOriginPattern { get; set; }

    /// <summary>Vé dùng một lần, phải mở trên host đơn vị trong thời gian này.</summary>
    public TimeSpan TicketLifetime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Phiên đóng vai tối đa — hết hạn thì không làm mới token được nữa, phải tạo vé mới.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromMinutes(30);
}

public sealed record StartImpersonationRequest(long TenantId, string Reason);

public sealed record ImpersonationTicketDto(string Url, DateTimeOffset ExpiresAt);

/// <summary>Phiên đóng vai đã đổi từ vé: người thật (quản trị nền tảng) + đơn vị + hạn.</summary>
public sealed record ImpersonationSession(SignInIdentity Identity, DateTimeOffset ExpiresAt);

/// <summary>
/// "Đóng vai đơn vị" (docs 04 §2.2): quản trị nền tảng xem dữ liệu một đơn vị bằng chính app Admin của đơn vị, CHỈ ĐỌC,
/// có thời hạn, có lý do và ghi nhật ký (AuditRecorded). Không có chế độ "thấy gộp mọi đơn vị".
/// Luồng: host hệ thống tạo vé (60 giây, dùng một lần) → trình duyệt mở URL vé trên host đơn vị → identity đổi vé lấy phiên
/// đăng nhập của host đó → app Admin xin token như thường; token mang claim imp=readonly.
/// </summary>
public sealed class ImpersonationService(
    IIdentityDb db,
    ITenantContext tenant,
    ICurrentActor actor,
    IDistributedCache cache,
    IPublishEndpoint publisher,
    IdentityAudit audit,
    TimeProvider clock,
    IOptions<ImpersonationOptions> options)
{
    private sealed record Ticket(long SystemUserId, long TenantId, string Reason);

    public async Task<ImpersonationTicketDto> StartAsync(StartImpersonationRequest request, CancellationToken ct)
    {
        var userId = actor.Id ?? throw new BusinessRuleException("FORBIDDEN", "Không xác định được người dùng.", 403);
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length is < 5 or > 500)
            throw new BusinessRuleException("IMPERSONATION_REASON_REQUIRED", "Nhập lý do xem đơn vị (5–500 ký tự) — lý do được ghi vào nhật ký.");

        using (tenant.UseSystem())
        {
            if (!await db.SystemUsers.AnyAsync(u => u.Id == userId && u.IsActive, ct))
                throw new BusinessRuleException("FORBIDDEN", "Tài khoản quản trị nền tảng không còn hiệu lực.", 403);
        }
        var replica = await db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == request.TenantId, ct)
                      ?? throw new NotFoundException("đơn vị", request.TenantId);
        if (replica.Status != "Active")
            throw new ConflictException("TENANT_UNAVAILABLE", $"Đơn vị {replica.Code} đang ở trạng thái {replica.Status}.");
        var pattern = options.Value.TenantOriginPattern;
        if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains('*', StringComparison.Ordinal))
            throw new InvalidOperationException("Chưa cấu hình Identity:Impersonation:TenantOriginPattern.");

        var ticket = Base64Url(RandomNumberGenerator.GetBytes(32));
        var expiresAt = clock.GetUtcNow() + options.Value.TicketLifetime;
        await cache.SetStringAsync(Key(ticket), JsonSerializer.Serialize(new Ticket(userId, replica.TenantId, reason)),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt }, ct);

        var origin = pattern.Replace("*", replica.Subdomain, StringComparison.Ordinal).TrimEnd('/');
        return new ImpersonationTicketDto($"{origin}/account/impersonate?ticket={ticket}", expiresAt);
    }

    /// <summary>Đổi vé lấy phiên — vé phải còn hạn, chưa dùng và được mở trên đúng host của đơn vị. Null = từ chối.</summary>
    public async Task<ImpersonationSession?> RedeemAsync(string? ticket, long? hostTenantId, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ticket) || ticket.Length > 100 || hostTenantId is null) return null;
        var key = Key(ticket);
        var json = await cache.GetStringAsync(key, ct);
        if (json is null) return null;
        await cache.RemoveAsync(key, ct); // dùng một lần — kể cả khi mở nhầm host
        var data = JsonSerializer.Deserialize<Ticket>(json)!;
        if (data.TenantId != hostTenantId) return null;

        var identity = await ReloadAsync(data.TenantId, data.SystemUserId, ct);
        if (identity is null) return null;

        await publisher.Publish(new AuditRecorded
        {
            TenantId = data.TenantId,
            Actor = new EventActor(data.SystemUserId, ElibSubjectTypes.Staff),
            Service = "identity",
            Action = "IMPERSONATION_STARTED",
            EntityType = "Tenant",
            EntityId = data.TenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Summary = $"Quản trị nền tảng {identity.UserName} xem đơn vị (chỉ đọc). Lý do: {data.Reason}",
            IpAddress = ipAddress,
            ActorName = identity.FullName,
        }, ct);
        await audit.SystemAsync("IMPERSONATION_STARTED", "Tenant", data.TenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"Vào xem đơn vị {identity.TenantCode} (chỉ đọc). Lý do: {data.Reason}", data.TenantId, ct,
            new IdentityAudit.Who(data.SystemUserId, identity.UserName));
        await db.SaveChangesAsync(ct); // đẩy outbox
        return new ImpersonationSession(identity, clock.GetUtcNow() + options.Value.SessionLifetime);
    }

    /// <summary>Tải lại khi phát/làm mới token: tài khoản hệ thống còn hoạt động, đơn vị vẫn Active.</summary>
    public async Task<SignInIdentity?> ReloadAsync(long tenantId, long systemUserId, CancellationToken ct)
    {
        var replica = await db.TenantReplicas.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (replica is null || replica.Status != "Active") return null;
        using var system = tenant.UseSystem();
        var user = await db.SystemUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == systemUserId, ct);
        if (user is not { IsActive: true } || user.Credentials.IsLockedOut(clock.GetUtcNow())) return null;
        return new SignInIdentity(user.Id, tenantId, user.UserName, user.FullName + " (quản trị nền tảng)", "imp",
            replica.Code, replica.Subdomain, MustChangePassword: false, ReadOnlyImpersonation: true);
    }

    private static string Key(string ticket) =>
        "imp-ticket:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
