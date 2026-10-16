using System.Linq.Expressions;
using Elib.Acquisition.Domain;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Acquisition.Application;

/// <summary>Nguồn bổ sung (monolith: AbSourceController, quyền AB_SOURCES).</summary>
public sealed class SourceResource(ICrudDbContext db) : NamedCatalogResource<SourceResource, AcquisitionSource>(db)
{
    public static readonly IReadOnlyList<string> Defaults = ["Mua", "Biếu tặng", "Trao đổi", "Lưu chiểu"];

    protected override string EntityName => "Nguồn bổ sung";

    protected override async Task EnsureDeletableAsync(AcquisitionSource entity, CancellationToken ct)
    {
        if (await Db.Set<PurchaseOrder>().AnyAsync(o => o.SourceId == entity.Id, ct) || await Db.Set<Receipt>().AnyAsync(r => r.SourceId == entity.Id, ct))
            throw new ConflictException("SOURCE_IN_USE", $"Nguồn '{entity.Name}' đang được dùng ở đơn đặt / phiếu nhận, không xoá được.");
    }

    /// <summary>Đơn vị mới: nguồn bổ sung mặc định (bỏ qua nguồn đã có cùng tên).</summary>
    public async Task<int> AddMissingDefaultsAsync(CancellationToken ct)
    {
        var existing = await Set.Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var name in Defaults.Where(n => !existing.Contains(n)))
        {
            Set.Add(NamedCatalogItem.New<AcquisitionSource>(name));
            added++;
        }
        await Db.SaveChangesAsync(ct);
        return added;
    }
}

