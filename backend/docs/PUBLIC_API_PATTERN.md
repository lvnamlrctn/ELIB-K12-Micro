# Public API Pattern — ELIBAPI.API.Public

## Tổng quan

`ELIBAPI.API.Public` là project riêng trong cùng solution, cung cấp các endpoint **không yêu cầu đăng nhập** để client bên ngoài lấy dữ liệu công khai (News, Menu, Category, Photo).

- **Không cần JWT** — toàn bộ controller dùng `[AllowAnonymous]`
- **Chỉ 2 endpoint** mỗi bảng: `Search` (phân trang) và `SearchAll` (không phân trang)
- **DepartmentId do client truyền** — khác với API nội bộ (lấy từ JWT claim); nếu client không truyền thì không filter
- **Response riêng** (`PublicResponse`) — ẩn các trường audit trail (`CreatedRowBy`, `UpdateRowBy`, `CreatedRowDate`, `UpdatedRowDate`, `IsDelete`, `PublicId`, v.v.)
- **Chỉ trả Status = 2** (đã xuất bản) — không trả bản ghi ẩn

---

## Cấu trúc Solution

```
ELIBAPI/
├── ELIBAPI.Core/                   # Dùng chung — Entities, DTOs, Interfaces
│   └── Interfaces/
│       └── IRepositories.cs        # IPublicGenericRepository<TEntity, TSearch>
├── ELIBAPI.Infrastructure/         # Dùng chung — Repositories, Services, DbContext
│   └── Repositories/
│       ├── PublicBaseRepository.cs     # Public base: không JWT, enforce Status=2
│       └── PublicRepositories.cs      # Public concrete repositories
├── ELIBAPI.API/                    # API nội bộ — yêu cầu JWT
├── ELIBAPI.API.Public/             # API công khai — không cần JWT
│   ├── Controllers/
│   │   ├── PublicBaseController.cs     # Base controller [AllowAnonymous] — không theo schema
│   │   └── Cms/                        # Public controllers — namespace ELIBAPI.API.Public.Controllers.Cms
│   │       ├── PublicNewsController.cs
│   │       ├── PublicMenuController.cs
│   │       ├── PublicCategoryController.cs
│   │       └── PublicPhotoController.cs
│   ├── DTOs/
│   │   ├── PublicRequests.cs           # PublicSearchRequest + entity-specific requests
│   │   └── PublicResponses.cs          # PublicResponse riêng cho từng entity
│   ├── Program.cs
│   └── appsettings.json
└── ELIBAPI.sln
```

---

## 2 Endpoint chuẩn cho mỗi bảng Public

| Method | URL | Mô tả | Auth |
|--------|-----|-------|------|
| `POST` | `/api/public/{Entity}/Search` | Tìm kiếm có phân trang | Không cần |
| `POST` | `/api/public/{Entity}/SearchAll` | Lấy tất cả theo điều kiện | Không cần |

---

## PublicSearchRequest — Base cho tất cả Public Search

```csharp
// ELIBAPI.API.Public/DTOs/PublicRequests.cs

public class PublicSearchRequest
{
    public string? Keyword   { get; set; }
    public string? PortalId  { get; set; }
    public string? Language  { get; set; }
    public long?   DepartmentId { get; set; }  // Client tự truyền — không lấy từ JWT
    public int     PageIndex { get; set; } = 1;
    public int     PageSize  { get; set; } = 10;
}
```

---

## PublicResponses — ẩn trường nhạy cảm

