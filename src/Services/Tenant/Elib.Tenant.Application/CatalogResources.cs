using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Tenant.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Tenant.Application;

public sealed record NameRequest(string Name);

public sealed record NamedItemDto(long Id, Guid PublicId, string Name, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Danh mục chỉ có tên: tìm theo tên, sắp theo tên, tên không trùng trong đơn vị.</summary>
public abstract class NamedCatalogResource<TSelf, TItem>(ICrudDbContext db)
    : CrudResource<TSelf, TItem, CrudSearch, NameRequest, NamedItemDto>(db)
    where TSelf : NamedCatalogResource<TSelf, TItem>
    where TItem : NamedCatalogItem, new()
{
    protected override Expression<Func<TItem, NamedItemDto>> Projection =>
        x => new NamedItemDto(x.Id, x.PublicId, x.Name, x.CreatedAt, x.UpdatedAt);

    protected override TItem Create(NameRequest request) => NamedCatalogItem.New<TItem>(request.Name);

    protected override void Update(TItem entity, NameRequest request) => entity.Rename(request.Name);

    protected override string? Describe(TItem entity) => entity.Name;

    protected override IQueryable<TItem> Filter(IQueryable<TItem> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<TItem> Order(IQueryable<TItem> query) => query.OrderBy(x => x.Name).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(TItem entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, ct))
            throw new ConflictException("NAME_EXISTS", $"{EntityName} '{entity.Name}' đã có.");
    }
}

public sealed class NationalityResource(ICrudDbContext db) : NamedCatalogResource<NationalityResource, Nationality>(db)
{
    protected override string EntityName => "Quốc tịch";
}

public sealed class EthnicityResource(ICrudDbContext db) : NamedCatalogResource<EthnicityResource, Ethnicity>(db)
{
    protected override string EntityName => "Dân tộc";
}

public sealed class AcademicTitleResource(ICrudDbContext db) : NamedCatalogResource<AcademicTitleResource, AcademicTitle>(db)
{
    protected override string EntityName => "Học hàm học vị";
}

public sealed class DegreeResource(ICrudDbContext db) : NamedCatalogResource<DegreeResource, Degree>(db)
{
    protected override string EntityName => "Trình độ";
}

public sealed class PositionResource(ICrudDbContext db) : NamedCatalogResource<PositionResource, Position>(db)
{
    protected override string EntityName => "Chức vụ";
}

public sealed record CurrencyRequest(string Code, string Name, decimal ExchangeRate, int? Status);

public sealed record CurrencyDto(long Id, Guid PublicId, string Code, string Name, decimal ExchangeRate, int Status);

public sealed class CurrencyResource(ICrudDbContext db) : CrudResource<CurrencyResource, Currency, CrudSearch, CurrencyRequest, CurrencyDto>(db)
{
    protected override string EntityName => "Tiền tệ";

    protected override string? Describe(Currency entity) => $"{entity.Code} — {entity.Name}";

    protected override Expression<Func<Currency, CurrencyDto>> Projection =>
        x => new CurrencyDto(x.Id, x.PublicId, x.Code, x.Name, x.ExchangeRate, x.Status);

    protected override Currency Create(CurrencyRequest request) => Currency.Create(request.Code, request.Name, request.ExchangeRate, request.Status);

    protected override void Update(Currency entity, CurrencyRequest request) =>
        entity.Update(request.Code, request.Name, request.ExchangeRate, request.Status);

    protected override IQueryable<Currency> Filter(IQueryable<Currency> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311
        return query.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Currency> Order(IQueryable<Currency> query) => query.OrderBy(x => x.Code);

    protected override async Task ValidateAsync(Currency entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("CURRENCY_CODE_EXISTS", $"Mã tiền tệ '{entity.Code}' đã có.");
    }
}
