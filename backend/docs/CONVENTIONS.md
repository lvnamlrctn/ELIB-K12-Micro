# Quy ước & Coding Style

## Đặt tên

### Namespace
```
ELIBAPI.Core.*
ELIBAPI.Infrastructure.*
ELIBAPI.API.*              ← Internal API
ELIBAPI.API.Public.*       ← Public API
```

**Namespace chi tiết theo schema:**

| Loại | Namespace |
|------|-----------|
| Entity schema `cms` | `ELIBAPI.Core.Entities.Cms` |
| Entity schema `dbo` | `ELIBAPI.Core.Entities.Dbo` |
| Entity schema `Ebook` | `ELIBAPI.Core.Entities.Ebook` |
| Entity schema `EOffice` | `ELIBAPI.Core.Entities.EOffice` |
| Entity schema `Evaluate` | `ELIBAPI.Core.Entities.Evaluate` |
| Entity schema `PrintBook` | `ELIBAPI.Core.Entities.PrintBook` |
| Internal Controller (cms) | `ELIBAPI.API.Controllers.Cms` |
| Internal Controller (dbo) | `ELIBAPI.API.Controllers.Dbo` |
| Internal Controller (Ebook) | `ELIBAPI.API.Controllers.Ebook` |
| Internal Controller (EOffice) | `ELIBAPI.API.Controllers.EOffice` |
| Internal Controller (Evaluate) | `ELIBAPI.API.Controllers.Evaluate` |
| Internal Controller (PrintBook) | `ELIBAPI.API.Controllers.PrintBook` |
| Public Controller (cms) | `ELIBAPI.API.Public.Controllers.Cms` |
| `AuthController` (đặc biệt) | `ELIBAPI.API.Controllers` |

### Entity class
- Tên class = tên bảng (PascalCase), ví dụ: `PhotoAlbum`, `MenuType`
- Ngoại lệ: bảng `news` → class `News`
- Thuộc tính giữ nguyên tên cột database, ví dụ: `Can_Add`, `IsDelete`, `CreatedRowBy`
- **File đặt theo schema subfolder**: `Entities/{Schema}/{Entity}.cs`
  - Schema `cms` → `Entities/Cms/{Entity}.cs`, namespace `ELIBAPI.Core.Entities.Cms`
  - Schema `dbo` → `Entities/Dbo/{Entity}.cs`, namespace `ELIBAPI.Core.Entities.Dbo`
  - Schema `Ebook` → `Entities/Ebook/{Entity}.cs`, namespace `ELIBAPI.Core.Entities.Ebook`
  - Schema `EOffice` → `Entities/EOffice/{Entity}.cs`, namespace `ELIBAPI.Core.Entities.EOffice`
  - Schema `Evaluate` → `Entities/Evaluate/{Entity}.cs`, namespace `ELIBAPI.Core.Entities.Evaluate`
  - Schema `PrintBook` → `Entities/PrintBook/{Entity}.cs`, namespace `ELIBAPI.Core.Entities.PrintBook`

### DTO — Internal (`ELIBAPI.Core/DTOs/`)
- Request: `{Entity}Request` → `NewsRequest`, `CategoryRequest`
- Search request: `{Entity}SearchRequest` → `NewsSearchRequest` (kế thừa `SearchRequest`)
- **KHÔNG** khai báo `DepartmentId` trong request body — server tự lấy từ JWT claim

### DTO — Public (`ELIBAPI.API.Public/DTOs/`)
- Search request: `Public{Entity}SearchRequest` → `PublicNewsSearchRequest` (kế thừa `PublicSearchRequest`)
- Response: `Public{Entity}Response` → `PublicNewsResponse`
- **CÓ** `DepartmentId` trong `PublicSearchRequest` — client tự truyền (optional)
- **KHÔNG** có `DepartmentId`, audit trail, `PublicId` trong `Public{Entity}Response`

### Repository (Internal)
- Pattern: `{Entity}Repository` → `NewsRepository`
- Kế thừa `BaseRepository<TEntity, TSearch, TRequest>`
- **KHÔNG** tự xử lý DepartmentId — `BaseRepository` lo

