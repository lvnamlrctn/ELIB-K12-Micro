# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tổng quan dự án
ELIBAPI là REST API quản lý nội dung xây dựng bằng **.NET 8** + **Entity Framework Core** + **JWT Authentication**, kiến trúc **3 tầng** (Core / Infrastructure / API).

Solution gồm **3 project**:

| Project | Mục đích |
|---------|----------|
| `ELIBAPI.Core` | Entities, DTOs, Interfaces, Common — dùng chung |
| `ELIBAPI.Infrastructure` | DbContext, Repositories, Services, DI — dùng chung |
| `ELIBAPI.API` | API nội bộ (JWT, CRUD đầy đủ) + API công khai `/api/public/**` trong `Controllers/Public` |

## Tài liệu chi tiết
| File | Nội dung |
|------|----------|
| `docs/ARCHITECTURE.md` | Kiến trúc tổng thể, luồng xử lý request, file structure thực tế |
| `docs/CONVENTIONS.md` | Quy ước đặt tên, coding style, checklist thêm bảng mới |
| `docs/ENTITIES.md` | Toàn bộ entity & schema database |
| `docs/API_PATTERN.md` | Pattern chuẩn + ví dụ đầy đủ 6 bước để tạo Internal API mới |
| `docs/PUBLIC_API_PATTERN.md` | Pattern chuẩn + ví dụ đầy đủ để tạo Public API mới |
| `docs/AUTH_PERMISSION.md` | Xác thực JWT & phân quyền Permission |
| `docs/DTOS.md` | Request/Response DTOs đầy đủ cho mọi schema |

## Lệnh thường dùng
```bash
# Build toàn bộ solution
dotnet build ELIBAPI.sln

# Chạy API nội bộ (https://localhost:7001)
cd ELIBAPI.API && dotnet run


# Restore packages
dotnet restore ELIBAPI.sln

# Tạo migration
cd ELIBAPI.Infrastructure
dotnet ef migrations add <TênMigration> --startup-project ../ELIBAPI.API

# Cập nhật database
dotnet ef database update --startup-project ../ELIBAPI.API
```

## Database Schemas

| Schema | Namespace C# | Ví dụ entity |
|--------|-------------|--------------|
| `dbo` | `ELIBAPI.Core.Entities.Dbo` | `Users`, `UserLog` |
| `cms` | `ELIBAPI.Core.Entities.Cms` | `News`, `Category`, `Menu`, `Photo`, ... |
| `Ebook` | `ELIBAPI.Core.Entities.Ebook` | `IntroBooks`, `EbookCollection`, `EbookItem` |
| `EOffice` | `ELIBAPI.Core.Entities.EOffice` | `Document`, ... |
| `Evaluate` | `ELIBAPI.Core.Entities.Evaluate` | `MonHoc`, `NganhHoc`, ... |
| `PrintBook` | `ELIBAPI.Core.Entities.PrintBook` | `Bib`, `Barcode`, `BookOut`, ... |

## Nguyên tắc quan trọng nhất

### ELIBAPI.API — Internal
1. **KHÔNG** tạo controller/repository mới từ đầu — kế thừa `GenericController` và `BaseRepository`
2. **LUÔN** gắn `[Permission("MODULE_CODE", "action")]` vào Add/Update/Delete/ChangeStatus — dùng `ModuleCode` string (từ `cms.Module.ModuleCode`), không hardcode `ModuleId`
   - `"add"` → kiểm tra `Can_Add = 2`; `"edit"` → `Can_Edit = 2`; `"delete"` → `Can_Delete = 2`
3. **LUÔN** dùng `ApiResponse<T>` làm wrapper cho mọi response
4. **LUÔN** soft delete (`IsDelete = 2`), không xoá thật; query lọc `IsDelete != 2`; `IsDelete = 1/null` là chưa xoá
5. **KHÔNG** thay đổi tên bảng/cột — ánh xạ với database hiện có
6. **Mỗi bảng** = 1 file Entity riêng (`Entities/{Schema}/{Entity}.cs`) + 1 file Controller riêng (`Controllers/{Schema}/{Entity}Controller.cs`)
7. **Update/Delete** dùng `Guid publicId` (trường `PublicId`), không dùng `long id`; `GetById` cũng dùng `publicId`
8. **Status**: `2` = Xuất bản/Hoạt động, `1` = Ẩn/Chưa xuất bản
9. **Message**: base tiếng Anh, dùng `IStringLocalizer` để localize — client gửi `Accept-Language` header
10. **TenantId (đơn vị) tự động từ JWT** — `BaseRepository` đọc claim `TenantId`, không cần client truyền, `BuildQuery` không tự lọc:
    - **Search/SearchAll**: `ApplyTenantFilter` → `WHERE TenantId = @tenant`; tài khoản đặc quyền (không đơn vị / `ReadOnlyPolicy`) thấy tất cả hoặc đơn vị đã chọn + dữ liệu dùng chung (`TenantId = null`)
    - **GetById/GetByPublicId**: chỉ trả bản ghi của đơn vị mình hoặc dùng chung
    - **Add**: tự gán `TenantId` của user; **Update/Delete**: kiểm tra bản ghi thuộc đúng đơn vị
    - Controller viết tay dùng `TenantScopeHelper`; ĐKCB chỉ duy nhất trong đơn vị — tra qua `BarcodeTenantLookup`
