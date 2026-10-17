using System.Linq.Expressions;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.BuildingBlocks.Tenancy;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elib.Digital.Application;

/// <summary>Request của danh mục cây — ParentId/SortOrder/Status xử lý chung trong <see cref="TreeCatalogResource{TSelf,TEntity,TRequest,TDto}"/>.</summary>
public interface ITreeCatalogRequest
{
    long? ParentId { get; }
    int? SortOrder { get; }
    int? Status { get; }
}

public sealed class TreeCatalogSearch : CrudSearch
{
    public long? ParentId { get; set; }
}

/// <summary>
/// Danh mục dạng cây dùng chung bởi Collection/Subject/Topic (giữ cha/Level/SortOrder — xem <see cref="TreeCatalogItem"/>).
/// Lớp con chỉ khai báo tạo/áp trường riêng; cha, thứ tự, trạng thái, tránh vòng lặp, Level con cháu đều xử lý ở đây.
/// </summary>
public abstract class TreeCatalogResource<TSelf, TEntity, TRequest, TDto>(ICrudDbContext db)
    : CrudResource<TSelf, TEntity, TreeCatalogSearch, TRequest, TDto>(db)
    where TSelf : TreeCatalogResource<TSelf, TEntity, TRequest, TDto>
    where TEntity : TreeCatalogItem
    where TRequest : class, ITreeCatalogRequest
{
    /// <summary>Tạo entity với tên + trường riêng (chưa gắn cha/thứ tự/trạng thái — Resource tự làm sau).</summary>
    protected abstract TEntity NewEntity(TRequest request);

    /// <summary>Áp tên + trường riêng khi sửa (không gồm cha/thứ tự/trạng thái).</summary>
    protected abstract void ApplyFields(TEntity entity, TRequest request);

    protected sealed override TEntity Create(TRequest request) => throw new NotSupportedException();

    protected sealed override void Update(TEntity entity, TRequest request) => throw new NotSupportedException();

    protected override async Task<TEntity> CreateAsync(TRequest request, CancellationToken ct)
    {
        var parent = await ParentAsync(request.ParentId, ct);
        var entity = NewEntity(request);
        entity.AttachTo(parent);
        entity.Reorder(request.SortOrder ?? 0);
        if (request.Status is { } status) entity.ChangeStatus(status);
        return entity;
    }

    protected override async Task UpdateAsync(TEntity entity, TRequest request, CancellationToken ct)
    {
        ApplyFields(entity, request);
        entity.Reorder(request.SortOrder ?? entity.SortOrder);
        if (request.Status is { } status) entity.ChangeStatus(status);
        if (request.ParentId != entity.ParentId) await MoveCoreAsync(entity, request.ParentId, ct);
    }

    protected override IQueryable<TEntity> Filter(IQueryable<TEntity> query, TreeCatalogSearch search)
    {
        if (search.ParentId is { } parentId) query = query.Where(x => x.ParentId == parentId);
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311 // EF dịch ToLower() sang lower() của SQL
        return query.Where(x => x.Name.ToLower().Contains(term));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<TEntity> Order(IQueryable<TEntity> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(TEntity entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.ParentId == entity.ParentId && x.Name == entity.Name && x.Id != entity.Id, ct))
            throw new ConflictException("NAME_EXISTS", $"{EntityName} '{entity.Name}' đã có trong cùng mục cha.");
    }

    protected override async Task EnsureDeletableAsync(TEntity entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.ParentId == entity.Id, ct))
            throw new ConflictException("CATALOG_HAS_CHILDREN", $"{EntityName} '{entity.Name}' còn mục con, không xoá được.");
    }

    private async Task<TEntity?> ParentAsync(long? parentId, CancellationToken ct) =>
        parentId is { } id and > 0
            ? await Set.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"{EntityName} cha", id)
            : null;

    private async Task MoveCoreAsync(TEntity entity, long? newParentId, CancellationToken ct)
    {
        var parent = await ParentAsync(newParentId, ct);
        var descendants = await DescendantsAsync(entity.Id, ct);
        if (parent is not null && descendants.Any(d => d.Id == parent.Id))
            throw new BusinessRuleException("CATALOG_MOVE_CYCLE", $"Không chuyển {EntityName} vào chính nhánh con của nó.");
        var before = entity.Level;
        entity.AttachTo(parent);
        var delta = entity.Level - before;
        if (delta != 0)
            foreach (var d in descendants) d.ShiftLevel(delta);
    }

    /// <summary>Mọi con cháu (tracked). Cây nhỏ — tải một lần rồi duyệt trong bộ nhớ (như Tenant's OrgResource).</summary>
    private async Task<List<TEntity>> DescendantsAsync(long rootId, CancellationToken ct)
    {
        var all = await Set.Where(x => x.ParentId != null).ToListAsync(ct);
        var children = all.ToLookup(x => x.ParentId!.Value);
        var result = new List<TEntity>();
        var stack = new Stack<long>([rootId]);
        while (stack.Count > 0)
            foreach (var child in children[stack.Pop()])
            {
                result.Add(child);
                stack.Push(child.Id);
            }
        return result;
    }
}