```csharp
// ELIBAPI.API.Public/DTOs/PublicResponses.cs

// ==================== NEWS ====================
public class PublicNewsResponse
{
    public long     Id          { get; set; }
    public string?  Title       { get; set; }
    public string?  Brief       { get; set; }
    public string?  Content     { get; set; }
    public string?  Images      { get; set; }
    public string?  Thumb       { get; set; }
    public DateTime? StartTime  { get; set; }
    public DateTime? EndTime    { get; set; }
    public long?    CategoryId  { get; set; }
    public string?  PortalId    { get; set; }
    public string?  Language    { get; set; }
    public string?  Keyword     { get; set; }
    public string?  Author      { get; set; }
    public string?  Source      { get; set; }
    public string?  Types       { get; set; }
    public int?     Status      { get; set; }
    public int?     AllowComment { get; set; }
    public int?     TotalView   { get; set; }
    public string?  MetaTitle   { get; set; }
    public string?  MetaKeyword { get; set; }
    public string?  MetaDescription { get; set; }
    // Ẩn: IsDelete, CreatedRowBy, UpdateRowBy, CreatedRowDate, UpdatedRowDate,
    //      PublicId, DepartmentId, MaleAudio, FaleAudio, ContentAudio, BriefAudio, TitleAudio
}

// ==================== MENU ====================
public class PublicMenuResponse
{
    public long    Id         { get; set; }
    public string? Name       { get; set; }
    public long?   MenuType   { get; set; }
    public string? Link       { get; set; }
    public string? FriendUrl  { get; set; }
    public int?    SortOrder  { get; set; }
    public int?    Status     { get; set; }
    public string? OpenType   { get; set; }
    public string? PortalId   { get; set; }
    public string? Language   { get; set; }
    public long?   ParentId   { get; set; }
    public string? LinkType   { get; set; }
    public string? SubId      { get; set; }
    public int?    IsLogIn    { get; set; }
    public string? Icon       { get; set; }
    // Ẩn: IsDelete, CreatedRowBy, UpdateRowBy, CreatedRowDate, UpdatedRowDate, PublicId, DepartmentId
}

// ==================== CATEGORY ====================
public class PublicCategoryResponse
{
    public long    Id              { get; set; }
    public string? Name            { get; set; }
    public long?   ParentId        { get; set; }
    public long?   Level           { get; set; }
    public int?    Status          { get; set; }
    public int?    Order           { get; set; }
    public string? PortalId        { get; set; }
    public int?    IsLogin         { get; set; }
    public string? Description     { get; set; }
    public string? Keyword         { get; set; }
    public string? PageTitle       { get; set; }
    public string? MetaDescription { get; set; }
    public string? Language        { get; set; }
    public string? Link            { get; set; }
    // Ẩn: IsDelete, CreatedRowBy, UpdateRowBy, CreatedRowDate, UpdatedRowDate, PublicId, DepartmentId
}

// ==================== PHOTO ====================
public class PublicPhotoResponse
{
    public long    Id           { get; set; }
    public string? Name         { get; set; }
    public string? Brief        { get; set; }
    public string? Image        { get; set; }
    public string? Link         { get; set; }
    public string? Postion      { get; set; }
    public long?   PhotoAlbumId { get; set; }
    public int?    Width        { get; set; }
    public int?    Height       { get; set; }
    public int?    Status       { get; set; }
    public string? PortalId     { get; set; }
    public int?    SortOrder    { get; set; }
    public string? Language     { get; set; }
    public string? Types        { get; set; }
    // Ẩn: IsDelete, CreatedRowBy, UpdateRowBy, CreatedRowDate, UpdatedRowDate, PublicId, DepartmentId
}
```

---

## PublicBaseController

```csharp
// ELIBAPI.API.Public/Controllers/PublicBaseController.cs

[ApiController]
[AllowAnonymous]   // Toàn bộ Public API không cần JWT
[Route("api/public/[controller]")]
public abstract class PublicBaseController : ControllerBase { }
```

---

## IPublicGenericRepository (`ELIBAPI.Core/Interfaces/IRepositories.cs`)

```csharp
public interface IPublicGenericRepository<TEntity, TSearch>
    where TSearch : PublicSearchRequest
{
    Task<PagedResult<TEntity>> SearchAsync(TSearch request);
    Task<List<TEntity>> SearchAllAsync(TSearch request);
}
```

---

## PublicBaseRepository (`ELIBAPI.Infrastructure/Repositories/PublicBaseRepository.cs`)

```csharp
public abstract class PublicBaseRepository<TEntity, TSearch>
    : IPublicGenericRepository<TEntity, TSearch>
    where TEntity : class
    where TSearch : PublicSearchRequest
{
    protected readonly ELIBAPIDbContext _db;
    private readonly IMemoryCache _cache;

    protected PublicBaseRepository(ELIBAPIDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    // Subclass phải implement — LUÔN có IsDelete != 2 && Status == 2
    protected abstract IQueryable<TEntity> BuildQuery(TSearch request);

    public async Task<PagedResult<TEntity>> SearchAsync(TSearch request)
    {
        var q = BuildQuery(request);
        var total = await q.CountAsync();
        var items = await q
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();
        return new PagedResult<TEntity>
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = request.PageIndex,
            PageSize   = request.PageSize
        };
    }

    public async Task<List<TEntity>> SearchAllAsync(TSearch request)
        => await BuildQuery(request).ToListAsync();
}
```

---

## Ví dụ đầy đủ: PublicNewsRepository

