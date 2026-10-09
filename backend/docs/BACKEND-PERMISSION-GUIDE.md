# Hướng dẫn backend — Phân quyền & Menu admin (RBAC)

Tài liệu này đặc tả **những gì backend (.NET) phải đảm bảo** để cơ chế phân quyền của frontend admin-website hoạt động đúng, kèm ghi chú 2 thay đổi hợp đồng API gần đây (`tenantId`, thống kê truy cập).

> Văn phong & quy ước response giống [BACKEND-API-ELIB.md](./BACKEND-API-ELIB.md): `{ "success": true, "data": ... }`.

---

## 1. Tổng quan cơ chế phía frontend

FE **không tự định nghĩa quyền**. Nó ghép 2 nguồn dữ liệu từ backend:

1. **Cây module** (`POST /api/Cms/Module/GetTree`) → cho biết mỗi module có `id`, `moduleCode`, **`link`** (đường dẫn route admin).
2. **Quyền của user** (`GET /api/Dbo/Users/GetPermission/{userId}`) → cho mỗi `moduleId` các cờ `can_View/Add/Edit/Delete`.

FE **join 2 nguồn theo `moduleId`** để ra bảng `link → {view, add, edit, delete}`, rồi:

- **Menu**: một mục menu chỉ hiển thị nếu module tương ứng (khớp theo `link`) có **`canView = true`**. Mục cha hiện nếu còn ≥1 mục con hiện.
- **Nút trên trang** (khớp theo URL hiện tại):

  | Nút / hành động | Cờ quyền yêu cầu |
  |---|---|
  | Thêm mới | `canAdd` |
  | Sửa | `canEdit` |
  | Xóa / Xóa nhiều | `canDelete` |
  | **Xuất (Export)** | **`canView`** |
  | **Nhập (Import)** | **`canAdd`** |

- **Route con** (vd `/admin/catalog-bibs/edit/:id`) lấy quyền theo module có `link` là **tiền tố dài nhất** khớp URL → nên chỉ cần seed module cho route gốc (`/admin/catalog-bibs`).

> ⚠️ **Fail-open**: nếu FE **không nhận được dữ liệu quyền** (endpoint lỗi/rỗng, hoặc không có `userId`), FE **hiển thị tất cả** menu & nút để tránh khóa trắng giao diện. Nghĩa là: **muốn siết quyền có hiệu lực, backend BẮT BUỘC phải trả đúng 3 hợp đồng dưới đây.**

---

## 2. Login phải trả về định danh user

`POST /api/Auth/Login` — body `{ loginName, password }`.

`data` trả về **phải có `id` (bigint) và/hoặc `publicId` (GUID)** của user, ngoài các trường hiện có:

```json
{
  "success": true,
  "data": {
    "id": 1024,
    "publicId": "04503B89-CD40-F111-8A6B-0050569D1DDC",
    "roleId": 3,
    "fullName": "Nguyễn Văn A",
    "loginName": "admin",
    "token": "<jwt>"
  }
}
```

FE lưu `id`/`publicId`/`roleId` vào `localStorage.user` và gọi `GetPermission/{publicId ?? id}`.
**Nếu thiếu cả `id` lẫn `publicId`** ⇒ FE không tải được quyền ⇒ rơi vào fail-open (hiện hết).

---

## 3. Cây module — `POST /api/Cms/Module/GetTree`

Body FE gửi: `{ "keyword": "", "status": 0, "pageIndex": 0, "pageSize": 0, "parentId": 0 }`.

Trả về cây, mỗi node:

```json
{
  "id": 12,
  "moduleCode": "READER",
  "link": "/admin/readers",
  "parentId": null,
  "status": 1,
  "children": [ /* … node con cùng cấu trúc … */ ]
}
```

**Yêu cầu then chốt:**

- **`link` phải khớp CHÍNH XÁC route admin** mà FE dùng (vd `/admin/readers`, `/admin/catalog-bibs`). FE chuẩn hóa khi so khớp (bỏ `/` thừa đầu/cuối, lowercase), nhưng **đường dẫn phải đúng**. `link` sai/để trống ⇒ mục menu đó **không khớp được** ⇒ bị ẩn (trừ Dashboard, xem §5).
- Bảng `module` cần **seed 1 bản ghi cho mỗi route cần kiểm soát** (danh sách route ở §5).
- `id` chính là `moduleId` dùng trong bảng quyền (§4) — phải nhất quán.

---

## 4. Quyền của user

### 4.1 Lấy quyền — `GET /api/Dbo/Users/GetPermission/{userId}`

- `{userId}` nhận **publicId (GUID) hoặc id (số)** — FE truyền `publicId ?? id`.
- Trả về **cây module kèm cờ quyền**, mỗi node:

```json
{
  "moduleId": 12,
  "moduleName": "Bạn đọc",
  "can_View": 2,
  "can_Add": 2,
  "can_Edit": 0,
  "can_Delete": 0,
  "parentId": null,
  "children": [ /* … */ ]
}
```

- **Quy ước cờ: giá trị `=== 2` nghĩa là CÓ quyền**; các giá trị khác (0/1/null) = không có quyền. (FE map `canView = can_View === 2`, v.v.)
- `moduleId` phải trùng `id` ở cây module (§3) để FE join ra `link`.

### 4.2 Lưu quyền — `POST /api/Dbo/Users/SavePermission`

