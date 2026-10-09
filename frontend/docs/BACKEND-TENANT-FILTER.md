# Hướng dẫn backend — Lọc theo Đơn vị (TenantId) + trả TenantName cho Admin

Tài liệu này mô tả yêu cầu backend cho tính năng **super-admin lọc dữ liệu chéo đơn vị** trong trang quản trị. Nó **bổ sung** cho [BACKEND-MULTI-TENANT.md](BACKEND-MULTI-TENANT.md) (kiến trúc đa đơn vị theo subdomain) — đọc file đó trước để nắm bối cảnh.

## 1) Bối cảnh & nguyên tắc

Admin dùng chung 1 bản triển khai. Bình thường mỗi API admin **tự scope theo đơn vị của user đăng nhập**. Nay bổ sung:

- User **có quyền cấp cao** (`IsPrivileged = true`, vd super-admin toàn hệ thống) có thể **xem/lọc dữ liệu của bất kỳ đơn vị nào**, hoặc xem **tất cả đơn vị**.
- Frontend hiển thị **combobox "Đơn vị"** và **cột "Đơn vị" (TenantName)** trong danh sách/cây **chỉ khi** `IsPrivileged = true`.
- User thường **không thấy** combobox/cột; dữ liệu vẫn scope theo đơn vị của họ.

> **Nguyên tắc bảo mật quan trọng:** `tenantId` do FE gửi lên **chỉ được tôn trọng khi user là `IsPrivileged`**. Với user thường, backend **phải bỏ qua** `tenantId` trong request và **ép** scope theo `TenantId` của chính user (lấy từ token/claim). Không được tin `tenantId` từ client.

> **⚠️ Kiểu dữ liệu `tenantId`:** luôn là **PublicId (GUID/chuỗi)** của Tenant — **KHÔNG** phải id số (long). FE gửi lên chuỗi GUID (vd `"1A552F01-81D8-4746-A985-1B8575B5E3F6"`) hoặc `null`. Backend cần so khớp theo `Tenant.PublicId`.

## 2) Login trả về IsPrivileged + TenantId

`POST /api/Auth/Login` (body đã có `tenantId` — xem doc multi-tenant) cần trả về trong `data`:

```json
{
  "success": true,
  "data": {
    "token": "…",
    "isPrivileged": true,
    "tenantId": 12,
    "…": "…"
  }
}
```

| Trường | Kiểu | Mô tả |
|---|---|---|
| `isPrivileged` | `bool` | `true` → user được lọc chéo đơn vị (FE bật combobox + cột). Mặc định `false`. |
| `tenantId` | `string?` (GUID PublicId) | Đơn vị của user (**PublicId**, không phải id số); backend dùng để ép scope cho user thường. |

> FE lưu 2 trường này vào localStorage và đọc qua `Auth.isPrivileged()` / `Auth.getTenantId()`.

## 3) Nguồn danh sách đơn vị cho combobox

FE nạp options qua service Tenant sẵn có:

| | |
|---|---|
| Method/URL | `POST /api/Dbo/Tenant/Search` |
| Body | `{ "keyword": "", "pageIndex": 1, "pageSize": 500, "tenantId": null }` |
| Response | `{ "data": [ { "publicId": "1A55…E3F6", "name": "Thư viện A" }, … ], "recordsTotal": N }` |

Mỗi item cần có **`publicId`** (GUID — FE dùng làm **value** gửi khi lọc) + `name` (string, hiển thị). Chỉ nên trả về khi user gọi là `IsPrivileged`.

## 4) Tham số `tenantId` trong các API danh sách/cây

Tất cả endpoint **Search** (danh sách phân trang) và **GetTree** (cây) của admin nay nhận thêm `tenantId` trong **body**:

```jsonc
{
  "keyword": "",
  "pageIndex": 1,
  "pageSize": 10,
  "tenantId": "1A552F01-81D8-4746-A985-1B8575B5E3F6"  // GUID PublicId = 1 đơn vị | null = tất cả đơn vị
}
```

### Backend xử lý `tenantId`

```text
IF user.IsPrivileged:
    IF body.tenantId != null:               // body.tenantId là PublicId (GUID)
        lọc WHERE Tenant.PublicId = body.tenantId
    ELSE:
        không lọc theo đơn vị (trả tất cả đơn vị)
ELSE (user thường):
    BỎ QUA body.tenantId
    lọc WHERE Tenant.PublicId = user.TenantId   // ép theo token (PublicId)
```

