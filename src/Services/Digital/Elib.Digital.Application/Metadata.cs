using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

public sealed record MetadataSchemaRequest(string? NameSpace, string? ShortId, string? Language, int? Status);

public sealed record MetadataSchemaDto(long Id, Guid PublicId, string NameSpace, string ShortId, string? Language, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Lược đồ metadata (LRC: MetadataSchemaRegistryController, quyền DIGITAL_METADATA).</summary>
public sealed class MetadataSchemaResource(ICrudDbContext db)
    : CrudResource<MetadataSchemaResource, MetadataSchema, CrudSearch, MetadataSchemaRequest, MetadataSchemaDto>(db)
{
    protected override string EntityName => "Lược đồ metadata";

    protected override string? Describe(MetadataSchema entity) => entity.ShortId;

    protected override Expression<Func<MetadataSchema, MetadataSchemaDto>> Projection => x =>
        new MetadataSchemaDto(x.Id, x.PublicId, x.NameSpace, x.ShortId, x.Language, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override MetadataSchema Create(MetadataSchemaRequest request) =>
        MetadataSchema.Create(request.NameSpace, request.ShortId, request.Language, request.Status ?? IHasStatus.Active);

    protected override void Update(MetadataSchema entity, MetadataSchemaRequest request) =>
        entity.Update(request.NameSpace, request.ShortId, request.Language, request.Status ?? entity.Status);

    protected override async Task ValidateAsync(MetadataSchema entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.ShortId == entity.ShortId && x.Id != entity.Id, ct))
            throw new ConflictException("SCHEMA_EXISTS", $"Lược đồ '{entity.ShortId}' đã có.");
    }

    protected override async Task EnsureDeletableAsync(MetadataSchema entity, CancellationToken ct)
    {
        if (await Db.Set<MetadataField>().AnyAsync(f => f.SchemaId == entity.Id, ct))
            throw new ConflictException("SCHEMA_HAS_FIELDS", $"Lược đồ '{entity.ShortId}' còn trường — xoá hoặc chuyển trường trước.");
    }

    protected override IQueryable<MetadataSchema> Filter(IQueryable<MetadataSchema> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.ShortId.ToLower().Contains(term) || x.NameSpace.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<MetadataSchema> Order(IQueryable<MetadataSchema> query) => query.OrderBy(x => x.ShortId);
}

public sealed class MetadataFieldSearch : CrudSearch
{
    public Guid? SchemaPublicId { get; set; }
}

public sealed record MetadataFieldRequest(
    Guid SchemaPublicId, string Field, string? Subfield, string DescriptionVn, string? DescriptionEn, int SortOrder = 0, string? Input = null, int? ExportField = null,
    int Status = IHasStatus.Active);

public sealed record MetadataFieldDto(
    long Id, Guid PublicId, Guid SchemaPublicId, string SchemaShortId, string Field, string? Subfield, string DescriptionVn, string? DescriptionEn, int SortOrder,
    string Input, int? ExportField, int Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Trường metadata (LRC: MetaDataFieldRegisteryController, quyền DIGITAL_METADATA).</summary>
public sealed class MetadataFieldResource(ICrudDbContext db)
    : CrudResource<MetadataFieldResource, MetadataField, MetadataFieldSearch, MetadataFieldRequest, MetadataFieldDto>(db)
{
    protected override string EntityName => "Trường metadata";

    protected override string? Describe(MetadataField entity) => entity.Label;

    protected override Expression<Func<MetadataField, MetadataFieldDto>> Projection => x => new MetadataFieldDto(
        x.Id, x.PublicId, Db.Set<MetadataSchema>().Where(s => s.Id == x.SchemaId).Select(s => s.PublicId).FirstOrDefault(),
        Db.Set<MetadataSchema>().Where(s => s.Id == x.SchemaId).Select(s => s.ShortId).FirstOrDefault() ?? "", x.Field, x.Subfield, x.DescriptionVn, x.DescriptionEn,
        x.SortOrder, x.Input, x.ExportField, x.Status, x.CreatedAt, x.UpdatedAt);

    protected override MetadataField Create(MetadataFieldRequest request) => throw new NotSupportedException("Tạo qua CreateAsync (cần tra SchemaId).");

    protected override async Task<MetadataField> CreateAsync(MetadataFieldRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MetadataField.Create(await ResolveSchemaAsync(request.SchemaPublicId, ct), ToFields(request));
    }

    protected override void Update(MetadataField entity, MetadataFieldRequest request) => entity.Update(ToFields(request));

    protected override async Task ValidateAsync(MetadataField entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.SchemaId == entity.SchemaId && x.Field == entity.Field && x.Subfield == entity.Subfield && x.Id != entity.Id, ct))
            throw new ConflictException("FIELD_EXISTS", $"Trường '{entity.Label}' đã có trong lược đồ.");
    }

    protected override async Task EnsureDeletableAsync(MetadataField entity, CancellationToken ct)
    {
        if (await Db.Set<EbookMetadataValue>().AnyAsync(v => v.FieldId == entity.Id, ct))
            throw new ConflictException("FIELD_IN_USE", $"Trường '{entity.Label}' đã có dữ liệu — ẩn trường thay vì xoá.");
    }

    protected override IQueryable<MetadataField> Filter(IQueryable<MetadataField> query, MetadataFieldSearch search)
    {
        if (search.SchemaPublicId is { } schemaPublicId)
        {
            var schemaId = Db.Set<MetadataSchema>().Where(s => s.PublicId == schemaPublicId).Select(s => s.Id).FirstOrDefault();
            query = query.Where(x => x.SchemaId == schemaId);
        }
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Field.ToLower().Contains(term) || x.DescriptionVn.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<MetadataField> Order(IQueryable<MetadataField> query) => query.OrderBy(x => x.SchemaId).ThenBy(x => x.SortOrder).ThenBy(x => x.Id);

    private async Task<long> ResolveSchemaAsync(Guid schemaPublicId, CancellationToken ct) =>
        await Db.Set<MetadataSchema>().Where(s => s.PublicId == schemaPublicId).Select(s => s.Id).FirstOrDefaultAsync(ct) is var id and > 0
            ? id : throw new NotFoundException("Lược đồ metadata", schemaPublicId);

    private static MetadataFieldFields ToFields(MetadataFieldRequest r) =>
        new(r.Field, r.Subfield, r.DescriptionVn, r.DescriptionEn, r.SortOrder, r.Input, r.ExportField, r.Status);
}

public sealed record EbookMetadataEntry(Guid FieldPublicId, string Value, string? Language = null);

public sealed record EbookMetadataSave(IReadOnlyList<EbookMetadataEntry> Values);

public sealed record EbookMetadataRow(Guid FieldPublicId, string Field, string? Subfield, string DescriptionVn, string? DescriptionEn, string Input, string Value, string? Language);

/// <summary>
/// Giá trị metadata của một tài liệu số (LRC: MetaDataValueController). Đặt cả danh sách một lần (thay thế) — khuôn của form sửa tài liệu,
/// không có sửa lẻ từng dòng. Chỉ nhận trường đang hoạt động; giá trị kiểm theo kiểu nhập của trường.
/// </summary>
public sealed class EbookMetadata(ICrudDbContext db, TimeProvider clock, EbookStatePublisher states)
{
    private const int MaxValues = 300;

    public async Task<IReadOnlyList<EbookMetadataRow>> GetAsync(Guid itemPublicId, CancellationToken ct)
    {
        var itemId = await ItemIdAsync(itemPublicId, ct);
        return await RowsAsync(itemId, ct);
    }

    public async Task<IReadOnlyList<EbookMetadataRow>> ReplaceAsync(Guid itemPublicId, EbookMetadataSave save, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Values.Count > MaxValues) throw new BusinessRuleException("METADATA_TOO_MANY", $"Tối đa {MaxValues} giá trị metadata cho một tài liệu.");
        var item = await db.Set<EbookItem>().FirstOrDefaultAsync(x => x.PublicId == itemPublicId, ct) ?? throw new NotFoundException("Tài liệu số", itemPublicId);

        var fieldIds = save.Values.Select(v => v.FieldPublicId).Distinct().ToList();
        var fields = await db.Set<MetadataField>().Where(f => fieldIds.Contains(f.PublicId)).ToDictionaryAsync(f => f.PublicId, ct);
        var order = new Dictionary<Guid, int>();
        var created = new List<EbookMetadataValue>();
        foreach (var entry in save.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.Value)) continue; // dòng trống = bỏ
            if (!fields.TryGetValue(entry.FieldPublicId, out var field)) throw new NotFoundException("Trường metadata", entry.FieldPublicId);
            if (field.Status != IHasStatus.Active) throw new BusinessRuleException("FIELD_INACTIVE", $"Trường '{field.Label}' đang ẩn.");
            order[field.PublicId] = order.GetValueOrDefault(field.PublicId) + 1;
            created.Add(EbookMetadataValue.Create(item.Id, field, entry.Value, entry.Language, order[field.PublicId]));
        }

        // Chỉ thay giá trị của trường đang hoạt động; giá trị của trường đã ẩn giữ nguyên (ẩn trường không làm mất dữ liệu đã nhập).
        var removed = await db.Set<EbookMetadataValue>().Where(v => v.EbookItemId == item.Id && db.Set<MetadataField>().Any(f => f.Id == v.FieldId && f.Status == IHasStatus.Active)).ToListAsync(ct);
        db.Set<EbookMetadataValue>().RemoveRange(removed);
        db.Set<EbookMetadataValue>().AddRange(created);
        item.Touch(clock.GetUtcNow());
        await states.PublishAsync(item, false, ct, metadata: created.Select(v => v.Value).ToList());
        await db.SaveChangesAsync(ct);
        return await RowsAsync(item.Id, ct);
    }

    private async Task<long> ItemIdAsync(Guid itemPublicId, CancellationToken ct) =>
        await db.Set<EbookItem>().Where(x => x.PublicId == itemPublicId).Select(x => x.Id).FirstOrDefaultAsync(ct) is var id and > 0
            ? id : throw new NotFoundException("Tài liệu số", itemPublicId);

    private async Task<IReadOnlyList<EbookMetadataRow>> RowsAsync(long itemId, CancellationToken ct) =>
        await db.Set<EbookMetadataValue>().AsNoTracking().Where(v => v.EbookItemId == itemId)
            .Join(db.Set<MetadataField>().AsNoTracking().Where(f => f.Status == IHasStatus.Active), v => v.FieldId, f => f.Id, (v, f) => new { v, f })
            .OrderBy(x => x.f.SortOrder).ThenBy(x => x.f.Id).ThenBy(x => x.v.SortOrder)
            .Select(x => new EbookMetadataRow(x.f.PublicId, x.f.Field, x.f.Subfield, x.f.DescriptionVn, x.f.DescriptionEn, x.f.Input, x.v.Value, x.v.Language))
            .ToListAsync(ct);
}