Màn quản trị User (`/admin/users`) gửi:

```json
{
  "UserId": "<publicId hoặc id>",
  "Permissions": [
    { "ModuleId": 12, "CanView": true, "CanAdd": true, "CanEdit": false, "CanDelete": false }
  ]
}
```

Backend quy đổi `true/false` ⇄ cờ `2/0` khi lưu/đọc cho nhất quán với §4.1.

---

## 5. Seed dữ liệu & tránh tự khóa

- **Seed module** cho mọi route admin cần kiểm soát. Danh sách route lấy từ menu sidebar (`src/app/services/menu.ts`) và bảng tổng hợp Nhóm 1–6 ở [BACKEND-API-ELIB.md](./BACKEND-API-ELIB.md). Một số ví dụ:

  | `link` (phải khớp) | Ý nghĩa |
  |---|---|
  | `/admin/dashboard` | Trang chủ (FE luôn hiện — xem ghi chú dưới) |
  | `/admin/readers` | Bạn đọc |
  | `/admin/books`, `/admin/ebooks` | Kho tài liệu |
  | `/admin/catalog-bibs` | Biên mục (gồm route con `/edit/:mfn`) |
  | `/admin/borrow`, `/admin/loan-history`, `/admin/fines`, `/admin/circ-policies` | Lưu thông |
  | `/admin/magazine-types`, `/admin/serial-subscriptions`, `/admin/serial-issues` | Ấn phẩm định kỳ |
  | `/admin/inventory`, `/admin/lost-books`, `/admin/liquidates` | Quản lý kho |
  | `/admin/check-in-out`, `/admin/borrow-keys` | Vào ra |
  | `/admin/users`, `/admin/roles`, `/admin/modules`, `/admin/departments` | Hệ thống |
  | `/admin/z3950-configs`, `/admin/z3950-search` | Z39.50 |

- **Role/admin tổng** phải được gán **đủ `can_View/Add/Edit/Delete = 2`** trên mọi module, nếu không admin sẽ tự ẩn mất menu/nút của chính mình.
- **Ghi chú Dashboard**: FE đặt `/admin/dashboard` vào danh sách **luôn hiển thị** (không bao giờ ẩn) để người dùng còn trang đích sau khi đăng nhập, dù module dashboard chưa được cấp quyền.

---

## 6. (Khuyến nghị, tùy chọn) Endpoint quyền theo token

Để FE **không phụ thuộc việc Login trả `id`**, backend có thể bổ sung:

- `GET /api/Dbo/Users/MyPermission` → trả đúng cấu trúc §4.1 cho **chính user theo token** (không cần `{userId}`), hoặc
- `GET /api/Cms/Module/GetMenu` → trả thẳng cây menu đã lọc theo quyền của user hiện tại.

Khi có endpoint này, FE chỉ cần 1 lời gọi và bỏ được ràng buộc ở §2. (Hiện FE đang dùng `GetTree` + `GetPermission/{id}`.)

---

## 7. Ghi chú đổi hợp đồng API gần đây (không thuộc RBAC)

### 7.1 `departmentId` → `tenantId`
FE đã đổi **toàn bộ** `departmentId`/`DepartmentId` thành `tenantId`/`TenantId` trên **cả body JSON lẫn query param** (các service OPAC và receiption). Backend cần **chấp nhận tên trường `tenantId`/`TenantId`**.

- **Ngoại lệ giữ nguyên**: `GET /api/public/PublicMenu/BuildCmsMenu?DepartmentCode=<tenant>` — tham số vẫn tên `DepartmentCode` (giá trị là tenant), **không** đổi.

### 7.2 Thống kê truy cập (đã có, xác nhận hợp đồng)
- `POST /api/public/PublicStatistic/TrackVisit` — body `{ "tenantId": "<GUID|null>" }`. Ghi nhận 1 lượt truy cập (server tự đọc IP từ `X-Forwarded-For`/`RemoteIpAddress`). `[AllowAnonymous]`.
- `GET /api/public/PublicCounter/Stats?tenantId=<GUID>` → `{ "data": { "total": 12500, "today": 47, "lastWeek": 312, "lastMonth": 1240 } }`. `[AllowAnonymous]`, cache ~1 phút.

---

## 8. Checklist nghiệm thu backend

- [ ] `POST /api/Auth/Login` trả `data.id` và/hoặc `data.publicId` (khuyến nghị kèm `roleId`).
- [ ] `POST /api/Cms/Module/GetTree` trả node có `id`, `moduleCode`, **`link` khớp route admin**, `parentId`, `status`, `children`.
- [ ] Đã **seed module** cho mọi route cần kiểm soát (link đúng chuẩn `/admin/...`).
- [ ] `GET /api/Dbo/Users/GetPermission/{userId}` nhận publicId/id, trả cây với `can_*` theo quy ước **`=== 2` là có quyền**; `moduleId` trùng `id` ở GetTree.
- [ ] `POST /api/Dbo/Users/SavePermission` lưu/đọc nhất quán cờ 2/0.
- [ ] Role admin tổng được cấp đủ quyền (tránh tự khóa).
- [ ] Các endpoint OPAC/receiption chấp nhận `tenantId`/`TenantId`; `BuildCmsMenu` vẫn dùng `DepartmentCode`.
- [ ] (Tùy chọn) `MyPermission`/`GetMenu` theo token.