```csharp
// ELIBAPI.Infrastructure/Repositories/PublicRepositories.cs

public class PublicNewsRepository
    : PublicBaseRepository<News, PublicNewsSearchRequest>
{
    public PublicNewsRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }
    // Cache Search/SearchAll được PublicBaseRepository xử lý tự động — không cần code thêm

    protected override IQueryable<News> BuildQuery(PublicNewsSearchRequest r)
    {
        // LUÔN có IsDelete != 2 && Status == 2 — chỉ trả bản ghi đã xuất bản
        var q = _db.News.Where(x => x.IsDelete != 2 && x.Status == 2);

        if (!string.IsNullOrEmpty(r.Keyword))  q = q.Where(x => x.Title!.Contains(r.Keyword));
        if (!string.IsNullOrEmpty(r.PortalId)) q = q.Where(x => x.PortalId == r.PortalId);
        if (!string.IsNullOrEmpty(r.Language)) q = q.Where(x => x.Language == r.Language);
        if (r.CategoryId.HasValue)             q = q.Where(x => x.CategoryId == r.CategoryId);
        if (r.Types != null)                   q = q.Where(x => x.Types == r.Types);
        if (r.DepartmentId.HasValue)           q = q.Where(x => x.DepartmentId == r.DepartmentId);

        return q.OrderByDescending(x => x.Id);
    }
}
```

---

## Cache — IMemoryCache

`PublicBaseRepository` bật cache tự động cho `Search` và `SearchAll`. Concrete public repository chỉ cần truyền `IMemoryCache` lên constructor:

```csharp
// ĐÚNG — truyền IMemoryCache lên base
public PublicNewsRepository(ELIBAPIDbContext db, IMemoryCache cache) : base(db, cache) { }

// SAI — không tự gọi cache trong concrete repository
_cache.Set("...", data);
```

- **Cache key**: `"{EntityName}:Search:{hash}"` / `"{EntityName}:SearchAll:{hash}"`
- **Duration**: 5 phút; configurable qua `CacheDurationMinutes` trong `appsettings.json`
- **Invalidation**: Public API không có write operations → cache hết hạn tự nhiên theo TTL, không cần invalidate

---

## Ví dụ đầy đủ: PublicNewsController

```csharp
// ELIBAPI.API.Public/Controllers/Cms/PublicNewsController.cs
namespace ELIBAPI.API.Public.Controllers.Cms;

[Route("api/public/[controller]")]
public class PublicNewsController : PublicBaseController
{
    private readonly IPublicGenericRepository<News, PublicNewsSearchRequest> _repo;

    public PublicNewsController(IPublicGenericRepository<News, PublicNewsSearchRequest> repo)
        => _repo = repo;

    // POST /api/public/News/Search
    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicNewsSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        var result = new PagedResult<PublicNewsResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        };
        return Ok(ApiResponse<PagedResult<PublicNewsResponse>>.Ok(result));
    }

    // POST /api/public/News/SearchAll
    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicNewsSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicNewsResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private static PublicNewsResponse ToResponse(News x) => new()
    {
        Id              = x.Id,
        Title           = x.Title,
        Brief           = x.Brief,
        Content         = x.Content,
        Images          = x.Images,
        Thumb           = x.Thumb,
        StartTime       = x.StartTime,
        EndTime         = x.EndTime,
        CategoryId      = x.CategoryId,
        PortalId        = x.PortalId,
        Language        = x.Language,
        Keyword         = x.Keyword,
        Author          = x.Author,
        Source          = x.Source,
        Types           = x.Types,
        Status          = x.Status,
        AllowComment    = x.AllowComment,
        TotalView       = x.TotalView,
        MetaTitle       = x.MetaTitle,
        MetaKeyword     = x.MetaKeyword,
        MetaDescription = x.MetaDescription,
    };
}
```

---

## PublicSearchRequest chi tiết cho từng entity

```csharp
// ELIBAPI.API.Public/DTOs/PublicRequests.cs

public class PublicNewsSearchRequest : PublicSearchRequest
{
    public long?   CategoryId { get; set; }
    public string? Types      { get; set; }
}

public class PublicMenuSearchRequest : PublicSearchRequest
{
    public long? MenuType { get; set; }
    public long? ParentId { get; set; }
}

public class PublicCategorySearchRequest : PublicSearchRequest
{
    public long? ParentId { get; set; }
    public long? Level    { get; set; }
}

public class PublicPhotoSearchRequest : PublicSearchRequest
{
    public long?   PhotoAlbumId { get; set; }
    public string? Types        { get; set; }
}
```