public sealed record EbookCollectionRequest(
    string Name, long? ParentId = null, int? SortOrder = null, int? Status = null, string? Language = null, string? Link = null,
    bool AllowDownload = false, bool Share = false, string? CoverUrl = null) : ITreeCatalogRequest;

public sealed record EbookCollectionDto(
    long Id, Guid PublicId, string Name, long? ParentId, int Level, int SortOrder, int Status, string? Language, string? Link, bool AllowDownload,
    bool Share, string? CoverUrl, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Bộ sưu tập tài liệu số (LRC: EbookCollectionController, quyền DIGITAL_COLLECTIONS).</summary>
public sealed class EbookCollectionResource(ICrudDbContext db, ITenantContext tenant, IOptions<DigitalOptions> options)
    : TreeCatalogResource<EbookCollectionResource, EbookCollection, EbookCollectionRequest, EbookCollectionDto>(db)
{
    private readonly DigitalOptions _options = options.Value;

    protected override string EntityName => "Bộ sưu tập";

    protected override string? Describe(EbookCollection entity) => entity.Name;

    protected override Expression<Func<EbookCollection, EbookCollectionDto>> Projection => x => new EbookCollectionDto(
        x.Id, x.PublicId, x.Name, x.ParentId, x.Level, x.SortOrder, x.Status, x.Language, x.Link, x.AllowDownload, x.Share, x.CoverUrl,
        x.CreatedAt, x.UpdatedAt);

    protected override EbookCollection NewEntity(EbookCollectionRequest request) => EbookCollection.Create(
        request.Name, request.Language, request.Link, request.AllowDownload, request.Share, request.CoverUrl, _options.MediaPublicPrefix,
        tenant.RequireTenantId());

    protected override void ApplyFields(EbookCollection entity, EbookCollectionRequest request) => entity.Update(
        request.Name, request.Language, request.Link, request.AllowDownload, request.Share, request.CoverUrl, _options.MediaPublicPrefix,
        tenant.RequireTenantId());
}

public sealed record EbookSubjectRequest(
    string Name, long? ParentId = null, int? SortOrder = null, int? Status = null, string? Ddc = null, string? Language = null,
    string? Link = null) : ITreeCatalogRequest;

public sealed record EbookSubjectDto(
    long Id, Guid PublicId, string Name, long? ParentId, int Level, int SortOrder, int Status, string? Ddc, string? Language, string? Link,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Môn loại tài liệu số (LRC: EbookSubjectController, quyền DIGITAL_SUBJECTS).</summary>
public sealed class EbookSubjectResource(ICrudDbContext db)
    : TreeCatalogResource<EbookSubjectResource, EbookSubject, EbookSubjectRequest, EbookSubjectDto>(db)
{
    protected override string EntityName => "Môn loại";

    protected override string? Describe(EbookSubject entity) => entity.Name;

    protected override Expression<Func<EbookSubject, EbookSubjectDto>> Projection => x => new EbookSubjectDto(
        x.Id, x.PublicId, x.Name, x.ParentId, x.Level, x.SortOrder, x.Status, x.Ddc, x.Language, x.Link, x.CreatedAt, x.UpdatedAt);

    protected override EbookSubject NewEntity(EbookSubjectRequest request) =>
        EbookSubject.Create(request.Name, request.Ddc, request.Language, request.Link);

    protected override void ApplyFields(EbookSubject entity, EbookSubjectRequest request) =>
        entity.Update(request.Name, request.Ddc, request.Language, request.Link);
}

public sealed record EbookTopicRequest(
    string Name, long? ParentId = null, int? SortOrder = null, int? Status = null, bool RequiresLogin = false, string? Description = null,
    string? Keyword = null, string? PageTitle = null, string? MetaDescription = null, string? Language = null, string? Ddc = null)
    : ITreeCatalogRequest;

public sealed record EbookTopicDto(
    long Id, Guid PublicId, string Name, long? ParentId, int Level, int SortOrder, int Status, bool RequiresLogin, string? Description,
    string? Keyword, string? PageTitle, string? MetaDescription, string? Language, string? Ddc, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Chủ đề tài liệu số (LRC: EbookTopicController, quyền DIGITAL_TOPICS).</summary>
public sealed class EbookTopicResource(ICrudDbContext db) : TreeCatalogResource<EbookTopicResource, EbookTopic, EbookTopicRequest, EbookTopicDto>(db)
{
    protected override string EntityName => "Chủ đề";

    protected override string? Describe(EbookTopic entity) => entity.Name;

    protected override Expression<Func<EbookTopic, EbookTopicDto>> Projection => x => new EbookTopicDto(
        x.Id, x.PublicId, x.Name, x.ParentId, x.Level, x.SortOrder, x.Status, x.RequiresLogin, x.Description, x.Keyword, x.PageTitle,
        x.MetaDescription, x.Language, x.Ddc, x.CreatedAt, x.UpdatedAt);

    protected override EbookTopic NewEntity(EbookTopicRequest request) => EbookTopic.Create(
        request.Name, request.RequiresLogin, request.Description, request.Keyword, request.PageTitle, request.MetaDescription, request.Language,
        request.Ddc);

    protected override void ApplyFields(EbookTopic entity, EbookTopicRequest request) => entity.Update(
        request.Name, request.RequiresLogin, request.Description, request.Keyword, request.PageTitle, request.MetaDescription, request.Language,
        request.Ddc);
}

public sealed record EbookDigTypeRequest(string Code, string? DescriptionVn = null, string? DescriptionEn = null, int SortOrder = 0, string? Language = null);

public sealed record EbookDigTypeDto(
    long Id, Guid PublicId, string Code, string? DescriptionVn, string? DescriptionEn, int SortOrder, string? Language, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Loại tài liệu số (LRC: DigTypeController, quyền DIGITAL_DIG_TYPES) — danh mục phẳng, mã không trùng trong đơn vị.</summary>
public sealed class EbookDigTypeResource(ICrudDbContext db)
    : CrudResource<EbookDigTypeResource, EbookDigType, CrudSearch, EbookDigTypeRequest, EbookDigTypeDto>(db)
{
    protected override string EntityName => "Loại tài liệu số";

    protected override string? Describe(EbookDigType entity) => entity.Code;

    protected override Expression<Func<EbookDigType, EbookDigTypeDto>> Projection => x => new EbookDigTypeDto(
        x.Id, x.PublicId, x.Code, x.DescriptionVn, x.DescriptionEn, x.SortOrder, x.Language, x.CreatedAt, x.UpdatedAt);

    protected override EbookDigType Create(EbookDigTypeRequest request) =>
        EbookDigType.Create(request.Code, request.DescriptionVn, request.DescriptionEn, request.SortOrder, request.Language);

    protected override void Update(EbookDigType entity, EbookDigTypeRequest request) =>
        entity.Update(request.Code, request.DescriptionVn, request.DescriptionEn, request.SortOrder, request.Language);

    protected override IQueryable<EbookDigType> Filter(IQueryable<EbookDigType> query, CrudSearch search)
    {
        if (search.Term is not { } term) return query;
#pragma warning disable CA1862, CA1304, CA1311
        return query.Where(x => x.Code.ToLower().Contains(term) || (x.DescriptionVn != null && x.DescriptionVn.ToLower().Contains(term)));
#pragma warning restore CA1862, CA1304, CA1311
    }

    protected override IOrderedQueryable<EbookDigType> Order(IQueryable<EbookDigType> query) => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);

    protected override async Task ValidateAsync(EbookDigType entity, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Code == entity.Code && x.Id != entity.Id, ct))
            throw new ConflictException("CODE_EXISTS", $"Mã loại tài liệu số '{entity.Code}' đã có.");
    }
}
