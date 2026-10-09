# Kiến trúc ELIBAPI

> Tài liệu này mô tả **monolith hiện tại**. Thiết kế microservice viết lại cho khách hàng mới nằm ở [microservice/README.md](microservice/README.md). Bộ đó thay thế `backend/DLS_Microservice_Architecture.docx`.

## Tầng (Layer Architecture)

```
┌──────────────────────────────┐  ┌──────────────────────────────────┐
│       ELIBAPI.API            │  │       ELIBAPI.API.Public          │
│  Controllers / Filters /     │  │  PublicControllers / PublicDTOs  │
│  Middleware                  │  │  → Không JWT, AllowAnonymous      │
│  → Yêu cầu JWT              │  │  → Chỉ Search + SearchAll         │
└──────────────┬───────────────┘  └─────────────────┬────────────────┘
               │ gọi IGenericRepository<T>           │ inject ELIBAPIDbContext trực tiếp
               └───────────────────┬─────────────────┘
                                   │ dùng chung
┌──────────────────────────────────▼─────────────────────────────────┐
│                       ELIBAPI.Infrastructure                        │
│           Repositories / Services / DbContext                       │
│           → Xử lý nghiệp vụ, truy cập database                    │
└──────────────────────────────────┬─────────────────────────────────┘
                                   │ sử dụng
┌──────────────────────────────────▼─────────────────────────────────┐
│                          ELIBAPI.Core                               │
│              Entities / DTOs / Interfaces / Common                  │
│              → Định nghĩa model, contract                           │
└─────────────────────────────────────────────────────────────────────┘
```

## Luồng xử lý — Internal API (`ELIBAPI.API`)

```
Client (có JWT)
  │
  ▼
[JWT Middleware]          ← Xác thực token, gán ClaimsPrincipal
  │                         (token chứa UserId, DepartmentId)
  ▼
[PermissionAttribute]     ← Kiểm tra cms.Permission theo UserId + ModuleId
  │
  ▼
[Controller Action]       ← Nhận request DTO, gọi repository
  │
  ▼
[BaseRepository]          ← Đọc DepartmentId từ JWT claim (IHttpContextAccessor)
  │                         • Search/SearchAll : WHERE DepartmentId = @dept (null → admin, bỏ qua)
  │                         • Add              : gán entity.DepartmentId = @dept (override client)
  │                         • Update/Delete    : kiểm tra entity.DepartmentId == @dept → 403 nếu sai
  │                         • GetByPublicId    : WHERE PublicId == guid AND IsDelete != 2
  │                         • Add/Update/Delete/ChangeStatus: sau SaveChanges → ghi dbo.UserLog
  │                         BuildQuery / MapRequest / SoftDelete / SetStatus
  ▼
[ELIBAPIDbContext]        ← EF Core → SQL Server
  │
  ▼
[ApiResponse<T>]          ← Wrap kết quả, trả về client
```

## Luồng xử lý — Public API (`ELIBAPI.API.Public`)

```
Client (không cần JWT)
  │
  ▼
[PublicBaseController]    ← [AllowAnonymous] — bỏ qua xác thực
  │
  ▼
[PublicXxxController]     ← Nhận PublicXxxSearchRequest, gọi repository
  │                         Mapping Entity → PublicXxxResponse thực hiện tại đây
  │
  ▼
[PublicBaseRepository]    ← BuildQuery: LUÔN có IsDelete != 2 && Status == 2
  │                         Filter DepartmentId nếu client truyền (optional)
  │                         Không có JWT context — DepartmentId từ request body
  │                         • Search/SearchAll: check IMemoryCache → hit: trả cache; miss: query DB → set cache 5 phút
  ▼
[ELIBAPIDbContext]        ← EF Core → SQL Server
  │
  ▼
[PublicXxxResponse]       ← Map sang response công khai (ẩn audit trail)
  │
  ▼
[ApiResponse<T>]          ← Wrap kết quả, trả về client
```

## Dependency Injection

### ELIBAPI.API (Internal)
```
IAuthService             → AuthService
IPermissionService       → PermissionService
IGenericRepository<T>    → Concrete Repository tương ứng
IHttpContextAccessor     → (built-in) — đọc JWT claims trong BaseRepository
ELIBAPIDbContext         → SQL Server (Scoped)
// UserLog: BaseRepository tự ghi qua ELIBAPIDbContext (đã có sẵn)
```

### ELIBAPI.API.Public
```
IPublicGenericRepository<T, TSearch>  → PublicXxxRepository (concrete)
ELIBAPIDbContext                      → SQL Server (Scoped)
IMemoryCache                          → (AddMemoryCache) — cache Search/SearchAll trong PublicBaseRepository
// Không cần IAuthService, IPermissionService, IHttpContextAccessor
```

Cả hai gọi `builder.Services.AddInfrastructure(configuration)` — dùng chung một implementation.

## Cache Strategy — Public API

- **Áp dụng**: CHỈ `PublicBaseRepository` — `BaseRepository` (Internal) **không** dùng cache
- **Cache key**: `"{EntityTypeName}:{MethodName}:{hash(JsonSerialize(request))}"`  
  Ví dụ: `"News:Search:a3f9c2"`, `"News:SearchAll:b1d8e4"`
- **Duration**: 5 phút mặc định; configurable qua `appsettings.json` key `"CacheDurationMinutes"`
- **Invalidation**: Public API không có write operations → không cần invalidate thủ công; cache tự hết hạn sau TTL
- **Scope**: chỉ `ELIBAPI.API.Public` — không ảnh hưởng Internal API

## UserLog — Ghi nhật ký thao tác