public sealed record SupplierDto(
    long Id, Guid PublicId, string Name, string? Address, string? Phone, string? Fax, string? Email, string? Website, string? TaxCode, string? BankAccount,
    string? BankName, string? Representative, string? Position, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Nhà cung cấp (monolith: SupplierController, quyền SUPPLIERS). Tên không trùng trong đơn vị.</summary>
public sealed class SupplierResource(ICrudDbContext db) : CrudResource<SupplierResource, Supplier, CrudSearch, SupplierData, SupplierDto>(db)
{
    protected override string EntityName => "Nhà cung cấp";

    protected override string? Describe(Supplier entity) => entity.Name;

    protected override Expression<Func<Supplier, SupplierDto>> Projection => x => new SupplierDto(
        x.Id, x.PublicId, x.Name, x.Address, x.Phone, x.Fax, x.Email, x.Website, x.TaxCode, x.BankAccount, x.BankName, x.Representative, x.Position,
        x.CreatedAt, x.UpdatedAt);

    protected override Supplier Create(SupplierData request) => Supplier.Create(request);

    protected override void Update(Supplier entity, SupplierData request) => entity.Update(request);

    protected override IQueryable<Supplier> Filter(IQueryable<Supplier> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || (x.Phone != null && x.Phone.Contains(term))
            || (x.TaxCode != null && x.TaxCode.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<Supplier> Order(IQueryable<Supplier> query) => query.OrderBy(x => x.Name).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(Supplier entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Name == entity.Name && x.Id != entity.Id, ct))
            throw new ConflictException("NAME_EXISTS", $"Nhà cung cấp '{entity.Name}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(Supplier entity, CancellationToken ct)
    {
        if (await Db.Set<PurchaseOrder>().AnyAsync(o => o.SupplierId == entity.Id, ct) || await Db.Set<Receipt>().AnyAsync(r => r.SupplierId == entity.Id, ct))
            throw new ConflictException("SUPPLIER_IN_USE", $"Nhà cung cấp '{entity.Name}' đang có đơn đặt / phiếu nhận, không xoá được.");
    }
}

public sealed record BudgetRequest(string Name, decimal? Amount = null, DateOnly? StartsOn = null, DateOnly? EndsOn = null, string? Note = null);

public sealed record BudgetDto(
    long Id, Guid PublicId, string Name, decimal? Amount, DateOnly? StartsOn, DateOnly? EndsOn, string? Note, int Status, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Kinh phí (monolith: BudgetController, quyền BUDGETS).</summary>
public sealed class BudgetResource(ICrudDbContext db) : CrudResource<BudgetResource, Budget, CrudSearch, BudgetRequest, BudgetDto>(db)
{
    protected override string EntityName => "Kinh phí";

    protected override string? Describe(Budget entity) => entity.Name;

    protected override Expression<Func<Budget, BudgetDto>> Projection => x => new BudgetDto(
        x.Id, x.PublicId, x.Name, x.Amount, x.StartsOn, x.EndsOn, x.Note, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override Budget Create(BudgetRequest request) => Budget.Create(request.Name, request.Amount, request.StartsOn, request.EndsOn, request.Note);

    protected override void Update(Budget entity, BudgetRequest request) =>
        entity.Update(request.Name, request.Amount, request.StartsOn, request.EndsOn, request.Note);

    protected override IQueryable<Budget> Filter(IQueryable<Budget> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override async Task EnsureDeletableAsync(Budget entity, CancellationToken ct)
    {
        if (await Db.Set<Fund>().AnyAsync(f => f.BudgetId == entity.Id, ct) || await Db.Set<PurchaseOrder>().AnyAsync(o => o.BudgetId == entity.Id, ct)
            || await Db.Set<Receipt>().AnyAsync(r => r.BudgetId == entity.Id, ct))
            throw new ConflictException("BUDGET_IN_USE", $"Kinh phí '{entity.Name}' đang được dùng ở quỹ / đơn đặt / phiếu nhận, không xoá được.");
    }
}

public sealed record FundRequest(
    string Name, long? BudgetId = null, decimal? Amount = null, string? Manager = null, string? Purpose = null, string? Note = null);

public sealed record FundDto(
    long Id, Guid PublicId, string Name, long? BudgetId, string? BudgetName, decimal? Amount, string? Manager, string? Purpose, string? Note,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Quỹ (monolith: FundController, quyền FUNDS).</summary>
public sealed class FundResource(ICrudDbContext db) : CrudResource<FundResource, Fund, CrudSearch, FundRequest, FundDto>(db)
{
    protected override string EntityName => "Quỹ";

    protected override string? Describe(Fund entity) => entity.Name;

    protected override Expression<Func<Fund, FundDto>> Projection => x => new FundDto(
        x.Id, x.PublicId, x.Name, x.BudgetId, Db.Set<Budget>().Where(b => b.Id == x.BudgetId).Select(b => b.Name).FirstOrDefault(), x.Amount, x.Manager,
        x.Purpose, x.Note, x.CreatedAt, x.UpdatedAt);

    protected override Fund Create(FundRequest request) =>
        Fund.Create(request.Name, request.BudgetId, request.Amount, request.Manager, request.Purpose, request.Note);

    protected override void Update(Fund entity, FundRequest request) =>
        entity.Update(request.Name, request.BudgetId, request.Amount, request.Manager, request.Purpose, request.Note);

    protected override IQueryable<Fund> Filter(IQueryable<Fund> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term) || (x.Manager != null && x.Manager.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override async Task ValidateAsync(Fund entity, CancellationToken ct)
    {
        if (entity.BudgetId is { } budget && !await Db.Set<Budget>().AnyAsync(b => b.Id == budget, ct))
            throw new BusinessRuleException("BUDGET_NOT_FOUND", "Kinh phí đã chọn không còn.");
    }

    protected override async Task EnsureDeletableAsync(Fund entity, CancellationToken ct)
    {
        if (await Db.Set<PurchaseOrder>().AnyAsync(o => o.FundId == entity.Id, ct) || await Db.Set<Receipt>().AnyAsync(r => r.FundId == entity.Id, ct))
            throw new ConflictException("FUND_IN_USE", $"Quỹ '{entity.Name}' đang được dùng ở đơn đặt / phiếu nhận, không xoá được.");
    }
}

/// <summary>Kiểm tra nguồn / nhà cung cấp / kinh phí / quỹ đã chọn còn trong đơn vị.</summary>
internal static class AcquisitionRefChecks
{
    public static async Task EnsureAsync(ICrudDbContext db, AcquisitionRefs refs, CancellationToken ct)
    {
        if (refs.SourceId is { } source && !await db.Set<AcquisitionSource>().AnyAsync(x => x.Id == source, ct))
            throw new BusinessRuleException("SOURCE_NOT_FOUND", "Nguồn bổ sung đã chọn không còn.");
        if (refs.SupplierId is { } supplier && !await db.Set<Supplier>().AnyAsync(x => x.Id == supplier, ct))
            throw new BusinessRuleException("SUPPLIER_NOT_FOUND", "Nhà cung cấp đã chọn không còn.");
        if (refs.BudgetId is { } budget && !await db.Set<Budget>().AnyAsync(x => x.Id == budget, ct))
            throw new BusinessRuleException("BUDGET_NOT_FOUND", "Kinh phí đã chọn không còn.");
        if (refs.FundId is { } fund && !await db.Set<Fund>().AnyAsync(x => x.Id == fund, ct))
            throw new BusinessRuleException("FUND_NOT_FOUND", "Quỹ đã chọn không còn.");
    }
}

/// <summary>Ô chọn chung của màn đơn đặt / phiếu nhận (cán bộ không có quyền danh mục vẫn chọn được).</summary>
public sealed record AcquisitionOptions(
    IReadOnlyList<OptionItem> Sources, IReadOnlyList<OptionItem> Suppliers, IReadOnlyList<OptionItem> Budgets, IReadOnlyList<OptionItem> Funds);

public sealed record OptionItem(long Id, string Name);

public sealed class AcquisitionOptionsQuery(ICrudDbContext db)
{
    public async Task<AcquisitionOptions> GetAsync(CancellationToken ct) => new(
        await db.Set<AcquisitionSource>().AsNoTracking().OrderBy(x => x.Name).Select(x => new OptionItem(x.Id, x.Name)).ToListAsync(ct),
        await db.Set<Supplier>().AsNoTracking().OrderBy(x => x.Name).Select(x => new OptionItem(x.Id, x.Name)).ToListAsync(ct),
        await db.Set<Budget>().AsNoTracking().Where(x => x.Status == IHasStatus.Active).OrderBy(x => x.Name).Select(x => new OptionItem(x.Id, x.Name)).ToListAsync(ct),
        await db.Set<Fund>().AsNoTracking().OrderBy(x => x.Name).Select(x => new OptionItem(x.Id, x.Name)).ToListAsync(ct));
}
