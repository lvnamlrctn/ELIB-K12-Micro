using System.Linq.Expressions;
using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Elib.BuildingBlocks.Crud;

/// <summary>Kiểu tự map được 8 endpoint chuẩn — để <see cref="CrudEndpointExtensions.MapCrud{TResource}"/> gọi không cần liệt kê kiểu generic.</summary>
public interface ICrudEndpoints
{
    static abstract void MapEndpoints(RouteGroupBuilder group, string permissionModule);
}

/// <summary>
/// Danh mục CRUD chuẩn (thay <c>GenericController</c> + <c>BaseRepository</c> của monolith). Lớp con khai báo
/// tạo/cập nhật entity, lọc theo điều kiện và projection sang DTO; lọc đơn vị, xoá mềm, PublicId, cột audit do
/// Persistence lo. Đăng ký scoped trong DI (<see cref="CrudServiceCollectionExtensions.AddCrudResource{TResource}"/>).
/// </summary>
/// <typeparam name="TSelf">Chính lớp con (CRTP) — để endpoint lấy đúng resource từ DI.</typeparam>
public abstract class CrudResource<TSelf, TEntity, TSearch, TRequest, TDto>(ICrudDbContext db) : ICrudEndpoints, ICrudAuditable
    where TSelf : CrudResource<TSelf, TEntity, TSearch, TRequest, TDto>
    where TEntity : Entity, IHasPublicId
    where TSearch : CrudSearch
    where TRequest : class
{
    /// <summary>SearchAll không phân trang — chặn trên để một danh mục lỗi dữ liệu không kéo sập service.</summary>
    public const int SearchAllLimit = 5000;

    private Func<TEntity, TDto>? _toDto;

    protected ICrudDbContext Db { get; } = db;

    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    /// <summary>Nhật ký thao tác (AddCrudResource đặt từ DI; null = không ghi, ví dụ trong unit test).</summary>
    public ICrudAuditSink? AuditSink { get; set; }

    /// <summary>Nhãn của bản ghi trong nhật ký thao tác (tên, mã…). null = chỉ ghi loại danh mục.</summary>
    protected virtual string? Describe(TEntity entity) => null;

    /// <summary>Ghi nhật ký thao tác — gọi trước SaveChanges (event vào outbox cùng transaction). Override thao tác ghi thì gọi lại.</summary>
    protected Task AuditAsync(TEntity entity, CrudChange change, CancellationToken ct)
    {
        if (AuditSink is null) return Task.CompletedTask;
        if (entity.PublicId == Guid.Empty) entity.PublicId = Guid.CreateVersion7(); // bản ghi mới: interceptor chỉ gán khi còn trống
        return AuditSink.RecordAsync(new CrudAuditEntry(typeof(TEntity).Name, EntityName, entity.PublicId, change, Describe(entity)), ct);
    }

    /// <summary>Tên hiển thị trong thông báo "Không tìm thấy …".</summary>
    protected abstract string EntityName { get; }

    protected abstract Expression<Func<TEntity, TDto>> Projection { get; }

    protected abstract TEntity Create(TRequest request);

    protected abstract void Update(TEntity entity, TRequest request);

    /// <summary>Lọc theo điều kiện riêng (Keyword, trường thêm). Status đã được lọc sẵn nếu entity có <see cref="IHasStatus"/>.</summary>
    protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> query, TSearch search) => query;

    protected virtual IOrderedQueryable<TEntity> Order(IQueryable<TEntity> query) => query.OrderByDescending(e => e.Id);

    /// <summary>Kiểm tra trước khi lưu (trùng mã…). Entity mới có Id = 0.</summary>
    protected virtual Task ValidateAsync(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Kiểm tra trước khi xoá (còn bản ghi con, đang được tham chiếu…).</summary>
    protected virtual Task EnsureDeletableAsync(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Ngay trước SaveChanges — publish event ở đây để event vào outbox cùng transaction với dữ liệu.</summary>
    protected virtual Task OnSavingAsync(TEntity entity, CrudChange change, CancellationToken ct) => Task.CompletedTask;

    protected TDto ToDto(TEntity entity) => (_toDto ??= Projection.Compile())(entity);

    public IQueryable<TEntity> Query(TSearch search)
    {
        IQueryable<TEntity> query = Set.AsNoTracking();
        if (search.Status is > 0 && typeof(IHasStatus).IsAssignableFrom(typeof(TEntity)))
        {
            var status = search.Status.Value;
            query = query.Where(e => EF.Property<int>(e, nameof(IHasStatus.Status)) == status);
        }
        return Order(Filter(query, search));
    }

    public async Task<CrudPage<TDto>> SearchAsync(TSearch search, CancellationToken ct)
    {
        var page = Math.Max(1, search.PageIndex);
        var size = Math.Clamp(search.PageSize, 1, 500);
        var query = Query(search);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).Select(Projection).ToListAsync(ct);
        return new CrudPage<TDto>(items, total, page, size);
    }

    public async Task<IReadOnlyList<TDto>> SearchAllAsync(TSearch search, CancellationToken ct) =>
        await Query(search).Take(SearchAllLimit).Select(Projection).ToListAsync(ct);

    public async Task<TDto> GetAsync(long id, CancellationToken ct) =>
        await Set.AsNoTracking().Where(e => e.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(EntityName, id);

    public async Task<TDto> GetAsync(Guid publicId, CancellationToken ct) =>
        await Set.AsNoTracking().Where(e => e.PublicId == publicId).Select(Projection).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(EntityName, publicId);

    /// <summary>Override khi tạo entity cần tra DB (ví dụ nút cha của cây) — <see cref="Create"/> khi đó không được gọi.</summary>
    public virtual async Task<TDto> AddAsync(TRequest request, CancellationToken ct)
    {
        var entity = Create(request);
        await ValidateAsync(entity, ct);
        Set.Add(entity);
        await OnSavingAsync(entity, CrudChange.Added, ct);
        await AuditAsync(entity, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public virtual async Task<TDto> UpdateAsync(Guid publicId, TRequest request, CancellationToken ct)
    {
        var entity = await LoadAsync(publicId, ct);
        Update(entity, request);
        await ValidateAsync(entity, ct);
        await OnSavingAsync(entity, CrudChange.Updated, ct);
        await AuditAsync(entity, CrudChange.Updated, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid publicId, CancellationToken ct)
    {
        var entity = await LoadAsync(publicId, ct);
        await EnsureDeletableAsync(entity, ct);
        Set.Remove(entity); // interceptor chuyển thành xoá mềm
        await OnSavingAsync(entity, CrudChange.Deleted, ct);
        await AuditAsync(entity, CrudChange.Deleted, ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(ChangeStatusRequest request, CancellationToken ct)
    {
        var entity = await LoadAsync(request.PublicId, ct);
        ((IHasStatus)entity).ChangeStatus(request.Status);
        await OnSavingAsync(entity, CrudChange.StatusChanged, ct);
        await AuditAsync(entity, CrudChange.StatusChanged, ct);
        await Db.SaveChangesAsync(ct);
    }

    protected async Task<TEntity> LoadAsync(Guid publicId, CancellationToken ct) =>
        await Set.FirstOrDefaultAsync(e => e.PublicId == publicId, ct) ?? throw new NotFoundException(EntityName, publicId);

    /// <summary>
    /// 8 endpoint của <c>GenericController</c> (docs 07 §3), cùng đường dẫn. Quyền: view cho đọc, add/edit/delete cho ghi.
    /// ChangeStatus chỉ có khi entity có <see cref="IHasStatus"/>. Thêm mới trả 201, xoá/đổi trạng thái trả 204;
    /// lỗi trả problem+json (không bọc ApiResponse như monolith).
    /// </summary>
#pragma warning disable CA1000 // cài đặt static abstract của ICrudEndpoints — gọi qua MapCrud<TResource>, không gọi trực tiếp
    public static void MapEndpoints(RouteGroupBuilder group, string permissionModule)
#pragma warning restore CA1000
    {
        var view = new PermissionAttribute(permissionModule, "view");

        group.MapGet("/{id:long}", ([FromServices] TSelf r, long id, CancellationToken ct) => r.GetAsync(id, ct))
            .RequireAuthorization(view);

        group.MapGet("/GetById/{publicId:guid}", ([FromServices] TSelf r, Guid publicId, CancellationToken ct) => r.GetAsync(publicId, ct))
            .RequireAuthorization(view);

        group.MapPost("/Search", ([FromServices] TSelf r, TSearch search, CancellationToken ct) => r.SearchAsync(search, ct))
            .RequireAuthorization(view);

        group.MapPost("/SearchAll", ([FromServices] TSelf r, TSearch search, CancellationToken ct) => r.SearchAllAsync(search, ct))
            .RequireAuthorization(view);

        group.MapPost("/Add", async ([FromServices] TSelf r, TRequest request, CancellationToken ct) =>
        {
            var created = await r.AddAsync(request, ct);
            return Results.Created((string?)null, created);
        }).RequireAuthorization(new PermissionAttribute(permissionModule, "add"));

        group.MapPut("/Update/{publicId:guid}", ([FromServices] TSelf r, Guid publicId, TRequest request, CancellationToken ct)
            => r.UpdateAsync(publicId, request, ct))
            .RequireAuthorization(new PermissionAttribute(permissionModule, "edit"));

        group.MapDelete("/Delete/{publicId:guid}", async ([FromServices] TSelf r, Guid publicId, CancellationToken ct) =>
        {
            await r.DeleteAsync(publicId, ct);
            return Results.NoContent();
        }).RequireAuthorization(new PermissionAttribute(permissionModule, "delete"));

        if (typeof(IHasStatus).IsAssignableFrom(typeof(TEntity)))
        {
            group.MapPut("/ChangeStatus", async ([FromServices] TSelf r, ChangeStatusRequest request, CancellationToken ct) =>
            {
                await r.ChangeStatusAsync(request, ct);
                return Results.NoContent();
            }).RequireAuthorization(new PermissionAttribute(permissionModule, "edit"));
        }
    }
}