---

## Đăng ký DI Public Repositories (`ELIBAPI.Infrastructure/DependencyInjection.cs`)

Thêm vào `AddInfrastructure()`:

```csharp
// ── Public Repositories ──────────────────────────────────────────────────────
services.AddScoped<
    IPublicGenericRepository<News, PublicNewsSearchRequest>,
    PublicNewsRepository>();

services.AddScoped<
    IPublicGenericRepository<Menu, PublicMenuSearchRequest>,
    PublicMenuRepository>();

services.AddScoped<
    IPublicGenericRepository<Category, PublicCategorySearchRequest>,
    PublicCategoryRepository>();

services.AddScoped<
    IPublicGenericRepository<Photo, PublicPhotoSearchRequest>,
    PublicPhotoRepository>();
```

---

## Program.cs cho ELIBAPI.API.Public

```csharp
// ELIBAPI.API.Public/Program.cs

var builder = WebApplication.CreateBuilder(args);

// Dùng chung Infrastructure — đăng ký DbContext + tất cả repositories (kể cả Public)
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

// CORS — cho phép client bên ngoài gọi
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Không cần JWT Authentication — toàn bộ là [AllowAnonymous]

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.Run();
```

> **Lưu ý**: `ELIBAPI.API.Public` dùng lại `AddInfrastructure()` từ `ELIBAPI.Infrastructure` — Public repositories được đăng ký trong cùng method này.

---

## appsettings.json cho ELIBAPI.API.Public

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=ELIB;..."
  },
  "AllowedHosts": "*"
}
```

> Không cần cấu hình `Jwt` vì project này không xác thực token.

---

## Checklist thêm bảng Public mới

```
[ ] Tạo PublicXxxSearchRequest  trong PublicRequests.cs  (kế thừa PublicSearchRequest — CÓ DepartmentId)
[ ] Tạo PublicXxxResponse       trong PublicResponses.cs (chỉ giữ trường an toàn — KHÔNG có audit trail)
[ ] Tạo PublicXxxRepository     trong PublicRepositories.cs  kế thừa PublicBaseRepository<TEntity, TSearch>
    [ ] BuildQuery: LUÔN bắt đầu bằng IsDelete != 2 && Status == 2
    [ ] Filter DepartmentId chỉ khi r.DepartmentId.HasValue
    [ ] Constructor nhận IMemoryCache và truyền lên PublicBaseRepository — cache tự động
    [ ] Đăng ký DI trong DependencyInjection.cs: IPublicGenericRepository<TEntity, TSearch> → PublicXxxRepository
[ ] Tạo Controllers/{Schema}/PublicXxxController.cs — namespace ELIBAPI.API.Public.Controllers.{Schema}
    [ ] Kế thừa PublicBaseController
    [ ] Inject IPublicGenericRepository<TEntity, TSearch> — KHÔNG inject ELIBAPIDbContext
    [ ] ToResponse(): map an toàn sang PublicXxxResponse
    [ ] POST Search    → gọi _repo.SearchAsync  → phân trang
    [ ] POST SearchAll → gọi _repo.SearchAllAsync → không phân trang
[ ] KHÔNG gắn [Permission], KHÔNG gắn [Authorize] — chỉ [AllowAnonymous] từ base
```

---

## So sánh Public API vs Internal API

| Điểm khác biệt | ELIBAPI.API (Internal) | ELIBAPI.API.Public |
|---|---|---|
| Xác thực | JWT bắt buộc | Không cần |
| DepartmentId | Tự động từ JWT claim (BaseRepository) | Client tự truyền (optional) — xử lý trong BuildQuery |
| Status filter | Không bắt buộc — Internal trả cả bản ghi ẩn | **Bắt buộc `Status = 2`** — enforce tại `PublicBaseRepository.BuildQuery` |
| Repository | `BaseRepository<T,TSearch,TRequest>` | `PublicBaseRepository<T,TSearch>` |
| Controller inject | `IGenericRepository<T,TSearch,TRequest>` | `IPublicGenericRepository<T,TSearch>` |
| Số endpoint | 7 (CRUD đầy đủ) | 2 (Search + SearchAll) |
| Response | Entity đầy đủ | PublicResponse (ẩn audit trail) — mapping trong controller |
| Permission check | `[Permission]` attribute | Không có |
| Project | `ELIBAPI.API` | `ELIBAPI.API.Public` |
| Port (dev) | `https://localhost:7001` | `https://localhost:7002` |