### Trả về `tenantName` trong mỗi bản ghi

Mỗi item trong danh sách/cây cần kèm `tenantName` (tên đơn vị) để FE hiển thị cột:

```json
{ "id": 5, "name": "…", "tenantName": "Thư viện A", "…": "…" }
```

- Nếu bản ghi không gắn đơn vị → `tenantName` để `null` (FE hiển thị `—`).
- Đặt tên trường đúng **`tenantName`** (camelCase) để FE map trực tiếp.

## 5) Danh sách endpoint bị ảnh hưởng

Tất cả nhận `tenantId` trong body và cần trả `tenantName`:

| Nhóm | Endpoint | Kiểu |
|---|---|---|
| Tham số/từ điển dùng chung | `POST /api/Dbo/{Entity}/Search` (degrees, courses, academic-classes, reader-types, ethnics, nationalities, profs, chức vụ, z3950-group, ab-source, event…) | Search |
| Từ điển code/mô tả | `POST` các `DicCodeDesc` Search | Search |
| Bạn đọc | `POST /api/Circulation/Reader/Search` (cả export & cập nhật hàng loạt gửi kèm `tenantId`) | Search |
| Tài liệu số | `POST` EbookDocument `Search` | Search |
| Ebook (cây) | `GetTree` của Collection, Subject, Topic | GetTree |
| Ebook (danh sách) | `Search` của DigType, AccessPolicy, MetadataSchema | Search |
| CMS — Danh mục | `POST /api/Cms/Category/Search`, `/GetTree` (và nhánh `getAllCategories` dùng `Search` pageSize lớn) | Search + GetTree |
| CMS — Menu | `POST /api/Cms/…/GetTree` (cây menu) | GetTree |
| CMS — Banner | `POST /api/Cms/Banner/Search` | Search |
| CMS — Liên kết | `POST /api/Cms/Link/Search` | Search |
| CMS — Nhóm liên kết | `POST /api/Cms/LinkGroup/Search` | Search |
| CMS — Ảnh | `POST /api/Cms/Photo/Search` | Search |
| CMS — Album ảnh | `POST /api/Cms/PhotoAlbum/Search` | Search |
| CMS — Tin tức | `POST /api/Cms/News/Search` | Search |
| CMS — Loại menu | `POST /api/Cms/MenuType/Search` | Search |
| Hệ thống — Cơ cấu tổ chức | `POST /api/Dbo/Org/GetTree` | GetTree |
| Hệ thống — Người dùng | `POST` Users `Search` | Search |
| Ebook — Theo dõi đọc (thống kê) | `POST /api/Ebook/ReadingTracking/Statistics` | Thống kê |
| Ebook — Theo dõi đọc (chi tiết) | `POST /api/Ebook/ReadingTracking/Search` | Search |
| Tiếp nhận — Mượn theo khóa / Check-in-out / Báo cáo | `POST` borrow-key / checkin / receiption-report | Search/Report (param `tenantId` GUID, `null` = tất cả) |

> Với **GetTree**: `tenantId` cũng nằm trong body cạnh `parentId/level/pageIndex/pageSize`. Backend lọc gốc cây theo đơn vị (áp dụng cùng logic mục 4).

## 6) Ghi/Sửa dữ liệu (Add/Update)

Không thay đổi trong đợt này: dữ liệu tạo/sửa vẫn gắn đơn vị theo user (hoặc theo logic hiện có của backend). FE **không** gửi `tenantId` khi Add/Update trong tính năng này.

## Checklist nghiệm thu

1. Login trả `isPrivileged` + `tenantId` chính xác cho cả user thường và super-admin.
2. `POST /api/Dbo/Tenant/Search` trả danh sách `{id, name}` để đổ combobox.
3. User `IsPrivileged`:
   - Gửi `tenantId = <X>` → chỉ nhận dữ liệu của đơn vị X.
   - Gửi `tenantId = null` → nhận dữ liệu **mọi** đơn vị.
   - Mỗi bản ghi có `tenantName` đúng.
4. User thường: gửi `tenantId = <đơn vị khác>` → backend **bỏ qua**, chỉ trả dữ liệu đơn vị của chính user (không rò rỉ chéo đơn vị).
5. Các endpoint ở mục 5 đều nhận `tenantId` và trả `tenantName`.
