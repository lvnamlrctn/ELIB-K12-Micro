# Pattern Tạo API Mới

## Tổng quan

Mỗi bảng trong hệ thống được phục vụ bởi **7 endpoint** theo pattern chuẩn. Để thêm bảng mới, thực hiện theo đúng 6 bước dưới đây theo thứ tự.

---

## 7 Endpoint chuẩn cho mỗi bảng

| Method | URL | Mô tả | Yêu cầu quyền |
|--------|-----|-------|---------------|
| `GET` | `/api/{Table}/{id}` | Lấy bản ghi theo Id (legacy — dùng PK `long`) | Đăng nhập |
| `GET` | `/api/{Table}/GetById/{publicId}` | Lấy bản ghi theo PublicId (`Guid`) | Đăng nhập |
| `POST` | `/api/{Table}/SearchAll` | Lấy tất cả theo điều kiện (không phân trang) | Đăng nhập |
| `POST` | `/api/{Table}/Search` | Tìm kiếm có phân trang | Đăng nhập |
| `POST` | `/api/{Table}/Add` | Thêm mới | `Can_Add = 1` |
| `PUT` | `/api/{Table}/Update/{publicId}` | Cập nhật (dùng PublicId) | `Can_Edit = 1` |
| `DELETE` | `/api/{Table}/Delete/{publicId}` | Xoá mềm (dùng PublicId) | `Can_Delete = 1` |
| `PUT` | `/api/{Table}/ChangeStatus` | Thay đổi trạng thái | `Can_Edit = 1` |

> `GET /{id}` (PK long) là legacy — khuyến nghị dùng `GET /GetById/{publicId}` cho các entity mới.

---

## Ví dụ đầy đủ: Thêm bảng `Document`

### Bước 1 — Entity (`ELIBAPI.Core/Entities/Cms/Document.cs`)

```csharp
namespace ELIBAPI.Core.Entities.Cms;

[Table("Document", Schema = "cms")]
public class Document
{
    [Key] public long Id { get; set; }
    public string? Title { get; set; }
    public string? FileUrl { get; set; }
    public long? CategoryId { get; set; }
    public int? Status { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    // Audit Trail — LUÔN có
    public int? IsDelete { get; set; }
    public long? CreatedRowBy { get; set; }
    public long? UpdateRowBy { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long? DepartmentId { get; set; }
    public Guid PublicId { get; set; }
}
```

### Bước 2 — DbContext (`ELIBAPI.Infrastructure/Data/ELIBAPIDbContext.cs`)

Thêm vào class `ELIBAPIDbContext`:

```csharp
// Thêm DbSet
public DbSet<Document> Documents { get; set; }

// Trong OnModelCreating()
modelBuilder.Entity<Document>().ToTable("Document", "cms");
modelBuilder.Entity<Document>().Property(e => e.PublicId).HasDefaultValueSql("newsequentialid()");
```

### Bước 3 — DTOs (`ELIBAPI.Core/DTOs/Request/Requests.cs`)

```csharp
// ==================== DOCUMENT ====================
public class DocumentRequest
{
    public string? Title { get; set; }
    public string? FileUrl { get; set; }
    public long? CategoryId { get; set; }
    public int? Status { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    // KHÔNG khai báo DepartmentId — BaseRepository tự lấy từ JWT claim
}

public class DocumentSearchRequest : SearchRequest
{
    public long? CategoryId { get; set; }
    public int? Status { get; set; }
}
```

### Bước 4 — Repository (`ELIBAPI.Infrastructure/Repositories/Repositories.cs`)

```csharp
// ==================== DOCUMENT ====================
public class DocumentRepository : BaseRepository<Document, DocumentSearchRequest, DocumentRequest>
{
    public DocumentRepository(ELIBAPIDbContext ctx) : base(ctx) { }
    // UserLog được BaseRepository xử lý tự động sau Add/Update/Delete/ChangeStatus — không cần override

    protected override IQueryable<Document> BuildQuery(DocumentSearchRequest r)
    {
        // LUÔN bắt đầu bằng lọc IsDelete
        var q = _dbSet.Where(x => x.IsDelete != 2);

        // Lọc theo keyword
        if (!string.IsNullOrEmpty(r.Keyword))
            q = q.Where(x => x.Title!.Contains(r.Keyword));

        // Lọc theo các trường thêm
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.CategoryId.HasValue) q = q.Where(x => x.CategoryId == r.CategoryId);
        if (r.Status.HasValue) q = q.Where(x => x.Status == r.Status);

        return q.OrderByDescending(x => x.Id);
    }

    protected override void MapRequestToEntity(DocumentRequest r, Document e, long userId, bool isNew)
    {
        // Gán trường nghiệp vụ
        e.Title = r.Title;
        e.FileUrl = r.FileUrl;
        e.CategoryId = r.CategoryId;
        e.Status = r.Status;
        e.PortalId = r.PortalId;
        e.Language = r.Language;
        // KHÔNG gán DepartmentId ở đây — BaseRepository.ApplyDepartmentOnAdd() tự gán

        // Audit — LUÔN có
        e.UpdateRowBy = userId;
        e.UpdatedRowDate = DateTime.Now;
        if (isNew)
        {
            e.CreatedRowBy = userId;
            e.CreatedRowDate = DateTime.Now;
        }
    }

    protected override void SoftDelete(Document e, long userId)
    {
        e.IsDelete = 2;
        e.UpdateRowBy = userId;
        e.UpdatedRowDate = DateTime.Now;
    }

    protected override void SetStatus(Document e, int status, long userId)
    {
        e.Status = status;
        e.UpdateRowBy = userId;
        e.UpdatedRowDate = DateTime.Now;
    }
}
```

