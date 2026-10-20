using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed record AccessDeviceRequest(string Code, string Name, long? SpaceId);

public sealed record AccessDeviceDto(long Id, Guid PublicId, string Code, string Name, long? SpaceId, string? SpaceName, DateTimeOffset? LastSeenAt,
    int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record NewApiKey(string RawKey);

public sealed record UnlockRequest(long? DeviceId, Guid? BookingPublicId, string? Note);

/// <summary>Đầu đọc RFID tại cửa (monolith: AccessDeviceController, quyền SPACE_ACCESS). Khoá API chỉ hiện 1 lần lúc tạo/cấp lại.</summary>
public sealed class AccessDeviceResource(ICrudDbContext db) : CrudResource<AccessDeviceResource, AccessDevice, CrudSearch, AccessDeviceRequest, AccessDeviceDto>(db)
{
    protected override string EntityName => "Thiết bị kiểm soát cửa";

    protected override string? Describe(AccessDevice entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<AccessDevice, AccessDeviceDto>> Projection => x => new AccessDeviceDto(
        x.Id, x.PublicId, x.Code, x.Name, x.SpaceId, Db.Set<BookableSpace>().Where(s => s.Id == x.SpaceId).Select(s => s.Name).FirstOrDefault(),
        x.LastSeenAt, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override AccessDevice Create(AccessDeviceRequest request) => throw new NotSupportedException("Thiết bị tạo qua AddWithKeyAsync (cần sinh khoá API).");

    protected override void Update(AccessDevice entity, AccessDeviceRequest request) => entity.Update(request.Code, request.Name, request.SpaceId);

    protected override IQueryable<AccessDevice> Filter(IQueryable<AccessDevice> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<AccessDevice> Order(IQueryable<AccessDevice> query) => query.OrderBy(x => x.Code);

    protected override async Task ValidateAsync(AccessDevice entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("DEVICE_CODE_EXISTS", $"Mã thiết bị '{entity.Code}' đã có.");
    }

    /// <summary>Tạo thiết bị mới — trả khoá API thô 1 lần duy nhất, chỉ lưu hash.</summary>
    public async Task<(AccessDeviceDto Device, NewApiKey Key)> AddWithKeyAsync(AccessDeviceRequest request, CancellationToken ct)
    {
        var (raw, hash) = GenerateKey();
        var device = AccessDevice.Create(request.Code, request.Name, request.SpaceId, hash);
        Set.Add(device);
        await ValidateAsync(device, ct);
        await AuditAsync(device, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct);
        return ((await ToDtosAsync([device], ct))[0], new NewApiKey(raw));
    }

    /// <summary>Cấp lại khoá API — khoá cũ mất hiệu lực ngay, trả khoá mới 1 lần duy nhất.</summary>
    public async Task<NewApiKey> RegenerateKeyAsync(Guid publicId, CancellationToken ct)
    {
        var device = await LoadAsync(publicId, ct);
        var (raw, hash) = GenerateKey();
        device.SetApiKeyHash(hash);
        await AuditAsync(device, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return new NewApiKey(raw);
    }

    public async Task UnlockAsync(UnlockRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        long deviceId;
        if (request.DeviceId is { } id) deviceId = id;
        else if (request.BookingPublicId is { } bookingId)
        {
            var spaceId = await Db.Set<RoomBooking>().Where(b => b.PublicId == bookingId).Select(b => b.SpaceId).FirstOrDefaultAsync(ct);
            if (spaceId == 0) throw new NotFoundException("Lượt đặt phòng", bookingId);
            deviceId = await Set.Where(d => d.SpaceId == spaceId).Select(d => d.Id).FirstOrDefaultAsync(ct);
            if (deviceId == 0) throw new BusinessRuleException("NO_DEVICE_FOR_SPACE", "Không gian này chưa gắn thiết bị kiểm soát cửa.", 404);
        }
        else throw new BusinessRuleException("DEVICE_OR_BOOKING_REQUIRED", "Chọn thiết bị hoặc lượt đặt phòng để mở cửa.");

        Db.Set<AccessCommand>().Add(AccessCommand.Create(deviceId, DateTimeOffset.UtcNow));
        await Db.SaveChangesAsync(ct);
    }

    public static (string Raw, string Hash) GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (raw, HashKey(raw));
    }

    public static string HashKey(string raw) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private async Task<IReadOnlyList<AccessDeviceDto>> ToDtosAsync(IReadOnlyList<AccessDevice> devices, CancellationToken ct)
    {
        var ids = devices.Select(d => d.Id).ToList();
        return await Set.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(Projection).ToListAsync(ct);
    }
}

public sealed record AccessStaffCardRequest(string CardUid, string? Label);

public sealed record AccessStaffCardDto(long Id, Guid PublicId, string CardUid, string? Label, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Thẻ cán bộ mở cửa (quyền SPACE_ACCESS).</summary>
public sealed class AccessStaffCardResource(ICrudDbContext db) : CrudResource<AccessStaffCardResource, AccessStaffCard, CrudSearch, AccessStaffCardRequest, AccessStaffCardDto>(db)
{
    protected override string EntityName => "Thẻ cán bộ";

    protected override string? Describe(AccessStaffCard entity) => entity.Label ?? entity.CardUid;

    protected override Expression<Func<AccessStaffCard, AccessStaffCardDto>> Projection =>
        x => new AccessStaffCardDto(x.Id, x.PublicId, x.CardUid, x.Label, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override AccessStaffCard Create(AccessStaffCardRequest request) => AccessStaffCard.Create(request.CardUid, request.Label);

    protected override void Update(AccessStaffCard entity, AccessStaffCardRequest request) => entity.Update(request.CardUid, request.Label);

    protected override IOrderedQueryable<AccessStaffCard> Order(IQueryable<AccessStaffCard> query) => query.OrderBy(x => x.CardUid);

    protected override async Task ValidateAsync(AccessStaffCard entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.CardUid == entity.CardUid && x.Id != entity.Id, ct))
            throw new ConflictException("CARD_UID_EXISTS", $"Thẻ '{entity.CardUid}' đã có.");
    }
}

public sealed class AccessScanLogSearch : CrudSearch
{
    public long? DeviceId { get; set; }
    public bool? Allowed { get; set; }
}

public sealed record AccessScanLogDto(long Id, Guid PublicId, long DeviceId, string? DeviceCode, string CardUid, Guid? ReaderPublicId,
    string? ReaderName, Guid? BookingPublicId, bool Allowed, string Reason, DateTimeOffset ScannedAt);

public sealed record AccessScanLogOpUnused;

/// <summary>Nhật ký quyết định mở/từ chối cửa — chỉ xem (quyền SPACE_ACCESS view), ghi qua AccessControlService.</summary>
public sealed class AccessScanLogResource(ICrudDbContext db)
    : CrudResource<AccessScanLogResource, AccessScanLog, AccessScanLogSearch, AccessScanLogOpUnused, AccessScanLogDto>(db)
{
    protected override string EntityName => "Nhật ký kiểm soát cửa";

    protected override Expression<Func<AccessScanLog, AccessScanLogDto>> Projection => x => new AccessScanLogDto(
        x.Id, x.PublicId, x.DeviceId, Db.Set<AccessDevice>().Where(d => d.Id == x.DeviceId).Select(d => d.Code).FirstOrDefault(), x.CardUid,
        x.ReaderPublicId, x.ReaderPublicId == null ? null : Db.Set<PatronReplica>().Where(r => r.ReaderPublicId == x.ReaderPublicId).Select(r => r.FullName).FirstOrDefault(),
        x.BookingPublicId, x.Allowed, x.Reason, x.ScannedAt);

    protected override AccessScanLog Create(AccessScanLogOpUnused request) => throw new NotSupportedException("Nhật ký chỉ ghi qua AccessControlService.");

    protected override void Update(AccessScanLog entity, AccessScanLogOpUnused request) => throw new NotSupportedException("Nhật ký không sửa.");

    protected override IQueryable<AccessScanLog> Filter(IQueryable<AccessScanLog> query, AccessScanLogSearch search)
    {
        if (search.DeviceId is { } deviceId) query = query.Where(x => x.DeviceId == deviceId);
        if (search.Allowed is { } allowed) query = query.Where(x => x.Allowed == allowed);
        return query;
    }

    protected override IOrderedQueryable<AccessScanLog> Order(IQueryable<AccessScanLog> query) => query.OrderByDescending(x => x.ScannedAt);
}

public sealed record AccessDecision(bool Allowed, string Reason, DateTimeOffset? ValidUntil);

public sealed record PendingCommand(Guid Id, string Command, DateTimeOffset ExpiresAt);

/// <summary>
/// Quyết định mở cửa cho thiết bị RFID (monolith: AccessControlService) — xác thực bằng khoá API riêng (header, không JWT), tự
/// tìm đơn vị qua tenant đã được gateway/middleware suy từ Host như mọi endpoint OPAC khác. Thẻ cán bộ luôn mở; thẻ bạn đọc coi
/// như CardNo (nền tảng chưa có CardUid riêng) — có lượt CheckedIn ở đúng không gian thì mở; có lượt Approved trong
/// [StartAt, StartAt+grace] thì mở VÀ tự check-in luôn (tránh oan không tới); ngoài ra từ chối.
/// </summary>
public sealed class AccessControlService(ICrudDbContext db, Replicas replicas, BookingResource bookings, TimeProvider clock)
{
    public async Task<AccessDevice?> AuthenticateAsync(string? code, string? apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(apiKey)) return null;
        var device = await db.Set<AccessDevice>().FirstOrDefaultAsync(d => d.Code == code && d.Status == IHasStatus.Active, ct);
        if (device is null) return null;
        var hash = AccessDeviceResource.HashKey(apiKey);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(device.ApiKeyHash)) ? device : null;
    }

    public async Task<AccessDecision> VerifyAsync(AccessDevice device, string cardUid, DateTimeOffset scannedAt, CancellationToken ct)
    {
        device.Touch(scannedAt);
        var decision = await DecideAsync(device, cardUid, scannedAt, ct);
        db.Set<AccessScanLog>().Add(AccessScanLog.Create(device.Id, cardUid, decision.ReaderPublicId, decision.BookingPublicId, decision.Decision.Allowed,
            decision.Decision.Reason, scannedAt));
        await db.SaveChangesAsync(ct);
        return decision.Decision;
    }

    private async Task<(AccessDecision Decision, Guid? ReaderPublicId, Guid? BookingPublicId)> DecideAsync(AccessDevice device, string cardUid, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Set<AccessStaffCard>().AnyAsync(c => c.CardUid == cardUid && c.Status == IHasStatus.Active, ct))
            return (new AccessDecision(true, "Thẻ cán bộ", null), null, null);

        var reader = await replicas.ReaderAsync(cardUid, ct);
        if (reader is null) return (new AccessDecision(false, "Không nhận diện được thẻ", null), null, null);

        if (device.SpaceId is { } spaceId)
        {
            var checkedIn = await db.Set<RoomBooking>().FirstOrDefaultAsync(b => b.SpaceId == spaceId && b.ReaderPublicId == reader.ReaderPublicId
                && b.Status == RoomBookingStatus.CheckedIn, ct);
            if (checkedIn is not null) return (new AccessDecision(true, "Đang sử dụng không gian", checkedIn.EndAt), reader.ReaderPublicId, checkedIn.PublicId);

            var space = await db.Set<BookableSpace>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == spaceId, ct);
            var grace = space?.CheckInGraceMinutes ?? BookableSpace.DefaultCheckInGraceMinutes;
            var approved = await db.Set<RoomBooking>().FirstOrDefaultAsync(b => b.SpaceId == spaceId && b.ReaderPublicId == reader.ReaderPublicId
                && b.Status == RoomBookingStatus.Approved, ct);
            if (approved is not null && now >= approved.StartAt && now <= approved.StartAt.AddMinutes(grace))
            {
                await bookings.CheckInAsync(new CheckInRequest(approved.PublicId, spaceId), ct);
                return (new AccessDecision(true, "Mở cửa và tự check-in", approved.EndAt), reader.ReaderPublicId, approved.PublicId);
            }
        }
        return (new AccessDecision(false, "Không có lượt đặt phòng hợp lệ tại thời điểm này", null), reader.ReaderPublicId, null);
    }

    public async Task<IReadOnlyList<PendingCommand>> CommandsAsync(AccessDevice device, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.Set<AccessCommand>().Where(c => c.DeviceId == device.Id && c.AckedAt == null && c.ExpiresAt > now)
            .Select(c => new PendingCommand(c.PublicId, c.Command, c.ExpiresAt)).ToListAsync(ct);
    }

    public async Task AckAsync(AccessDevice device, Guid commandId, CancellationToken ct)
    {
        var command = await db.Set<AccessCommand>().FirstOrDefaultAsync(c => c.PublicId == commandId && c.DeviceId == device.Id, ct);
        if (command is null) return;
        command.Ack(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }
}
