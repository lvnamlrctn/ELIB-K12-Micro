using System.Linq.Expressions;
using Elib.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.Crud;

public sealed record NameRequest(string Name);

public sealed record NamedItemDto(long Id, Guid PublicId, string Name, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Danh mục chỉ có tên: tìm theo tên, sắp theo tên, tên không trùng trong đơn vị, nhập Excel một cột tên.</summary>
public abstract class NamedCatalogResource<TSelf, TItem>(ICrudDbContext db)
    : CrudResource<TSelf, TItem, CrudSearch, NameRequest, NamedItemDto>(db), ICrudImportable<NameRequest>
    where TSelf : NamedCatalogResource<TSelf, TItem>
    where TItem : NamedCatalogItem, new()
{
    /// <summary>Import như monolith: một cột tên (file cũ tiêu đề "Name" vẫn nhận).</summary>
    public IReadOnlyList<CrudImportColumn> ImportColumns =>
        [new("name", "Tên", Required: true, Note: "Mỗi dòng một mục, không trùng với mục đã có.", "Name", "Ten " + EntityName)];

    public NameRequest MapImportRow(CrudImportRow row) => new(row.Required("name", "Tên"));

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

public static class NamedCatalogModelBuilderExtensions
{
    /// <summary>Bảng danh mục tên: độ dài tên + tên duy nhất trong đơn vị (bỏ qua bản ghi đã xoá mềm).</summary>
    public static ModelBuilder NamedCatalog<T>(this ModelBuilder modelBuilder, string table, int maxName) where T : NamedCatalogItem =>
        modelBuilder.Entity<T>(e =>
        {
            e.ToTable(table);
            e.Property(x => x.Name).HasMaxLength(maxName);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("is_deleted = false");
        });
}