### Controller — Internal
- Pattern: `{Entity}Controller` → `NewsController`
- Kế thừa `GenericController<TEntity, TSearch, TRequest>`
- Route: `[Route("api/[controller]")]`
- **File đặt theo schema subfolder**: `Controllers/{Schema}/{Entity}Controller.cs`, namespace `ELIBAPI.API.Controllers.{Schema}`
  - Ví dụ: `Controllers/Cms/NewsController.cs`, `Controllers/Ebook/IntroBookCategoryController.cs`
- Ngoại lệ: `AuthController` không theo schema — `Controllers/AuthController.cs`, namespace `ELIBAPI.API.Controllers`

### Controller — Public
- Pattern: `Public{Entity}Controller` → `PublicNewsController`
- Kế thừa `PublicBaseController`
- Route: `[Route("api/public/[controller]")]` — prefix `public` phân biệt với Internal
- **File đặt theo schema subfolder**: `Controllers/{Schema}/Public{Entity}Controller.cs`, namespace `ELIBAPI.API.Public.Controllers.{Schema}`
  - Ví dụ: `Controllers/Cms/PublicNewsController.cs`, `Controllers/Ebook/PublicIntroBookCategoryController.cs`
- **KHÔNG** dùng `GenericController`, **KHÔNG** dùng `BaseRepository`
- **KHÔNG** inject `ELIBAPIDbContext` trực tiếp — inject `IPublicGenericRepository<TEntity, TSearch>`
- Chỉ chứa logic mapping Entity → `PublicXxxResponse`; gọi `_repo.SearchAsync` / `_repo.SearchAllAsync`

### Interface
- Pattern: `I{Name}` → `IAuthService`, `IPermissionService`
- Repository dùng generic: `IGenericRepository<T, TSearch, TRequest>`

---

## Cấu trúc Response — LUÔN dùng ApiResponse<T>

```csharp
// Thành công
return Ok(ApiResponse<T>.Ok(data));
return Ok(ApiResponse<T>.Ok(data, "Thêm mới thành công."));

// Lỗi
return NotFound(ApiResponse<T>.Fail("Không tìm thấy dữ liệu.", 404));
return BadRequest(ApiResponse<T>.Fail("Dữ liệu không hợp lệ."));
```

**KHÔNG** trả về trực tiếp object, string hay anonymous object — phải wrap trong `ApiResponse<T>`.

---

## Soft Delete — LUÔN dùng IsDelete (Internal)

```csharp
// ĐÚng
protected override void SoftDelete(News e, long userId)
{
    e.IsDelete = 2;
    e.UpdateRowBy = userId;
    e.UpdatedRowDate = DateTime.Now;
}

// SAI — không được xoá thật
_context.News.Remove(entity);
```

---

## Audit Trail — LUÔN gán trong MapRequestToEntity (Internal)

> Section này mô tả Audit Trail của entity (`CreatedRowBy`, `UpdatedRowDate`…) — khác với `dbo.UserLog` (ghi log hành động người dùng do `BaseRepository` Internal tự ghi). Public API không ghi UserLog.

```csharp
protected override void MapRequestToEntity(NewsRequest r, News e, long userId, bool isNew)
{
    // ... gán các trường nghiệp vụ ...
    e.UpdateRowBy = userId;
    e.UpdatedRowDate = DateTime.Now;
    if (isNew)
    {
        e.CreatedRowBy = userId;
        e.CreatedRowDate = DateTime.Now;
        // DepartmentId sẽ được BaseRepository.ApplyDepartmentOnAdd() gán sau
    }
}
```

---

## UserLog — Ghi nhật ký thao tác

```csharp
// ĐÚNG — Internal API, tự động, không cần code thêm
// BaseRepository.AddAsync() → SaveChanges() → ghi UserLog

// SAI — tự ghi UserLog từ controller hay concrete repository
_context.UserLogs.Add(new UserLog { ... });
```

- Internal: ghi tự động cho Add, Update, Delete, ChangeStatus
- Không ghi cho: GetByPublicId, Search, SearchAll
- **Public API**: không ghi UserLog
- `PortalId` lấy tự động từ entity bằng reflection — `null` nếu entity không có trường `PortalId`

---

## DepartmentId — Khác nhau giữa Internal và Public

### Internal (`ELIBAPI.API`) — Tự động từ JWT, KHÔNG xử lý thủ công

