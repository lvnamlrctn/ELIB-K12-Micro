using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Holdings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Holdings.Application;

/// <summary>Loại kho (monolith: StoreTypeController, quyền STORE_TYPES).</summary>
public sealed class StoreTypeResource(ICrudDbContext db) : NamedCatalogResource<StoreTypeResource, StoreType>(db)
{
    public static readonly IReadOnlyList<string> Defaults = ["Kho mở", "Kho đóng"];

    protected override string EntityName => "Loại kho";

    protected override async Task EnsureDeletableAsync(StoreType entity, CancellationToken ct)
    {
        if (await Db.Set<Store>().AnyAsync(s => s.StoreTypeId == entity.Id, ct))
            throw new ConflictException("STORE_TYPE_IN_USE", $"Loại kho '{entity.Name}' đang được dùng cho kho, không xoá được.");
    }

    /// <summary>Đơn vị mới: loại kho mặc định (bỏ qua loại đã có cùng tên).</summary>
    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var name in Defaults.Where(n => !existing.Contains(n)))
        {
            Set.Add(NamedCatalogItem.New<StoreType>(name));
            added++;
        }
        await Db.SaveChangesAsync(ct);
        return added;
    }
}

public sealed record StoreRequest(string Code, string Name, long? StoreTypeId = null, string? Position = null, int? Capacity = null);

/// <summary>Kho kèm số bản sách đang thuộc kho.</summary>
public sealed record StoreDto(
    long Id, Guid PublicId, string Code, string Name, long? StoreTypeId, string? Position, int? Capacity, int ItemCount,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Kho (monolith: StoreController, quyền STORES). Mã kho duy nhất trong đơn vị; kho còn bản sách thì không xoá được.</summary>
public sealed class StoreResource(ICrudDbContext db) : CrudResource<StoreResource, Store, CrudSearch, StoreRequest, StoreDto>(db)
{
    public const string DefaultCode = "KC";

    protected override string EntityName => "Kho";

    protected override string? Describe(Store entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<Store, StoreDto>> Projection => x => new StoreDto(
        x.Id, x.PublicId, x.Code, x.Name, x.StoreTypeId, x.Position, x.Capacity, Db.Set<Item>().Count(i => i.StoreId == x.Id),
        x.CreatedAt, x.UpdatedAt);

    protected override Store Create(StoreRequest request) => Store.Create(request.Code, request.Name, request.StoreTypeId, request.Position, request.Capacity);

    protected override void Update(Store entity, StoreRequest request) =>
        entity.Update(request.Code, request.Name, request.StoreTypeId, request.Position, request.Capacity);

    protected override IQueryable<Store> Filter(IQueryable<Store> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Store> Order(IQueryable<Store> query) => query.OrderBy(x => x.Code).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(Store entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("STORE_CODE_EXISTS", $"Mã kho '{entity.Code}' đã có.");
        if (entity.StoreTypeId is { } type && !await Db.Set<StoreType>().AnyAsync(t => t.Id == type, ct))
            throw new BusinessRuleException("STORE_TYPE_NOT_FOUND", "Loại kho đã chọn không còn.");
    }

    protected override async Task EnsureDeletableAsync(Store entity, CancellationToken ct)
    {
        var count = await Db.Set<Item>().CountAsync(i => i.StoreId == entity.Id, ct);
        if (count > 0) throw new ConflictException("STORE_NOT_EMPTY", $"Kho {entity.Code} còn {count} bản sách — chuyển kho hết rồi mới xoá được.");
    }

    /// <summary>Đơn vị mới: một kho chung để đăng ký cá biệt được ngay.</summary>
    public async Task<bool> AddDefaultAsync(CancellationToken ct)
    {
        if (await Set.AnyAsync(ct)) return false;
        var type = await Db.Set<StoreType>().Where(t => t.Name == StoreTypeResource.Defaults[0]).Select(t => (long?)t.Id).FirstOrDefaultAsync(ct);
        Set.Add(Store.Create(DefaultCode, "Kho chung", type, null, null));
        await Db.SaveChangesAsync(ct);
        return true;
    }
}
