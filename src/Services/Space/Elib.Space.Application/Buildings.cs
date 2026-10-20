using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Space.Application;

/// <summary>Cấu hình riêng của space — tiền tố công khai của media (ảnh nền sơ đồ), đúng khuôn Digital's DigitalOptions.</summary>
public sealed class SpaceOptions
{
    public const string SectionName = "Space";

    public string MediaPublicPrefix { get; set; } = "/s3/media-public";
}

public sealed record BuildingRequest(string Code, string Name, string? Description);

public sealed record BuildingDto(long Id, Guid PublicId, string Code, string Name, string? Description, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Toà nhà (monolith: map.MapBuilding, quyền SPACE_BUILDINGS).</summary>
public sealed class BuildingResource(ICrudDbContext db) : CrudResource<BuildingResource, MapBuilding, CrudSearch, BuildingRequest, BuildingDto>(db)
{
    protected override string EntityName => "Toà nhà";

    protected override string? Describe(MapBuilding entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<MapBuilding, BuildingDto>> Projection =>
        x => new BuildingDto(x.Id, x.PublicId, x.Code, x.Name, x.Description, x.CreatedAt, x.UpdatedAt);

    protected override MapBuilding Create(BuildingRequest request) => MapBuilding.Create(request.Code, request.Name, request.Description);

    protected override void Update(MapBuilding entity, BuildingRequest request) => entity.Update(request.Code, request.Name, request.Description);

    protected override IQueryable<MapBuilding> Filter(IQueryable<MapBuilding> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<MapBuilding> Order(IQueryable<MapBuilding> query) => query.OrderBy(x => x.Code).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(MapBuilding entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("BUILDING_CODE_EXISTS", $"Mã toà nhà '{entity.Code}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(MapBuilding entity, CancellationToken ct)
    {
        if (await Db.Set<MapFloor>().AnyAsync(f => f.BuildingId == entity.Id, ct))
            throw new ConflictException("BUILDING_IN_USE", $"Toà nhà {entity.Code} còn tầng — xoá hết tầng trước.");
    }
}

public sealed class FloorSearch : CrudSearch
{
    public long? BuildingId { get; set; }
}

public sealed record FloorRequest(long BuildingId, string Name, int? FloorNumber);

public sealed record FloorDto(long Id, Guid PublicId, long BuildingId, string? BuildingName, string Name, int? FloorNumber, string? LayoutImageUrl,
    double CanvasWidth, double CanvasHeight, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record SetFloorLayoutRequest(string? LayoutImageUrl, double CanvasWidth, double CanvasHeight);

/// <summary>Tầng (monolith: map.MapFloor, quyền SPACE_FLOORS) — thuộc một Toà nhà.</summary>
public sealed class FloorResource(ICrudDbContext db, ITenantContext tenant, IOptions<SpaceOptions> options)
    : CrudResource<FloorResource, MapFloor, FloorSearch, FloorRequest, FloorDto>(db)
{
    private readonly SpaceOptions _options = options.Value;

    protected override string EntityName => "Tầng";

    protected override string? Describe(MapFloor entity) => entity.Name;

    protected override Expression<Func<MapFloor, FloorDto>> Projection => x => new FloorDto(
        x.Id, x.PublicId, x.BuildingId, Db.Set<MapBuilding>().Where(b => b.Id == x.BuildingId).Select(b => b.Name).FirstOrDefault(),
        x.Name, x.FloorNumber, x.LayoutImageUrl, x.CanvasWidth, x.CanvasHeight, x.CreatedAt, x.UpdatedAt);

    protected override MapFloor Create(FloorRequest request) => MapFloor.Create(request.BuildingId, request.Name, request.FloorNumber);

    protected override void Update(MapFloor entity, FloorRequest request) => entity.Update(request.BuildingId, request.Name, request.FloorNumber);

    protected override IQueryable<MapFloor> Filter(IQueryable<MapFloor> query, FloorSearch search)
    {
        if (search.BuildingId is { } buildingId) query = query.Where(x => x.BuildingId == buildingId);
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<MapFloor> Order(IQueryable<MapFloor> query) => query.OrderBy(x => x.BuildingId).ThenBy(x => x.FloorNumber).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(MapFloor entity, CancellationToken ct)
    {
        if (!await Db.Set<MapBuilding>().AnyAsync(b => b.Id == entity.BuildingId, ct))
            throw new BusinessRuleException("BUILDING_NOT_FOUND", "Toà nhà đã chọn không còn.");
    }

    protected override async Task EnsureDeletableAsync(MapFloor entity, CancellationToken ct)
    {
        if (await Db.Set<BookableSpace>().AnyAsync(s => s.FloorId == entity.Id, ct))
            throw new ConflictException("FLOOR_IN_USE", $"Tầng {entity.Name} còn không gian — xoá hết không gian trước.");
    }

    /// <summary>Đặt ảnh nền + kích thước canvas (trang sơ đồ trực quan) — không đi qua UpdateAsync/form cấu hình thường.</summary>
    public async Task<FloorDto> SetLayoutAsync(Guid publicId, SetFloorLayoutRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await LoadAsync(publicId, ct);
        var url = FloorLayout.Validate(request.LayoutImageUrl, _options.MediaPublicPrefix, tenant.RequireTenantId());
        entity.SetLayout(url, request.CanvasWidth, request.CanvasHeight);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }
}
