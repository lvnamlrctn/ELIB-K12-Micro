using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Space.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Space.Application;

public sealed class LockerCabinetSearch : CrudSearch
{
    public long? FloorId { get; set; }
    public long? BuildingId { get; set; }
}

public sealed record LockerCabinetRequest(long FloorId, string Code, string Name, int Rows, int Cols, string? ColorHex, string? IconName, string? Note);

public sealed record LockerCabinetDto(
    long Id, Guid PublicId, long FloorId, string? FloorName, long? BuildingId, string? BuildingName, string Code, string Name, int Rows, int Cols,
    double? PositionX, double? PositionY, double? Width, double? Height, string? ColorHex, string? IconName, string? Note,
    int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record PlaceLockerRequest(double? PositionX, double? PositionY, double? Width, double? Height);

/// <summary>
/// Tủ đựng đồ (monolith: PrintBook.Cabinet, quyền SPACE_LOCKERS). Tạo/đổi Rows×Cols đồng bộ lại lưới ngăn
/// (<see cref="LockerCompartment"/>) ngay sau khi lưu — xem <see cref="SyncCompartmentsAsync"/>. Vị trí trên sơ đồ sửa qua
/// <see cref="PlaceAsync"/> riêng, không qua form cấu hình thường.
/// </summary>
public sealed class LockerCabinetResource(ICrudDbContext db)
    : CrudResource<LockerCabinetResource, LockerCabinet, LockerCabinetSearch, LockerCabinetRequest, LockerCabinetDto>(db)
{
    protected override string EntityName => "Tủ đựng đồ";

    protected override string? Describe(LockerCabinet entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<LockerCabinet, LockerCabinetDto>> Projection => x => new LockerCabinetDto(
        x.Id, x.PublicId, x.FloorId, Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.Name).FirstOrDefault(),
        Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault(),
        Db.Set<MapBuilding>().Where(b => b.Id == Db.Set<MapFloor>().Where(f => f.Id == x.FloorId).Select(f => f.BuildingId).FirstOrDefault())
            .Select(b => b.Name).FirstOrDefault(),
        x.Code, x.Name, x.Rows, x.Cols, x.PositionX, x.PositionY, x.Width, x.Height, x.ColorHex, x.IconName, x.Note,
        x.Status, x.CreatedAt, x.UpdatedAt);

    protected override LockerCabinet Create(LockerCabinetRequest request) =>
        LockerCabinet.Create(request.FloorId, request.Code, request.Name, request.Rows, request.Cols, request.ColorHex, request.IconName, request.Note);

    protected override void Update(LockerCabinet entity, LockerCabinetRequest request) =>
        entity.Update(request.FloorId, request.Code, request.Name, request.Rows, request.Cols, request.ColorHex, request.IconName, request.Note);

    protected override IQueryable<LockerCabinet> Filter(IQueryable<LockerCabinet> query, LockerCabinetSearch search)
    {
        if (search.FloorId is { } floorId) query = query.Where(x => x.FloorId == floorId);
        if (search.BuildingId is { } buildingId)
        {
            var floorIds = Db.Set<MapFloor>().Where(f => f.BuildingId == buildingId).Select(f => f.Id);
            query = query.Where(x => floorIds.Contains(x.FloorId));
        }
        if (search.Term is { } term)
        {
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
        }
        return query;
    }

    protected override IOrderedQueryable<LockerCabinet> Order(IQueryable<LockerCabinet> query) => query.OrderBy(x => x.FloorId).ThenBy(x => x.Code);

    protected override async Task ValidateAsync(LockerCabinet entity, CancellationToken ct)
    {
        if (!await Db.Set<MapFloor>().AnyAsync(f => f.Id == entity.FloorId, ct))
            throw new BusinessRuleException("FLOOR_NOT_FOUND", "Tầng đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("CABINET_CODE_EXISTS", $"Mã tủ '{entity.Code}' đã có.");
    }

    public override async Task<LockerCabinetDto> AddAsync(LockerCabinetRequest request, CancellationToken ct)
    {
        var dto = await base.AddAsync(request, ct);
        var cabinet = await LoadAsync(dto.PublicId, ct);
        await SyncCompartmentsAsync(cabinet, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(cabinet);
    }

    public override async Task<LockerCabinetDto> UpdateAsync(Guid publicId, LockerCabinetRequest request, CancellationToken ct)
    {
        await base.UpdateAsync(publicId, request, ct);
        var cabinet = await LoadAsync(publicId, ct);
        await SyncCompartmentsAsync(cabinet, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(cabinet);
    }

    /// <summary>Đặt vị trí trên sơ đồ tầng (trang sơ đồ trực quan) — không đi qua UpdateAsync/form cấu hình thường.</summary>
    public async Task<LockerCabinetDto> PlaceAsync(Guid publicId, PlaceLockerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await LoadAsync(publicId, ct);
        entity.SetPosition(request.PositionX, request.PositionY, request.Width, request.Height);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>Sinh/đồng bộ lưới ngăn theo Rows×Cols — thêm ngăn mới nếu lưới lớn hơn, xoá mềm ngăn dư nếu nhỏ hơn.</summary>
    private async Task SyncCompartmentsAsync(LockerCabinet cabinet, CancellationToken ct)
    {
        var existing = await Db.Set<LockerCompartment>().Where(c => c.CabinetId == cabinet.Id).ToListAsync(ct);
        for (var row = 1; row <= cabinet.Rows; row++)
        {
            for (var col = 1; col <= cabinet.Cols; col++)
            {
                if (existing.Any(c => c.RowIndex == row && c.ColIndex == col)) continue;
                Db.Set<LockerCompartment>().Add(LockerCompartment.Create(cabinet.Id, row, col, $"{cabinet.Code}-{row}{col}", null, null));
            }
        }
        var outOfGrid = existing.Where(c => c.RowIndex > cabinet.Rows || c.ColIndex > cabinet.Cols).ToList();
        if (outOfGrid.Count > 0) Db.Set<LockerCompartment>().RemoveRange(outOfGrid);
    }
}

public sealed class LockerCompartmentSearch : CrudSearch
{
    public long? CabinetId { get; set; }
}

public sealed record LockerCompartmentRequest(long CabinetId, int RowIndex, int ColIndex, string Code, string? Name, string? Note);

public sealed record LockerCompartmentDto(long Id, Guid PublicId, long CabinetId, int RowIndex, int ColIndex, string Code, string? Name, string? Note,
    int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// Ngăn trong tủ (monolith: PrintBook.CabinetCompartment, quyền SPACE_LOCKERS). Lưới ban đầu sinh tự động theo Rows×Cols
/// của tủ (xem LockerCabinetResource); CRUD đây cho thêm/sửa/xoá riêng từng ngăn (đúng bản gốc).
/// </summary>
public sealed class LockerCompartmentResource(ICrudDbContext db)
    : CrudResource<LockerCompartmentResource, LockerCompartment, LockerCompartmentSearch, LockerCompartmentRequest, LockerCompartmentDto>(db)
{
    protected override string EntityName => "Ngăn tủ";

    protected override string? Describe(LockerCompartment entity) => entity.Code;

    protected override Expression<Func<LockerCompartment, LockerCompartmentDto>> Projection => x =>
        new LockerCompartmentDto(x.Id, x.PublicId, x.CabinetId, x.RowIndex, x.ColIndex, x.Code, x.Name, x.Note, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override LockerCompartment Create(LockerCompartmentRequest request) =>
        LockerCompartment.Create(request.CabinetId, request.RowIndex, request.ColIndex, request.Code, request.Name, request.Note);

    protected override void Update(LockerCompartment entity, LockerCompartmentRequest request) =>
        entity.Update(request.Code, request.Name, request.Note);

    protected override IQueryable<LockerCompartment> Filter(IQueryable<LockerCompartment> query, LockerCompartmentSearch search) =>
        search.CabinetId is { } cabinetId ? query.Where(x => x.CabinetId == cabinetId) : query;

    protected override IOrderedQueryable<LockerCompartment> Order(IQueryable<LockerCompartment> query) => query.OrderBy(x => x.RowIndex).ThenBy(x => x.ColIndex);

    protected override async Task ValidateAsync(LockerCompartment entity, CancellationToken ct)
    {
        if (!await Db.Set<LockerCabinet>().AnyAsync(c => c.Id == entity.CabinetId, ct))
            throw new BusinessRuleException("CABINET_NOT_FOUND", "Tủ đã chọn không còn.");
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("COMPARTMENT_CODE_EXISTS", $"Mã ngăn '{entity.Code}' đã có.");
    }
}
