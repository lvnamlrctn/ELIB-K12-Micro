# 08 — Frontend

## 1. Hiện trạng

`frontend/` là một app Angular 21 duy nhất: standalone component, Signals, Material + Tailwind 4, SSR (`outputMode: "server"`). Trong đó:
- `/` là OPAC (`src/app/opac/`).
- `/admin/**` là trang quản trị (`src/app/pages/admin/`, `src/app/services/<domain>/`).
- Kiosk (check-in phòng bằng khuôn mặt) cũng nằm trong admin.
- Mọi API đều gọi `/api/...` tới một backend duy nhất.
- Có hai interceptor: `services/auth.interceptor.ts` cho admin và `opac/auth.interceptor.ts` cho bạn đọc.

## 2. Mục tiêu

Tách thành **hai app deploy độc lập** trong một **Nx workspace** ([ADR-010](adr/ADR-010-nx-frontend.md)):

| App | Kiểu | Phục vụ | Deploy |
|---|---|---|---|
| `opac` | Angular SSR (Node) | Bạn đọc, khách: tra cứu, trang đọc, MyLibrary, đặt phòng, thanh toán, chat AI, CMS | Deployment `opac-web`, HPA, cache CDN cho trang public |
| `admin` | Angular SPA (build tĩnh) | Thủ thư, quản trị, kiosk | nginx static (`admin-web`), có thể đặt sau CDN |
| `kiosk` (tuỳ chọn, giai đoạn sau) | Angular SPA nhẹ | Máy tự check-in, tra cứu tại chỗ | Tách từ `admin` khi cần cài lên thiết bị riêng |

**SEO và tốc độ:** chỉ OPAC cần SSR. Admin bỏ SSR để build và deploy đơn giản, và để giảm tải Node.

> **Hiện trạng GĐ0 (2026-10):**
> - `src/Web` hiện là **workspace Angular CLI nhiều project** (`projects/admin`), chưa dùng Nx. Lý do: mới có một app, và Angular CLI đủ cho việc đó.
> - Chuyển sang Nx (`nx init` chạy được trên workspace có sẵn) khi thêm `opac` và các thư viện dùng chung, tức là lúc cần `enforce-module-boundaries` và build theo phần thay đổi.
> - Đăng nhập dùng `oidc-client-ts` (code + PKCE, refresh token, lưu trong `sessionStorage`).

## 3. Cấu trúc Nx workspace

```
web/
├── apps/
│   ├── opac/
│   └── admin/
└── libs/
    ├── shared/ui/            # component dùng chung (bảng, form, dialog, face-capture…)
    ├── shared/auth/          # OIDC client (PKCE), interceptor, guard, ticket cho SSE/download
    ├── shared/tenant/        # resolve tenant theo host, FeatureService (license module)
    ├── shared/i18n/
    ├── api/<service>/        # client TypeScript sinh từ OpenAPI của từng service (openapi-generator / orval)
    ├── opac/feature-*/       # search, reader, booking, payment, chat, my-library, cms
    └── admin/feature-*/      # catalog, holdings, circulation, acquisition, serials, digital, space, …
```

- **Ranh giới phụ thuộc** (Nx `enforce-module-boundaries`):
  - `admin/feature-circulation` chỉ được import `api/circulation`, `shared/*`, và client của các service có trong luồng của nó.
  - Không import chéo giữa feature của các gói khác nhau.
- **Mỗi feature lib là một lazy route.** Gói chưa mua thì không tải bundle tương ứng.

## 4. Gọi API

- **Một origin, qua gateway:**
  - OPAC gọi `/api/opac/{service}/...`, Admin gọi `/api/admin/{service}/...`.
  - Không gọi trực tiếp service. Không cần CORS cho trường hợp cùng domain.
- **SSR (opac-web phía server)** gọi gateway nội bộ `http://gateway` (cấu hình `BackendBase` như hiện nay), có chuyển tiếp host gốc để gateway xác định tenant.
- **Client sinh từ OpenAPI** thay cho service viết tay trong `src/app/services/<domain>/`. Service viết tay được giữ lại làm adapter trong thời gian port để giảm sửa component.
- **Lỗi:** interceptor đọc `problem+json.code` và hiển thị thông điệp đã dịch. Hai mã chuẩn:
  - `MODULE_NOT_LICENSED`: hiện trang "Đơn vị chưa đăng ký phân hệ".
  - Lỗi một phần từ BFF (ví dụ MyLibrary thiếu khối phí): hiển thị khối "tạm thời không khả dụng", không làm hỏng cả trang.

## 5. Xác thực

- `shared/auth` dùng **OIDC Authorization Code + PKCE** với `identity`:
  - Admin và OPAC là hai client riêng.
  - Token lưu **trong bộ nhớ**; refresh token rotation do thư viện quản lý (ví dụ `angular-oauth2-oidc`).
  - Bỏ cách lưu JWT trong `localStorage` như hiện tại.
- **OPAC:** trang đăng nhập bạn đọc hiển thị đúng nguồn xác thực của đơn vị (nội bộ, LDAP hoặc API ngoài; [05 §1](05-bao-mat.md#luồng-grant-type)).
- **SSE (chat AI) và tải file bảo vệ:** xin **ticket** một lần, rồi mở `EventSource` hoặc link với `?t=`.

## 6. Ẩn/hiện theo license module

- Khi khởi động, app gọi `GET /api/{opac|admin}/tenant/features`, trả về `{ modules: [...], quotas: {...}, branding: {...} }`. Kết quả được cache trong `FeatureService` (signal).
- **Ba nơi áp dụng:**
  - Route guard `canMatch: [requireModule('CIRCULATION')]`.
  - Menu admin lọc theo module **và** quyền.
  - Khối OPAC (chat AI, đặt phòng, thanh toán, thư viện số) chỉ hiển thị khi có module.
- **Đây chỉ là trải nghiệm người dùng.** Việc chặn thật nằm ở gateway và service ([04 §3](04-du-lieu-va-tenant.md#3-license-module-bán-theo-module)).

## 7. Chuyển đổi code hiện có

| Hiện tại | Đích |
|---|---|
| `frontend/src/app/opac/**` | `apps/opac` + `libs/opac/feature-*` |
| `frontend/src/app/pages/admin/**` | `apps/admin` + `libs/admin/feature-*` (chia theo service ở [02](02-phan-ra-service.md)) |
| `frontend/src/app/components/face-capture` | `libs/shared/ui/face-capture` (dùng cho cả admin/kiosk và OPAC) |
| `frontend/src/app/services/<domain>/*.service.ts` | Adapter mỏng trên `libs/api/<service>`; bỏ dần |
| `opac/config.ts` (`APP_CONFIG`, `CHATBOT_API_BASE`) | `shared/tenant` + biến môi trường lúc chạy (`/assets/env.json`), không build lại theo môi trường |
| Thiếu `frontend/Dockerfile` (compose tham chiếu nhưng không có) | Mỗi app có Dockerfile riêng, build trong CI theo `nx affected` |
