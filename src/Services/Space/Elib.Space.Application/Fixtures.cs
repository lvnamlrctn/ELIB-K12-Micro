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
    string? ShelfCategoryRange, string? ShelfSubjectName, int? ShelfCapacity, string? ShelfDescription, Guid? StorePublicId,
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
