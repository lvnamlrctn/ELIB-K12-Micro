using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Space;
using Elib.Space.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

/// <summary>
/// Phát <see cref="ShelfChanged"/>/<see cref="FloorChanged"/> cho holdings (outbox — gọi trước SaveChanges để event cùng transaction với dữ liệu,
/// đúng khuôn StoreEvents của holdings). Holdings giữ bản sao giá/ngăn/tầng để xếp giá trực quan; space không biết số bản sách nên không chặn xoá giá.
/// </summary>
public sealed class ShelfEvents(ICrudDbContext db, IPublishEndpoint publisher, ITenantContext tenant, ICurrentActor actor)
{
    private EventActor Actor => new(actor.Id, actor.Kind);

    /// <summary>
    /// Phát trạng thái hiện tại của giá. <paramref name="pendingRow"/>: ngăn đang thêm/sửa/xoá trong cùng lần lưu (chưa có trong DB) — gộp vào danh sách ngăn.
    /// Chỉ giá (SHELF) mới phát; loại khác bỏ qua.
    /// </summary>
    public async Task PublishShelfAsync(MapFixture shelf, bool deleted, ShelfRow? pendingRow, bool pendingRowDeleted, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        if (shelf.PublicId == Guid.Empty) shelf.PublicId = Guid.CreateVersion7();
        var floorPublicId = await db.Set<MapFloor>().AsNoTracking().Where(f => f.Id == shelf.FloorId).Select(f => (Guid?)f.PublicId).FirstOrDefaultAsync(ct);
        if (floorPublicId is null) return;
        var rows = new List<ShelfRow>();
        if (!deleted)
        {
            rows = await db.Set<ShelfRow>().AsNoTracking().Where(r => r.FixtureId == shelf.Id).OrderBy(r => r.RowIndex).ToListAsync(ct);
            if (pendingRow is not null)
            {
                rows.RemoveAll(r => r.Id == pendingRow.Id && pendingRow.Id != 0);
                if (!pendingRowDeleted)
                {
                    if (pendingRow.PublicId == Guid.Empty) pendingRow.PublicId = Guid.CreateVersion7();
                    rows.Add(pendingRow);
                }
            }
        }
        await publisher.Publish(new ShelfChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = Actor,
            ShelfPublicId = shelf.PublicId,
            Code = shelf.Code,
            Name = shelf.Name,
            FloorPublicId = floorPublicId.Value,
            StorePublicId = shelf.StorePublicId,
            PositionX = shelf.PositionX,
            PositionY = shelf.PositionY,
            Width = shelf.Width,
            Height = shelf.Height,
            ColorHex = shelf.ColorHex,
            Rows = rows.OrderBy(r => r.RowIndex).Select(r => new ShelfRowState
            {
                RowPublicId = r.PublicId, RowIndex = r.RowIndex, DdcStart = r.DdcStart, DdcEnd = r.DdcEnd, Description = r.Description,
            }).ToList(),
            Deleted = deleted,
        }, ct);
    }

    /// <param name="buildingName">Tên toà đang chờ lưu (đổi tên toà) — mặc định đọc từ DB.</param>
    public async Task PublishFloorAsync(MapFloor floor, bool deleted, CancellationToken ct, string? buildingName = null)
    {
        ArgumentNullException.ThrowIfNull(floor);
        if (floor.PublicId == Guid.Empty) floor.PublicId = Guid.CreateVersion7();
        var building = buildingName ?? await db.Set<MapBuilding>().AsNoTracking().Where(b => b.Id == floor.BuildingId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        await publisher.Publish(new FloorChanged
        {
            TenantId = tenant.RequireTenantId(),
            Actor = Actor,
            FloorPublicId = floor.PublicId,
            Name = floor.Name,
            FloorNumber = floor.FloorNumber,
            BuildingName = building,
            CanvasWidth = floor.CanvasWidth,
            CanvasHeight = floor.CanvasHeight,
            LayoutImageUrl = floor.LayoutImageUrl,
            Deleted = deleted,
        }, ct);
    }

    /// <summary>Đổi tên toà → phát lại mọi tầng của toà (FloorChanged mang tên toà).</summary>
    public async Task PublishFloorsOfBuildingAsync(long buildingId, string buildingName, CancellationToken ct)
    {
        foreach (var floor in await db.Set<MapFloor>().AsNoTracking().Where(f => f.BuildingId == buildingId).ToListAsync(ct))
            await PublishFloorAsync(floor, deleted: false, ct, buildingName);
    }

    /// <summary>
    /// Phát lại toàn bộ tầng và mọi giá của đơn vị đang chọn — khi space khởi động (holdings dựng bản sao cho dữ liệu có trước khi có sự kiện này).
    /// Trả số giá đã phát. Nơi gọi SaveChanges để đẩy outbox.
    /// </summary>
    public async Task<int> RepublishAllAsync(CancellationToken ct)
    {
        var shelves = await db.Set<MapFixture>().AsNoTracking().Where(f => f.ObjectType == FixtureType.Shelf).ToListAsync(ct);
        // Mọi tầng (không chỉ tầng đang có giá): giá thêm sau này vào tầng chưa có bản sao ở holdings vẫn hiện trên sơ đồ.
        foreach (var floor in await db.Set<MapFloor>().AsNoTracking().ToListAsync(ct))
            await PublishFloorAsync(floor, deleted: false, ct);
        foreach (var shelf in shelves) await PublishShelfAsync(shelf, deleted: false, null, false, ct);
        return shelves.Count;
    }
}