### Bước 5 — Đăng ký DI (`ELIBAPI.Infrastructure/DependencyInjection.cs`)

```csharp
services.AddScoped<
    IGenericRepository<Document, DocumentSearchRequest, DocumentRequest>,
    DocumentRepository>();
```

### Bước 6 — Controller (`ELIBAPI.API/Controllers/Cms/DocumentController.cs`)

Mỗi controller tạo file riêng, đặt trong subfolder theo schema. Không dùng `ModuleIds` hardcoded — dùng `ModuleCode` string từ `cms.Module.ModuleCode`.

```csharp
namespace ELIBAPI.API.Controllers.Cms;

[Route("api/[controller]")]
public class DocumentController : GenericController<Document, DocumentSearchRequest, DocumentRequest>
{
    public DocumentController(IGenericRepository<Document, DocumentSearchRequest, DocumentRequest> repo)
        : base(repo) { }

    [HttpPost("Add")]
    [Permission("DOCUMENT", "add")]   // ModuleCode từ cms.Module.ModuleCode
    public new async Task<IActionResult> Add([FromBody] DocumentRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId}")]
    [Permission("DOCUMENT", "edit")]
    public new async Task<IActionResult> Update(Guid publicId, [FromBody] DocumentRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId}")]
    [Permission("DOCUMENT", "delete")]
    public new async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("DOCUMENT", "edit")]
    public new async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("GetById/{publicId}")]
    // Không có [Permission] — chỉ cần đăng nhập (kế thừa [Authorize] từ BaseApiController)
    public async Task<IActionResult> GetById(Guid publicId) => await base.GetByPublicId(publicId);
}
```

---

## Tính năng tích hợp sẵn trong BaseRepository

`BaseRepository` cung cấp sẵn các tính năng sau — concrete repository **không cần code thêm**:

- **GetByPublicId**: query `WHERE PublicId == guid AND IsDelete != 2`; expose qua action `GetById` trong controller
- **UserLog**: ghi tự động vào `dbo.UserLog` sau Add/Update/Delete/ChangeStatus thành công; **không ghi** cho GetByPublicId/Search/SearchAll
- **Cache**: **không có ở Internal API** — cache nằm ở `PublicBaseRepository`

Override chỉ khi cần logic nghiệp vụ đặc thù (`BuildQuery`, `MapRequestToEntity`, `SoftDelete`, `SetStatus`).

---

## Khi cần thêm endpoint tùy chỉnh

Nếu một bảng cần endpoint vượt ngoài 7 endpoint chuẩn, thêm trực tiếp vào controller cụ thể:

```csharp
[HttpGet("GetByCategory/{categoryId}")]
public async Task<IActionResult> GetByCategory(long categoryId)
{
    // Inject repository hoặc DbContext và xử lý custom
    var items = await _repo.SearchAllAsync(new DocumentSearchRequest { CategoryId = categoryId });
    return Ok(ApiResponse<List<Document>>.Ok(items));
}
```

**Không** sửa `GenericController` hay `BaseRepository` cho logic riêng của một bảng — override ở concrete class.

---

## Checklist khi thêm bảng mới

```
[ ] Bước 1: Entity class trong Entities/{Schema}/{Entity}.cs — namespace ELIBAPI.Core.Entities.{Schema}
[ ] Bước 2: DbSet + ToTable + PublicId default trong ELIBAPIDbContext
[ ] Bước 3: Request DTO + SearchRequest trong Requests.cs
[ ] Bước 4: Repository (BuildQuery, MapRequestToEntity, SoftDelete với IsDelete=2, SetStatus)
[ ] Bước 5: Đăng ký DI trong DependencyInjection.cs
[ ] Bước 6: Controller trong Controllers/{Schema}/{Entity}Controller.cs — namespace ELIBAPI.API.Controllers.{Schema}, dùng ModuleCode string
[ ] Bước 6 (tiếp): Thêm action GetById với [HttpGet("GetById/{publicId}")] — không có [Permission]
[ ] Xác nhận ModuleCode khớp với cms.Module.ModuleCode trong database thực tế
```
