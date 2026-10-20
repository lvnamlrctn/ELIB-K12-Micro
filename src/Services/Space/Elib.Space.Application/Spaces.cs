using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed class SpaceSearch : CrudSearch
{
    public long? FloorId { get; set; }
    public long? BuildingId { get; set; }
    public int? Kind { get; set; }
}

public sealed record SpaceRequest(
    long FloorId, string Code, string Name, int Kind, int Category, int Capacity, int MinAdvanceMinutes, int MaxAdvanceDays, int? MaxAdvanceHours,
    int MaxBookingMinutesPerReader, int CheckInGraceMinutes, bool AutoApprove, string? Rules, IReadOnlyList<long>? AllowedReaderTypeIds,
    int? MinGroupSize, string? ColorHex = null, string? IconName = null);

public sealed record SpaceDto(
    long Id, Guid PublicId, long FloorId, string? FloorName, long? BuildingId, string? BuildingName, string Code, string Name, int Kind, int Category,
    int Capacity, int MinAdvanceMinutes, int MaxAdvanceDays, int? MaxAdvanceHours, int MaxBookingMinutesPerReader, int CheckInGraceMinutes,
    bool AutoApprove, string? Rules, IReadOnlyList<long> AllowedReaderTypeIds, int? MinGroupSize, string? ColorHex, string? IconName,
    double? PositionX, double? PositionY, double? Width, double? Height, bool Maintenance, string? MaintenanceNote, int Status,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record PlaceSpaceRequest(double? PositionX, double? PositionY, double? Width, double? Height);

public sealed record SetMaintenanceRequest(bool On, string? Note);

/// <summary>
/// Không gian/phòng đặt được (monolith: map.MapObject + map.RoomBookingConfig gộp một, quyền SPACE_ROOMS). Vị trí trên sơ đồ sửa
/// qua <see cref="PlaceAsync"/> riêng (trang sơ đồ trực quan), không qua form cấu hình thường.
/// </summary>
public sealed class SpaceResource(ICrudDbContext db) : CrudResource<SpaceResource, BookableSpace, SpaceSearch, SpaceRequest, SpaceDto>(db)
{
    protected override string EntityName => "Không gian";

    protected override string? Describe(BookableSpace entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<BookableSpace, SpaceDto>> Projection => x => new SpaceDto(
        x.Id, x.PublicId, x.FloorId, Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.Name).FirstOrDefault(),
        Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault(),
        Db.Set<MapBuilding>().Where(b => b.Id == Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault())
            .Select(b => b.Name).FirstOrDefault(),
        x.Code, x.Name, x.Kind, x.Category, x.Capacity, x.MinAdvanceMinutes, x.MaxAdvanceDays, x.MaxAdvanceHours, x.MaxBookingMinutesPerReader,
        x.CheckInGraceMinutes, x.AutoApprove, x.Rules, x.AllowedReaderTypeIds, x.MinGroupSize, x.ColorHex, x.IconName,
        x.PositionX, x.PositionY, x.Width, x.Height, x.Maintenance, x.MaintenanceNote, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override BookableSpace Create(SpaceRequest request) => BookableSpace.Create(
        request.FloorId, request.Code, request.Name, request.Kind, request.Category, request.Capacity, request.MinAdvanceMinutes,
        request.MaxAdvanceDays, request.MaxAdvanceHours, request.MaxBookingMinutesPerReader, request.CheckInGraceMinutes, request.AutoApprove,
        request.Rules, request.AllowedReaderTypeIds, request.MinGroupSize, request.ColorHex, request.IconName);

    protected override void Update(BookableSpace entity, SpaceRequest request) => entity.Update(
        request.FloorId, request.Code, request.Name, request.Kind, request.Category, request.Capacity, request.MinAdvanceMinutes,
        request.MaxAdvanceDays, request.MaxAdvanceHours, request.MaxBookingMinutesPerReader, request.CheckInGraceMinutes, request.AutoApprove,
        request.Rules, request.AllowedReaderTypeIds, request.MinGroupSize, request.ColorHex, request.IconName);

    protected override IQueryable<BookableSpace> Filter(IQueryable<BookableSpace> query, SpaceSearch search)
    {
        if (search.FloorId is { } floorId) query = query.Where(x => x.FloorId == floorId);
        if (search.BuildingId is { } buildingId)
        {
            var floorIds = Db.Set<MapFloor>().Where(f => f.BuildingId == buildingId).Select(f => f.Id);
            query = query.Where(x => floorIds.Contains(x.FloorId));
        }
        if (search.Kind is { } kind) query = query.Where(x => x.Kind == kind);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<BookableSpace> Order(IQueryable<BookableSpace> query) => query.OrderBy(x => x.FloorId).ThenBy(x => x.Code);

    protected override async Task ValidateAsync(BookableSpace entity, CancellationToken ct)
    {
        if (!await Db.Set<MapFloor>().AnyAsync(f => f.Id == entity.FloorId, ct))
            throw new BusinessRuleException("FLOOR_NOT_FOUND", "Tầng đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("SPACE_CODE_EXISTS", $"Mã không gian '{entity.Code}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(BookableSpace entity, CancellationToken ct)
    {
        if (await Db.Set<RoomBooking>().AnyAsync(b => b.SpaceId == entity.Id, ct))
            throw new ConflictException("SPACE_IN_USE", $"Không gian {entity.Code} đã có lượt đặt — không xoá được, chỉ có thể ẩn (đổi trạng thái).");
    }

    /// <summary>Đặt vị trí trên sơ đồ tầng (trang sơ đồ trực quan) — không đi qua UpdateAsync/form cấu hình thường.</summary>
    public async Task<SpaceDto> PlaceAsync(Guid publicId, PlaceSpaceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await LoadAsync(publicId, ct);
        entity.SetPosition(request.PositionX, request.PositionY, request.Width, request.Height);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>Tạm ngưng (bảo trì) hoặc mở lại một không gian — không đi qua UpdateAsync/form cấu hình thường.</summary>
    public async Task<SpaceDto> SetMaintenanceAsync(Guid publicId, SetMaintenanceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await LoadAsync(publicId, ct);
        entity.SetMaintenance(request.On, request.Note);
        await AuditAsync(entity, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }
}