11. **UserLog**: `BaseRepository` tự ghi vào `dbo.UserLog` sau Add/Update/Delete/ChangeStatus thành công — không tự ghi thêm
12. **Không dùng `IMemoryCache`** trong Internal repositories — cache chỉ dùng ở Public API

### Public API (`ELIBAPI.API/Controllers/Public`, route `/api/public/**`)
13. **KHÔNG** dùng JWT, **KHÔNG** dùng `[Authorize]` hay `[Permission]` — toàn bộ `[AllowAnonymous]`
14. **CHỈ** có 2 endpoint: `POST Search` và `POST SearchAll` — không có Add/Update/Delete
15. **LUÔN** filter `Status = 2` và `IsDelete != 2` — enforce **tại `BuildQuery` của Repository**, không filter trong controller
16. **TenantId theo tên miền** — `PublicHostTenantFilter` ghi đè `TenantId` (Guid) của request bằng đơn vị suy từ Host; host chung thì dùng giá trị client gửi; xử lý trong `BuildQuery` của Repository
17. **Response dùng `PublicXxxResponse`** — ẩn hoàn toàn các trường audit trail; mapping Entity → Response thực hiện trong controller (`ToResponse()`)
18. **Kế thừa `PublicBaseController`** — không dùng `GenericController`
19. **Kế thừa `PublicBaseRepository`** và truyền `IMemoryCache` lên constructor — cache Search/SearchAll tự động 5 phút; không tự gọi `_cache.Set()`
20. **KHÔNG** inject `ELIBAPIDbContext` trực tiếp vào controller; inject `IPublicGenericRepository<TEntity, TSearch>`

### Quy tắc chung không được vi phạm
- **Request DTO Internal**: không khai báo `TenantId` để ghi (đơn vị lấy từ JWT); DTO tìm kiếm có `Guid? TenantId` cho tài khoản đặc quyền chọn đơn vị
- **Request DTO Public**: có `TenantId` (Guid, bị ghi đè theo Host)
- **Response Public**: không có `IsDelete`, `CreatedRowBy`, `UpdateRowBy`, `CreatedRowDate`, `UpdatedRowDate`, `PublicId`, `TenantId`
- **Entity**: thuộc tính giữ nguyên tên cột database (ví dụ `Can_Add`, `IsDelete`, `CreatedRowBy`)
- **Mật khẩu**: `AuthService` dùng **MD5** để hash — nếu hệ thống cũ dùng cách khác, sửa `HashPassword` trong `AuthService.cs`

## 7 endpoint chuẩn mỗi bảng Internal

| Method | URL | Quyền |
|--------|-----|-------|
| `GET` | `/api/{Table}/{id}` | Đăng nhập (legacy — PK long) |
| `GET` | `/api/{Table}/GetById/{publicId}` | Đăng nhập, **không** `[Permission]` |
| `POST` | `/api/{Table}/Search` | Đăng nhập |
| `POST` | `/api/{Table}/SearchAll` | Đăng nhập |
| `POST` | `/api/{Table}/Add` | `[Permission("CODE", "add")]` |
| `PUT` | `/api/{Table}/Update/{publicId}` | `[Permission("CODE", "edit")]` |
| `DELETE` | `/api/{Table}/Delete/{publicId}` | `[Permission("CODE", "delete")]` |
| `PUT` | `/api/{Table}/ChangeStatus` | `[Permission("CODE", "edit")]` |

> Xem `docs/API_PATTERN.md` để có ví dụ đầy đủ 6 bước (Entity → DbContext → DTO → Repository → DI → Controller).  
> Xem `docs/PUBLIC_API_PATTERN.md` để có ví dụ đầy đủ Public API.