```csharp
// ĐÚng — BuildQuery không tự filter DepartmentId
protected override IQueryable<News> BuildQuery(NewsSearchRequest r)
{
    var q = _dbSet.Where(x => x.IsDelete != 2);
    if (!string.IsNullOrEmpty(r.Keyword)) q = q.Where(x => x.Title!.Contains(r.Keyword));
    return q.OrderByDescending(x => x.Id);
    // BaseRepository.ApplyDepartmentFilter() tự xử lý DepartmentId
}

// SAI — tự filter DepartmentId
q = q.Where(x => x.DepartmentId == someDeptId);
```

### Public (`ELIBAPI.API.Public`) — Client truyền, optional; logic nằm trong Repository

```csharp
// ĐÚng — BuildQuery trong PublicNewsRepository (kế thừa PublicBaseRepository)
protected override IQueryable<News> BuildQuery(PublicNewsSearchRequest r)
{
    var q = _db.News.Where(x => x.IsDelete != 2 && x.Status == 2); // Status = 2 bắt buộc
    if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Title!.Contains(r.Keyword));
    if (r.DepartmentId.HasValue)           q = q.Where(x => x.DepartmentId == r.DepartmentId);
    return q.OrderByDescending(x => x.Id);
}

// SAI — filter trong controller thay vì repository
// PublicNewsController KHÔNG được tự xây BuildQuery
```

---

## Status filter

```csharp
// Internal API — KHÔNG bắt buộc filter Status; trả cả bản ghi chưa xuất bản để quản trị
protected override IQueryable<News> BuildQuery(NewsSearchRequest r)
{
    var q = _dbSet.Where(x => x.IsDelete != 2);
    // Status filter chỉ thêm nếu request có truyền
    if (r.Status.HasValue) q = q.Where(x => x.Status == r.Status);
    return q.OrderByDescending(x => x.Id);
}

// Public API — LUÔN có Status = 2; enforce tại PublicBaseRepository.BuildQuery
// SAI nếu bỏ Status = 2:
var q = _db.News.Where(x => x.IsDelete != 2);               // thiếu Status = 2

// ĐÚNG:
var q = _db.News.Where(x => x.IsDelete != 2 && x.Status == 2);
```

---

## PublicResponse — Không trả trường nhạy cảm

```csharp
// ĐÚng — chỉ map trường an toàn
private static PublicNewsResponse ToResponse(News x) => new()
{
    Id    = x.Id,
    Title = x.Title,
    // ...
    // Không map: IsDelete, CreatedRowBy, UpdateRowBy, CreatedRowDate,
    //            UpdatedRowDate, PublicId, DepartmentId
};

// SAI — trả về entity trực tiếp
return Ok(ApiResponse<News>.Ok(entity));
```

---

## Query — LUÔN lọc IsDelete

```csharp
// ĐÚng
var q = _dbSet.Where(x => x.IsDelete != 2);        // Internal
var q = _db.News.Where(x => x.IsDelete != 2 && x.Status == 2); // Public

// SAI
var q = _dbSet.AsQueryable();
```

---

## Permission Attribute — CHỈ dùng trong Internal, KHÔNG dùng trong Public

```csharp
// Internal — bắt buộc gắn Permission vào mutating actions
[HttpPost("Add")]
[Permission("NEWS", "add")]
public new async Task<IActionResult> Add([FromBody] NewsRequest request) => await base.Add(request);

// Public — chỉ [AllowAnonymous] từ PublicBaseController, không gắn thêm gì
[HttpPost("Search")]
public async Task<IActionResult> Search([FromBody] PublicNewsSearchRequest r) { ... }
```

---

## Cache — IMemoryCache

```csharp
// ĐÚNG — CHỈ Public API dùng cache; truyền IMemoryCache lên PublicBaseRepository
public PublicNewsRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

// SAI — Internal API không dùng cache
public NewsRepository(ELIBAPIDbContext ctx, IMemoryCache cache) : base(ctx, cache) { }

// SAI — không tự gọi cache trong concrete repository hay controller
_cache.Set("...", data);
```

- Cache CHỈ trong `PublicBaseRepository` — `BaseRepository` (Internal) **không cache**
- Concrete public repository truyền `IMemoryCache` lên constructor — không tự thao tác cache
- Debug: cache key format `"{EntityName}:{Method}:{hash}"` — restart process để xóa toàn bộ

---

## Giá trị Status thông dụng

