using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed class FixtureSearch : CrudSearch
{
    public long? FloorId { get; set; }
    public long? BuildingId { get; set; }
    public string? ObjectType { get; set; }
}

public sealed record FixtureRequest(
    long FloorId, string Code, string Name, string ObjectType, string? ColorHex, string? IconName,
    string? ShelfCategoryRange, string? ShelfSubjectName, int? ShelfCapacity, string? ShelfDescription, Guid? StorePublicId);

public sealed record FixtureDto(
    long Id, Guid PublicId, long FloorId, string? FloorName, long? BuildingId, string? BuildingName, string Code, string Name, string ObjectType,
    double? PositionX, double? PositionY, double? Width, double? Height, string? ColorHex, string? IconName,
    string? ShelfCategoryRange, string? ShelfSubjectName, int? ShelfCapacity, string? ShelfDescription, Guid? StorePublicId, string? StoreName,
    int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record PlaceFixtureRequest(double? PositionX, double? PositionY, double? Width, double? Height);

/// <summary>
/// Vật dụng đặt trên sơ đồ tầng, không đặt được (monolith: map.MapObject, các ObjectType ngoài ROOM/STUDY_SPACE — quyền
/// SPACE_FIXTURES). Vị trí trên sơ đồ sửa qua <see cref="PlaceAsync"/> riêng, không qua form cấu hình thường.
/// </summary>
public sealed class FixtureResource(ICrudDbContext db) : CrudResource<FixtureResource, MapFixture, FixtureSearch, FixtureRequest, FixtureDto>(db)
{
    protected override string EntityName => "Vật dụng";

    protected override string? Describe(MapFixture entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<MapFixture, FixtureDto>> Projection => x => new FixtureDto(
        x.Id, x.PublicId, x.FloorId, Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.Name).FirstOrDefault(),
        Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault(),
        Db.Set<MapBuilding>().Where(b => b.Id == Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault())
            .Select(b => b.Name).FirstOrDefault(),
        x.Code, x.Name, x.ObjectType, x.PositionX, x.PositionY, x.Width, x.Height, x.ColorHex, x.IconName,
        x.ShelfCategoryRange, x.ShelfSubjectName, x.ShelfCapacity, x.ShelfDescription, x.StorePublicId,
        x.StorePublicId == null ? null : Db.Set<StoreReplica>().Where(s => s.StorePublicId == x.StorePublicId).Select(s => s.Name).FirstOrDefault(),
        x.Status, x.CreatedAt, x.UpdatedAt);

    protected override MapFixture Create(FixtureRequest request) => MapFixture.Create(
        request.FloorId, request.Code, request.Name, request.ObjectType, request.ColorHex, request.IconName,
        request.ShelfCategoryRange, request.ShelfSubjectName, request.ShelfCapacity, request.ShelfDescription, request.StorePublicId);

    protected override void Update(MapFixture entity, FixtureRequest request) => entity.Update(
        request.FloorId, request.Code, request.Name, request.ObjectType, request.ColorHex, request.IconName,
        request.ShelfCategoryRange, request.ShelfSubjectName, request.ShelfCapacity, request.ShelfDescription, request.StorePublicId);

    protected override IQueryable<MapFixture> Filter(IQueryable<MapFixture> query, FixtureSearch search)
    {
        if (search.FloorId is { } floorId) query = query.Where(x => x.FloorId == floorId);
        if (search.BuildingId is { } buildingId)
        {
            var floorIds = Db.Set<MapFloor>().Where(f => f.BuildingId == buildingId).Select(f => f.Id);
            query = query.Where(x => floorIds.Contains(x.FloorId));
        }
        if (search.ObjectType is { } type) query = query.Where(x => x.ObjectType == type);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<MapFixture> Order(IQueryable<MapFixture> query) => query.OrderBy(x => x.FloorId).ThenBy(x => x.Code);

    protected override async Task ValidateAsync(MapFixture entity, CancellationToken ct)
    {
        if (!await Db.Set<MapFloor>().AnyAsync(f => f.Id == entity.FloorId, ct))
            throw new BusinessRuleException("FLOOR_NOT_FOUND", "Tầng đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("FIXTURE_CODE_EXISTS", $"Mã vật dụng '{entity.Code}' đã có.");
        if (entity.StorePublicId is { } storeId && !await Db.Set<StoreReplica>().AnyAsync(s => s.StorePublicId == storeId && !s.Deleted, ct))
            throw new BusinessRuleException("STORE_NOT_FOUND", "Kho đã chọn không còn.");
    }

    /// <summary>Đặt vị trí trên sơ đồ tầng (trang sơ đồ trực quan) — không đi qua UpdateAsync/form cấu hình thường.</summary>
    public async Task<FixtureDto> PlaceAsync(Guid publicId, PlaceFixtureRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await LoadAsync(publicId, ct);
        entity.SetPosition(request.PositionX, request.PositionY, request.Width, request.Height);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }
}

public sealed class FixtureEquipmentSearch : CrudSearch
{
    public long? FixtureId { get; set; }
}

public sealed record FixtureEquipmentRequest(long FixtureId, string Name, string? Description, int? Quantity, int ConditionStatus);

public sealed record FixtureEquipmentDto(long Id, Guid PublicId, long FixtureId, string Name, string? Description, int? Quantity, int ConditionStatus,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Thiết bị gắn theo một vật dụng trên sơ đồ (monolith: map.MapEquipment, quyền SPACE_FIXTURES).</summary>
public sealed class FixtureEquipmentResource(ICrudDbContext db)
    : CrudResource<FixtureEquipmentResource, MapFixtureEquipment, FixtureEquipmentSearch, FixtureEquipmentRequest, FixtureEquipmentDto>(db)
{
    protected override string EntityName => "Thiết bị";

    protected override string? Describe(MapFixtureEquipment entity) => entity.Name;

    protected override Expression<Func<MapFixtureEquipment, FixtureEquipmentDto>> Projection => x =>
        new FixtureEquipmentDto(x.Id, x.PublicId, x.FixtureId, x.Name, x.Description, x.Quantity, x.ConditionStatus, x.CreatedAt, x.UpdatedAt);

    protected override MapFixtureEquipment Create(FixtureEquipmentRequest request) =>
        MapFixtureEquipment.Create(request.FixtureId, request.Name, request.Description, request.Quantity, request.ConditionStatus);

    protected override void Update(MapFixtureEquipment entity, FixtureEquipmentRequest request) =>
        entity.Update(request.Name, request.Description, request.Quantity, request.ConditionStatus);

    protected override IQueryable<MapFixtureEquipment> Filter(IQueryable<MapFixtureEquipment> query, FixtureEquipmentSearch search) =>
        search.FixtureId is { } fixtureId ? query.Where(x => x.FixtureId == fixtureId) : query;

    protected override IOrderedQueryable<MapFixtureEquipment> Order(IQueryable<MapFixtureEquipment> query) => query.OrderBy(x => x.Name).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(MapFixtureEquipment entity, CancellationToken ct)
    {
        if (!await Db.Set<MapFixture>().AnyAsync(x => x.Id == entity.FixtureId, ct))
            throw new BusinessRuleException("FIXTURE_NOT_FOUND", "Vật dụng đã chọn không còn.");
    }
}

public sealed class FloorUtilitySearch : CrudSearch
{
    public long? FloorId { get; set; }
}

public sealed record FloorUtilityRequest(long FloorId, string Name, string? Description, int? Quantity, int ConditionStatus, string? IconName);

public sealed record FloorUtilityDto(long Id, Guid PublicId, long FloorId, string Name, string? Description, int? Quantity, int ConditionStatus,
    string? IconName, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Tiện ích theo tầng (monolith: map.MapFloorUtility, quyền SPACE_FLOORS).</summary>
public sealed class FloorUtilityResource(ICrudDbContext db)
    : CrudResource<FloorUtilityResource, MapFloorUtility, FloorUtilitySearch, FloorUtilityRequest, FloorUtilityDto>(db)
{
    protected override string EntityName => "Tiện ích tầng";

    protected override string? Describe(MapFloorUtility entity) => entity.Name;

    protected override Expression<Func<MapFloorUtility, FloorUtilityDto>> Projection => x =>
        new FloorUtilityDto(x.Id, x.PublicId, x.FloorId, x.Name, x.Description, x.Quantity, x.ConditionStatus, x.IconName, x.CreatedAt, x.UpdatedAt);

    protected override MapFloorUtility Create(FloorUtilityRequest request) =>
        MapFloorUtility.Create(request.FloorId, request.Name, request.Description, request.Quantity, request.ConditionStatus, request.IconName);

    protected override void Update(MapFloorUtility entity, FloorUtilityRequest request) =>
        entity.Update(request.Name, request.Description, request.Quantity, request.ConditionStatus, request.IconName);

    protected override IQueryable<MapFloorUtility> Filter(IQueryable<MapFloorUtility> query, FloorUtilitySearch search) =>
        search.FloorId is { } floorId ? query.Where(x => x.FloorId == floorId) : query;

    protected override IOrderedQueryable<MapFloorUtility> Order(IQueryable<MapFloorUtility> query) => query.OrderBy(x => x.Name).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(MapFloorUtility entity, CancellationToken ct)
    {
        if (!await Db.Set<MapFloor>().AnyAsync(f => f.Id == entity.FloorId, ct))
            throw new BusinessRuleException("FLOOR_NOT_FOUND", "Tầng đã chọn không còn.");
    }
}