- Ghi vào `dbo.UserLog` sau `SaveChanges()` thành công cho: Add, Update, Delete, ChangeStatus (chỉ Internal API)
- **Không ghi** cho: GetByPublicId, Search, SearchAll; **Public API không ghi UserLog**
- Nguồn dữ liệu từng trường:

| Trường | Nguồn |
|--------|-------|
| `UserId` | `IHttpContextAccessor` → `ClaimTypes.NameIdentifier` |
| `ActionType` | `"Add"` \| `"Update"` \| `"Delete"` \| `"ChangeStatus"` |
| `Object` | `typeof(TEntity).Name` |
| `Action` | `"Add {EntityName} #{Id}"` (mô tả ngắn) |
| `Submited` | `DateTime.Now` |
| `Ip` | `HttpContext.Connection.RemoteIpAddress?.ToString()` |
| `Application` | `"ELIBAPI"` (hằng số) |
| `PortalId` | Reflection: `entity.GetType().GetProperty("PortalId")?.GetValue(entity) as string` → `null` nếu entity không có |

## Cấu trúc file thực tế

```
ELIBAPI.Core/
├── Common/
│   └── ApiResponse.cs              # ApiResponse<T>, PagedResult<T>
├── DTOs/
│   ├── Request/Requests.cs         # Tất cả Request DTO (Internal)
│   └── Response/Responses.cs       # LoginResponse
├── Entities/
│   ├── Cms/                        # Entity schema cms — namespace ELIBAPI.Core.Entities.Cms
│   │   ├── News.cs
│   │   ├── Category.cs
│   │   ├── Photo.cs
│   │   ├── PhotoAlbum.cs
│   │   ├── Menu.cs
│   │   ├── MenuType.cs
│   │   ├── Module.cs
│   │   ├── ModuleRoles.cs
│   │   ├── Permission.cs
│   │   └── AttachFile.cs
│   └── Dbo/                        # Entity schema dbo — namespace ELIBAPI.Core.Entities.Dbo
│       └── Users.cs
└── Interfaces/
    └── IRepositories.cs            # IAuthService, IGenericRepository, IPermissionService, IPublicGenericRepository

ELIBAPI.Infrastructure/
├── Data/
│   └── ELIBAPIDbContext.cs
├── Repositories/
│   ├── BaseRepository.cs           # Internal generic base — chứa DepartmentId/JWT logic
│   ├── Repositories.cs             # Internal concrete repositories
│   ├── PublicBaseRepository.cs     # Public generic base — không có JWT, enforce Status=2
│   └── PublicRepositories.cs      # Public concrete repositories
├── Services/
│   ├── AuthService.cs              # JWT + MD5 + DepartmentId claim
│   └── PermissionService.cs
└── DependencyInjection.cs

ELIBAPI.API/                        # Internal API
├── Controllers/
│   ├── BaseApiController.cs        # [Authorize] + GenericController<T> — base, không theo schema
│   ├── AuthController.cs           # Auth — namespace ELIBAPI.API.Controllers
│   └── Cms/                        # Entity controllers — namespace ELIBAPI.API.Controllers.Cms
│       ├── NewsController.cs
│       ├── CategoryController.cs
│       ├── MenuController.cs
│       ├── MenuTypeController.cs
│       ├── PhotoController.cs
│       ├── PhotoAlbumController.cs
│       ├── AttachFileController.cs
│       ├── ModuleController.cs
│       ├── ModuleRolesController.cs
│       └── PermissionController.cs
├── Filters/
│   └── PermissionAttribute.cs
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
├── Resources/
│   ├── SharedResource.en.resx
│   └── SharedResource.vi.resx
├── Program.cs
└── appsettings.json

ELIBAPI.API.Public/                 # Public API
├── Controllers/
│   ├── PublicBaseController.cs     # [AllowAnonymous] — base, không theo schema
│   └── Cms/                        # Public controllers — namespace ELIBAPI.API.Public.Controllers.Cms
│       ├── PublicNewsController.cs
│       ├── PublicMenuController.cs
│       ├── PublicCategoryController.cs
│       └── PublicPhotoController.cs
├── DTOs/
│   ├── PublicRequests.cs           # PublicSearchRequest + entity-specific requests
│   └── PublicResponses.cs          # PublicXxxResponse — không có audit trail
├── Program.cs
└── appsettings.json
```

## Database Schema

- Schema `dbo`: bảng `Users`, `UserLog` (xem ENTITIES.md — ghi log thao tác bởi Internal API)
- Schema `cms`: `AttachFile`, `Category`, `Menu`, `MenuType`, `Module`, `ModuleRoles`, `news`, `Permission`, `Photo`, `PhotoAlbum`

## Quy ước chung

- **Soft delete**: cột `IsDelete` — `2` = đã xoá, `1/null` = chưa xoá; mọi query lọc `IsDelete != 2`
- **Audit trail**: `CreatedRowBy`, `UpdateRowBy`, `CreatedRowDate`, `UpdatedRowDate` — tự động gán trong Internal repository; **ẩn hoàn toàn** trong Public response
- **PublicId**: GUID — tự sinh bởi SQL Server (`newsequentialid()`); **ẩn** trong Public response
- **Phân trang**: `PageIndex` bắt đầu từ `1`, `PageSize` mặc định `10`
- **DepartmentId**:
  - Internal: tự động từ JWT claim — client không truyền
  - Public: client tự truyền (optional) — không truyền thì không filter
- **Status = 2**: Public API luôn enforce **tại tầng Repository** (`PublicBaseRepository.BuildQuery`) — chỉ trả bản ghi đã xuất bản; Internal API không bắt buộc filter Status (trả cả bản ghi ẩn để quản trị)
