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

    /// <summary>Tạo entity từ request. Override khi cần tra DB (ví dụ nút cha của cây) — <see cref="Create"/> khi đó không được gọi.</summary>
    protected virtual Task<TEntity> CreateAsync(TRequest request, CancellationToken ct) => Task.FromResult(Create(request));

    public virtual async Task<TDto> AddAsync(TRequest request, CancellationToken ct)
    {
        var entity = await CreateAsync(request, ct);
        await ValidateAsync(entity, ct);
        Set.Add(entity);
        await OnSavingAsync(entity, CrudChange.Added, ct);
        await AuditAsync(entity, CrudChange.Added, ct);
        await Db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>Áp request vào entity khi cập nhật. Override khi cần tra DB — <see cref="Update"/> khi đó không được gọi.</summary>
    protected virtual Task UpdateAsync(TEntity entity, TRequest request, CancellationToken ct)
    {
        Update(entity, request);
        return Task.CompletedTask;
    }

    public virtual async Task<TDto> UpdateAsync(Guid publicId, TRequest request, CancellationToken ct)
    {
        var entity = await LoadAsync(publicId, ct);
        await UpdateAsync(entity, request, ct);
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

    /// <summary>File mẫu .xlsx của danh mục (chỉ khi resource cài <see cref="ICrudImportable{TRequest}"/>).</summary>
    public byte[] ImportTemplate() => CrudExcel.Template(EntityName, Importable.ImportColumns);

    public string ImportTemplateName => $"Mau nhap {EntityName}.xlsx";

    /// <summary>
    /// Nhập từ Excel (monolith: action Import). Mỗi dòng đi đúng đường Thêm mới (tạo entity → ValidateAsync → OnSavingAsync) trong
    /// MỘT transaction, lưu từng dòng để dòng sau thấy dòng trước (trùng tên trong cùng file cũng bị bắt).
    /// Có dòng lỗi → huỷ cả lần nhập, trả danh sách lỗi theo số dòng. <paramref name="skipDuplicates"/>: dòng trùng (409) bị bỏ qua thay vì lỗi.
    /// </summary>
    public async Task<CrudImportResult> ImportAsync(Stream file, bool skipDuplicates, CancellationToken ct)
    {
        var importable = Importable;
        var rows = CrudExcel.Read(file, importable.ImportColumns);
        var strategy = Db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Db.Database.BeginTransactionAsync(token);
            var errors = new List<CrudImportError>();
            int imported = 0, skipped = 0;
            foreach (var row in rows)
            {
                TEntity? entity = null;
                try
                {
                    entity = await CreateAsync(importable.MapImportRow(row), token);
                    await ValidateAsync(entity, token);
                    Set.Add(entity);
                    await OnSavingAsync(entity, CrudChange.Added, token);
                    await Db.SaveChangesAsync(token);
                    imported++;
                }
                catch (BusinessRuleException ex)
                {
                    if (entity is not null) Set.Entry(entity).State = EntityState.Detached;
                    if (skipDuplicates && ex is ConflictException) skipped++;
                    else if (errors.Count < MaxImportErrors) errors.Add(new CrudImportError(row.Number, ex.Message));
                    else break;
                }
            }

            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(token);
                return new CrudImportResult(0, 0, errors);
            }
            if (imported > 0 && AuditSink is not null)
            {
                await AuditSink.RecordAsync(
                    new CrudAuditEntry(typeof(TEntity).Name, EntityName, Guid.Empty, CrudChange.Imported,
                        $"{imported} bản ghi" + (skipped > 0 ? $", bỏ qua {skipped} dòng đã có" : "")), token);
                await Db.SaveChangesAsync(token);
            }
            await transaction.CommitAsync(token);
            return new CrudImportResult(imported, skipped, []);
        }, ct);
    }

    /// <summary>Báo tối đa chừng này dòng lỗi — file sai hàng loạt thì người dùng sửa theo mẫu rồi nhập lại.</summary>
    public const int MaxImportErrors = 200;

    private ICrudImportable<TRequest> Importable => this as ICrudImportable<TRequest>
        ?? throw new NotSupportedException($"{EntityName} không hỗ trợ nhập từ Excel.");

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

        if (typeof(ICrudImportable<TRequest>).IsAssignableFrom(typeof(TSelf)))
        {
            var add = new PermissionAttribute(permissionModule, "add");
            group.MapGet("/ImportTemplate", ([FromServices] TSelf r) => Results.File(r.ImportTemplate(), CrudExcel.ContentType, r.ImportTemplateName))
                .RequireAuthorization(add);

            // multipart/form-data, trường "file". API xác thực bằng Bearer token (không cookie) → không cần antiforgery.
            group.MapPost("/Import", async ([FromServices] TSelf r, IFormFile file, bool? skipDuplicates, CancellationToken ct) =>
            {
                if (file.Length is 0 or > CrudExcel.MaxFileBytes)
                    throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {CrudExcel.MaxFileBytes / 1024 / 1024} MB.");
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, ct);
                buffer.Position = 0;
                var result = await r.ImportAsync(buffer, skipDuplicates ?? false, ct);
                return result.Errors.Count > 0 ? Results.Json(result, statusCode: StatusCodes.Status400BadRequest) : Results.Ok(result);
            }).RequireAuthorization(add).DisableAntiforgery();
        }
    }
}