public sealed class ShelfRowSearch : CrudSearch
{
    public long? FixtureId { get; set; }
}

public sealed record ShelfRowRequest(long FixtureId, int RowIndex, string DdcStart, string DdcEnd, string? Description);

public sealed record ShelfRowDto(long Id, Guid PublicId, long FixtureId, int RowIndex, string DdcStart, string DdcEnd, string? Description,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Ngăn theo khoảng DDC của giá sách (monolith: map.MapShelfRow, quyền SPACE_FIXTURES). Mọi thay đổi phát lại <see cref="ShelfChanged"/> của giá.</summary>
public sealed class ShelfRowResource(ICrudDbContext db, ShelfEvents events)
    : CrudResource<ShelfRowResource, ShelfRow, ShelfRowSearch, ShelfRowRequest, ShelfRowDto>(db)
{
    protected override string EntityName => "Ngăn giá sách";

    protected override string? Describe(ShelfRow entity) => $"Ngăn {entity.RowIndex} ({entity.DdcStart} – {entity.DdcEnd})";

    protected override Expression<Func<ShelfRow, ShelfRowDto>> Projection => x =>
        new ShelfRowDto(x.Id, x.PublicId, x.FixtureId, x.RowIndex, x.DdcStart, x.DdcEnd, x.Description, x.CreatedAt, x.UpdatedAt);

    protected override ShelfRow Create(ShelfRowRequest request) => ShelfRow.Create(request.FixtureId, request.RowIndex, request.DdcStart, request.DdcEnd, request.Description);

    protected override void Update(ShelfRow entity, ShelfRowRequest request) => entity.Update(request.RowIndex, request.DdcStart, request.DdcEnd, request.Description);

    protected override IQueryable<ShelfRow> Filter(IQueryable<ShelfRow> query, ShelfRowSearch search) =>
        search.FixtureId is { } fixtureId ? query.Where(x => x.FixtureId == fixtureId) : query;

    protected override IOrderedQueryable<ShelfRow> Order(IQueryable<ShelfRow> query) => query.OrderBy(x => x.FixtureId).ThenBy(x => x.RowIndex);

    protected override async Task ValidateAsync(ShelfRow entity, CancellationToken ct)
    {
        if (!await Db.Set<MapFixture>().AnyAsync(x => x.Id == entity.FixtureId && x.ObjectType == FixtureType.Shelf, ct))
            throw new BusinessRuleException("SHELF_NOT_FOUND", "Giá sách đã chọn không còn (hoặc vật dụng không phải giá sách).");
        var others = await Set.AsNoTracking().Where(x => x.FixtureId == entity.FixtureId && x.Id != entity.Id).ToListAsync(ct);
        if (others.Any(x => x.RowIndex == entity.RowIndex))
            throw new ConflictException("ROW_INDEX_EXISTS", $"Giá đã có ngăn số {entity.RowIndex}.");
        if (others.FirstOrDefault(x => ShelfRow.Overlaps(entity.DdcStart, entity.DdcEnd, x.DdcStart, x.DdcEnd)) is { } clash)
            throw new ConflictException("SHELF_ROW_OVERLAP", $"Khoảng DDC {entity.DdcStart} – {entity.DdcEnd} chồng với ngăn {clash.RowIndex} ({clash.DdcStart} – {clash.DdcEnd}).");
    }

    protected override async Task OnSavingAsync(ShelfRow entity, CrudChange change, CancellationToken ct)
    {
        var shelf = await Db.Set<MapFixture>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.FixtureId, ct);
        if (shelf is not null) await events.PublishShelfAsync(shelf, deleted: false, entity, pendingRowDeleted: change == CrudChange.Deleted, ct);
    }
}
