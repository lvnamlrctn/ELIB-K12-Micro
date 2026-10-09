# Hướng dẫn backend — Triển khai đa đơn vị (multi-tenant) theo subdomain

Một bản triển khai duy nhất phục vụ **92 đơn vị**, mỗi đơn vị một subdomain: `<madonvi>.thuvientn.vn` (vd `tanthai.thuvientn.vn`). OPAC mỗi đơn vị chỉ khác **tên ở header** và **đăng nhập bạn đọc**. Admin dùng chung, xác định đơn vị theo **user đăng nhập**.

## 1) Resolve tenant theo host (OPAC)
Frontend khi khởi động lấy `window.location.hostname` và gọi:

| Thuộc tính | Giá trị |
|---|---|
| Method/URL | `GET /api/public/PublicTenant/ResolveByHost?host=<hostname>` |
| Auth | **AllowAnonymous** |

**Response:**
```json
{
  "success": true,
  "data": {
    "tenantId": "1A552F01-81D8-4746-A985-1B8575B5E3F6",
    "name": "Thư viện Phường Phan Đình Phùng",
    "logoText": "PĐP",
    "logoUrl": null
  }
}
```
| Trường | Mô tả |
|---|---|
| `tenantId` | GUID đơn vị — FE gán vào mọi API public (`tenantId`) |
| `name` | Tên hiển thị ở header OPAC |
| `logoText` | Chữ trên logo tròn (tùy chọn; FE tự suy chữ cái đầu nếu thiếu) |
| `logoUrl` | (tùy chọn) ảnh logo |

**Yêu cầu backend:**
- Map `host` (hoặc subdomain phần đầu) → đơn vị. Khuyến nghị có bảng `Tenant(Subdomain, TenantId, Name, LogoText, ...)`.
- Không khớp → trả tenant mặc định hoặc `success:false`/404 (FE giữ fallback, OPAC vẫn chạy).
- FE gọi 1 lần lúc khởi động (chặn bootstrap tới khi xong); nên cache nhẹ.

> FE: [tenant.service.ts](../src/app/opac/services/tenant.service.ts) (`resolve()`), đăng ký `provideAppInitializer` trong [app.config.ts](../src/app/app.config.ts); gán `APP_CONFIG.TenantId`. localhost/IP/SSR → dùng fallback trong [config.ts](../src/app/opac/config.ts).

## 2) Đăng nhập bạn đọc theo đơn vị
`POST /api/public/PublicReader/Login` nay nhận thêm `tenantId`:
```json
{ "loginName": "...", "password": "...", "tenantId": "<GUID đơn vị>" }
```
- Backend chỉ cho phép tài khoản bạn đọc **thuộc đúng đơn vị** (`tenantId`) đăng nhập.

## 3) Các API public khác
Mọi endpoint `/api/public/...` (ebook, news, banner, hyperlink, system param, Z3950, review, visit...) đã nhận `tenantId` trong body/query → lọc dữ liệu theo đơn vị. Sau khi resolve, FE tự gửi đúng `tenantId`.

## 4) Admin (dùng chung, không đổi FE)
- `POST /api/Auth/Login` trả user kèm `tenantId`/`roleId`. Mọi API admin **tự scope theo tenant của user** (đã chuyển `DepartmentId → TenantId`).
- Không phụ thuộc subdomain — admin có thể truy cập qua tên miền chung; backend nhận diện đơn vị qua user/token.

## 5) Hạ tầng triển khai
- **DNS wildcard**: `*.thuvientn.vn` → 1 deployment FE (SSR/Nginx) + 1 backend multi-tenant.
- **SSL wildcard** cho `*.thuvientn.vn`.
- Thêm đơn vị mới = thêm 1 bản ghi `Tenant` + 1 subdomain DNS; **không cần build lại** FE.
- (Tùy chọn) Nếu dùng SSR và muốn resolve tenant phía server, đọc `Host` header của request (FE hiện resolve phía browser; SSR dùng tenant fallback rồi hydrate lại).

## Checklist nghiệm thu
1. `GET PublicTenant/ResolveByHost?host=tanthai.thuvientn.vn` trả đúng tenantId + tên đơn vị.
2. OPAC mỗi subdomain hiển thị đúng tên/logo; dữ liệu (sách/tin/banner) đúng đơn vị.
3. Đăng nhập bạn đọc chỉ chấp nhận tài khoản đúng đơn vị (gửi kèm `tenantId`).
4. Admin đăng nhập 1 nơi, thấy dữ liệu đúng đơn vị của user.
