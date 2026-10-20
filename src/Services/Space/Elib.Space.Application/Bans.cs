using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events.Platform;
using Elib.Space.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

/// <summary>Mã mẫu thông báo đặt phòng (monolith: EMAIL_ROOM_BOOKING_&lt;EVENT&gt;) — đăng ký vào Notification's DefaultTemplates.</summary>
public static class SpaceTemplates
{
    public const string Created = "SPACE_BOOKING_CREATED";
    public const string Approved = "SPACE_BOOKING_APPROVED";
    public const string Rejected = "SPACE_BOOKING_REJECTED";
    public const string Expired = "SPACE_BOOKING_EXPIRED";
    public const string Cancelled = "SPACE_BOOKING_CANCELLED";
    public const string NoShow = "SPACE_BOOKING_NOSHOW";
    public const string Banned = "SPACE_BOOKING_BANNED";
}

/// <summary>Báo bạn đọc qua NotificationRequested (outbox) — đúng khuôn EbookReaderNotifier/ReaderNotifier.</summary>
public sealed class SpaceReaderNotifier(IPublishEndpoint publisher, ITenantContext tenant)
{
    public Task NotifyAsync(PatronReplica? reader, string templateCode, IReadOnlyDictionary<string, string> data, string deduplicationKey, CancellationToken ct)
    {
        if (reader is null || (string.IsNullOrWhiteSpace(reader.Email) && string.IsNullOrWhiteSpace(reader.Phone))) return Task.CompletedTask;
        var values = new Dictionary<string, string>(data, StringComparer.Ordinal) { ["card_no"] = reader.CardNo };
        return publisher.Publish(new NotificationRequested
        {
            TenantId = tenant.RequireTenantId(), TemplateCode = templateCode,
            Recipient = new NotificationRecipient(null, null, reader.Email, reader.Phone, reader.FullName),
            Data = values, DeduplicationKey = deduplicationKey,
        }, ct);
    }
}

public sealed class BanSearch : CrudSearch
{
    public bool? ActiveOnly { get; set; }
}

public sealed record BanDto(long Id, Guid PublicId, Guid ReaderPublicId, string CardNo, string? ReaderName, string Reason, int Source,
    DateTimeOffset BannedUntil, DateTimeOffset? LiftedAt, long? LiftedBy, bool IsActive, DateTimeOffset CreatedAt);

public sealed record AddBanRequest(string CardNo, int Days, string Reason);

/// <summary>Cấm đặt phòng (monolith: RoomBookingBanService, quyền SPACE_BANS). Không có Add/Update chung — tạo qua AddAsync.</summary>
public sealed class BanResource(ICrudDbContext db, Replicas replicas, ICurrentActor actor, TimeProvider clock)
    : CrudResource<BanResource, RoomBookingBan, BanSearch, AddBanRequest, BanDto>(db)
{
    protected override string EntityName => "Lệnh cấm đặt phòng";

    protected override string? Describe(RoomBookingBan entity) => $"Thẻ {entity.CardNo}";

    protected override System.Linq.Expressions.Expression<Func<RoomBookingBan, BanDto>> Projection => x => new BanDto(
        x.Id, x.PublicId, x.ReaderPublicId, x.CardNo,
        Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.Reason, x.Source, x.BannedUntil, x.LiftedAt, x.LiftedBy, x.LiftedAt == null, x.CreatedAt);

    protected override RoomBookingBan Create(AddBanRequest request) => throw new NotSupportedException("Lệnh cấm tạo qua AddAsync.");

    protected override void Update(RoomBookingBan entity, AddBanRequest request) => throw new NotSupportedException("Lệnh cấm không sửa, chỉ gỡ qua LiftAsync.");

    protected override IQueryable<RoomBookingBan> Filter(IQueryable<RoomBookingBan> query, BanSearch search)
    {
        if (search.ActiveOnly == true)
        {
            var now = clock.GetUtcNow();
            query = query.Where(x => x.LiftedAt == null && x.BannedUntil > now);
        }
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.CardNo.ToLower().Contains(term) ||
                Db.Set<PatronReplica>().Any(r => r.ReaderPublicId == x.ReaderPublicId && r.FullName.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<RoomBookingBan> Order(IQueryable<RoomBookingBan> query) => query.OrderByDescending(x => x.CreatedAt);

    public override async Task<BanDto> AddAsync(AddBanRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reader = await replicas.ReaderAsync(request.CardNo, ct)
            ?? throw new BusinessRuleException("READER_NOT_FOUND", $"Không tìm thấy bạn đọc có số thẻ '{request.CardNo?.Trim()}'.", 404);
        var now = clock.GetUtcNow();
        if (await Set.AnyAsync(b => b.ReaderPublicId == reader.ReaderPublicId && b.LiftedAt == null && b.BannedUntil > now, ct))
            throw new ConflictException("BAN_ALREADY_ACTIVE", $"Thẻ {reader.CardNo} đang bị cấm đặt phòng.");
        var days = request.Days is >= 1 and <= 365 ? request.Days : throw new BusinessRuleException("BAN_DAYS_INVALID", "Số ngày cấm từ 1 đến 365.");
        var ban = RoomBookingBan.Create(reader.ReaderPublicId, reader.CardNo, request.Reason, RoomBookingBanSource.Staff, now.AddDays(days));
        Set.Add(ban);
        await AuditAsync(ban, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([ban], ct))[0];
    }

    public async Task<BanDto> LiftAsync(Guid publicId, CancellationToken ct)
    {
        var ban = await LoadAsync(publicId, ct);
        ban.Lift(actor.Id, clock.GetUtcNow());
        await AuditAsync(ban, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return (await ToDtosAsync([ban], ct))[0];
    }

    /// <summary>Có lệnh cấm đang hiệu lực không — BookingResource gọi khi đặt phòng (chủ phòng và từng thành viên).</summary>
    public Task<bool> IsBannedAsync(Guid readerPublicId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return Set.AnyAsync(b => b.ReaderPublicId == readerPublicId && b.LiftedAt == null && b.BannedUntil > now, ct);
    }

    private async Task<IReadOnlyList<BanDto>> ToDtosAsync(IReadOnlyList<RoomBookingBan> bans, CancellationToken ct)
    {
        var ids = bans.Select(b => b.Id).ToList();
        return await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(Projection).ToListAsync(ct);
    }
}