```
Status = 2   → Xuất bản / Hoạt động  (Public API chỉ trả Status = 2)
Status = 1   → Ẩn / Chưa xuất bản
IsDelete = 2           → Đã xoá mềm
IsDelete = 1 hoặc null → Chưa xoá
```

---

## Ngôn ngữ trong code & message

- **Code**: Tiếng Anh (tên biến, class, method, comment)
- **Message gốc (base language)**: Tiếng Anh — dùng `IStringLocalizer`
- **Mặc định**: English — khi client không gửi `Accept-Language` header, mọi message trả về bằng tiếng Anh
- **Đa ngữ**: ASP.NET Core Localization — client gửi `Accept-Language: vi-VN` để nhận tiếng Việt

```
Không có Accept-Language  →  English (default)
Accept-Language: en        →  English
Accept-Language: vi-VN     →  Tiếng Việt
```

| Resource key | English (default) | Tiếng Việt (vi-VN) |
|---|---|---|
| `AddSuccess` | `"Added successfully."` | `"Thêm mới thành công."` |
| `UpdateSuccess` | `"Updated successfully."` | `"Cập nhật thành công."` |
| `DeleteSuccess` | `"Deleted successfully."` | `"Xoá thành công."` |
| `ChangeStatusSuccess` | `"Status updated successfully."` | `"Cập nhật trạng thái thành công."` |
| `NotFound` | `"Data not found."` | `"Không tìm thấy dữ liệu."` |
| `Success` | `"Success."` | `"Thành công."` |
| `Forbidden` | `"You do not have permission to perform this action."` | `"Bạn không có quyền thực hiện thao tác này."` |
| `ForbiddenDepartment` | `"You do not have permission to modify data from another department."` | `"Bạn không có quyền chỉnh sửa dữ liệu của phòng ban khác."` |
| `Unauthorized` | `"You are not logged in or your token has expired."` | `"Bạn chưa đăng nhập hoặc token đã hết hạn."` |

---

## Checklist thêm bảng mới

### Internal (ELIBAPI.API)
- [ ] Tạo `ELIBAPI.Core/Entities/{Schema}/{Entity}.cs` — namespace `ELIBAPI.Core.Entities.{Schema}`, có cột `DepartmentId`
- [ ] Thêm `DbSet<T>` + `ToTable(...)` + `PublicId` default vào `ELIBAPIDbContext`
- [ ] Tạo Request DTO trong `Requests.cs` — **KHÔNG** có `DepartmentId`
- [ ] Tạo Repository — **KHÔNG** tự xử lý DepartmentId
- [ ] Đăng ký DI trong `DependencyInjection.cs`
- [ ] Tạo `ELIBAPI.API/Controllers/{Schema}/{Entity}Controller.cs` — namespace `ELIBAPI.API.Controllers.{Schema}`, dùng `ModuleCode` string
- [ ] Controller có action GetById: `[HttpGet("GetById/{publicId}")]` + không có `[Permission]`

### Public (ELIBAPI.API.Public)
- [ ] Tạo `Public{Entity}SearchRequest` trong `PublicRequests.cs` — **CÓ** `DepartmentId`
- [ ] Tạo `Public{Entity}Response` trong `PublicResponses.cs` — **KHÔNG** có audit trail
- [ ] Tạo `Public{Entity}Repository.cs` kế thừa `PublicBaseRepository<TEntity, TSearch>`
  - [ ] `BuildQuery`: `IsDelete != 2 && Status == 2` bắt buộc, filter `DepartmentId` nếu client truyền
  - [ ] Constructor nhận `IMemoryCache` và truyền lên `PublicBaseRepository` — cache tự động
  - [ ] Đăng ký DI `IPublicGenericRepository<TEntity, TSearch>` trong `DependencyInjection.cs`
- [ ] Tạo `ELIBAPI.API.Public/Controllers/{Schema}/Public{Entity}Controller.cs` — namespace `ELIBAPI.API.Public.Controllers.{Schema}`
  - [ ] Kế thừa `PublicBaseController`
  - [ ] Inject `IPublicGenericRepository<TEntity, TSearch>` — **KHÔNG** inject `ELIBAPIDbContext`
  - [ ] `ToResponse()`: map an toàn sang `Public{Entity}Response`
  - [ ] `POST Search` + `POST SearchAll`
