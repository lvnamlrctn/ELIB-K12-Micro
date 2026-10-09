# 04 — Dữ liệu và tenant

## 1. Database-per-service

- **PostgreSQL 16** chạy qua **CloudNativePG**:
  - Profile SaaS: một cluster CNPG (1 primary + 2 replica) cho các service tải thấp và vừa; **cluster riêng** cho `circulation`, `digital` và `search`, để tách I/O khi cần.
  - Profile theo tỉnh: một cluster duy nhất.
- **Mỗi service một database và một role.** Role chỉ có quyền trên database của mình. Không có user dùng chung, không có `dblink`/FDW giữa các DB, và **không join chéo service**.
- **Schema được quản lý bằng EF Core migrations cho PostgreSQL.** Bỏ hẳn kiểu DDL viết tay lúc khởi động như `Program.cs`, `RoomBookingSchema.cs`, `PaymentSchema.cs` hiện nay. Migration chạy bằng **Kubernetes Job** (Helm pre-upgrade hook) trước khi rollout. App không tự migrate.
- **Thay đổi schema không phá vỡ** (expand → migrate → contract): thêm cột mới và ghi song song; deploy code đọc cột mới; xoá cột cũ ở bản phát hành sau.
- **Backup:**
  - CNPG backup liên tục (WAL archiving) lên MinIO/S3, giữ PITR 14 ngày.
  - Snapshot hằng ngày, giữ 30 ngày.
  - Mỗi quý chạy diễn tập restore từng service.
- **Khoá chính** dùng `long` (identity/sequence) để tương thích tư duy hiện tại. Riêng `PublicId (Guid v7)` dùng cho URL và tham chiếu chéo service khi cần ẩn số tuần tự, giữ đúng quy ước PublicId hiện nay.

## 2. Mô hình tenant

Đơn vị (tenant) là thư viện của một trường, phòng hoặc sở. Mô hình: **shared database, shared schema, cột `TenantId`** trong từng service. Lý do chọn mô hình này: số đơn vị lớn (hàng trăm trường mỗi tỉnh) và dữ liệu mỗi đơn vị nhỏ, nên database-per-tenant không đáng chi phí vận hành ([ADR-003](adr/ADR-003-tenant-isolation.md)).

### 2.1 Bốn lớp bảo vệ

| Lớp | Cơ chế | Thay cho hiện tại |
|---|---|---|
| 1. Gateway | Tenant xác định theo host/token; header từ client bị loại bỏ | `PublicHostTenantFilter` |
| 2. Ứng dụng | `ITenantContext` (scoped) lấy từ claim `tenant_id`. **EF Core global query filter** tự động cho mọi entity `ITenantOwned`. `SaveChanges` interceptor tự gán `TenantId` khi insert và chặn sửa `TenantId` | `BaseRepository.ApplyTenantFilter` bằng reflection, và 69 controller tự lọc |
| 3. Database | **PostgreSQL RLS** trên mọi bảng có `tenant_id`: `USING (tenant_id = current_setting('app.tenant_id')::bigint OR current_setting('app.cross_tenant', true) = 'on')`. Interceptor `DbConnection` chạy `SET app.tenant_id` khi mở connection | (không có) |
| 4. Kiểm thử | Bộ test "tenant-leak" trong CI cho mọi service: tạo dữ liệu 2 tenant, gọi mọi endpoint bằng token tenant A, khẳng định không thấy dữ liệu tenant B | (không có) |

### 2.2 Các trường hợp đặc biệt

- **Dữ liệu dùng chung** (`TenantId = null`), ví dụ danh mục MARC chuẩn, từ điển quốc gia/ngôn ngữ, template thông báo mặc định:
  - Đặt ở bảng **riêng** với hậu tố `_shared`, không trộn `null` vào bảng nghiệp vụ.
  - Đơn vị có thể ghi đè bằng bản riêng (truy vấn theo thứ tự: tenant trước, shared sau).
- **Quyền đọc chéo đơn vị** (cấp Sở/Phòng xem các trường trực thuộc; thay `ReadOnlyPolicy` hiện nay):
  - Token mang claim `tenant_scope = [list]` hoặc `tenant_scope = org:<orgId>` do `identity` tính từ cây `Orgs` của `tenant`.
  - Service bật `app.cross_tenant` **chỉ cho truy vấn đọc** qua `ITenantContext.ReadAcross(scope)`, và filter đổi thành `tenant_id = ANY(scope)`.
  - Mọi thao tác ghi luôn theo một tenant duy nhất.
- **Super-admin hệ thống** không có `tenant_id`, nên chỉ truy cập API quản trị nền tảng (`tenant`, `identity`, giám sát). Muốn thao tác nghiệp vụ cho một đơn vị thì phải "đóng vai đơn vị": token có thời hạn ngắn, được ghi audit.
- **Job nền** chạy theo từng tenant. Building block `TenantJobRunner` lặp qua các tenant có license module tương ứng và đặt `ITenantContext` cho từng lượt (thay `DbContext.BackgroundTenantId`).
- **Consumer event** lấy `TenantId` từ envelope và đặt `ITenantContext` trước khi xử lý.

## 3. License module (bán theo module)

```
tenant DB:
  Modules(Code PK, Package, Name)                  -- CIRCULATION, CATALOG, DIGITAL, PAYMENT, SPACE, AI, PORTAL, SCHOOL…
  TenantModuleLicenses(TenantId, ModuleCode, Status[Trial|Active|Suspended|Expired], ValidFrom, ValidTo, Quota JSON)
```

- **Gói bán ánh xạ sang service** theo [02 §2](02-phan-ra-service.md#2-danh-sách-service). Một gói có thể gồm nhiều module code, ví dụ gói Sách in gồm `CATALOG`, `HOLDINGS`, `CIRCULATION`, `ACQUISITION`, `SERIALS`.
- **Nơi áp dụng:**
  1. **Gateway:** route metadata `module`; cache license theo tenant trong Redis (TTL 5 phút + xoá bằng `ModuleLicenseChanged`).
  2. **Service:** attribute `[RequiresModule("CIRCULATION")]` cho consumer và job, để không xử lý event hay chạy job cho tenant không có license.
  3. **Frontend:** API `GET /api/opac/tenant/features` và `/api/admin/tenant/features` trả về module + quota để ẩn/hiện menu ([08](08-frontend.md)).
- **Quota** (tuỳ chọn): ví dụ số câu hỏi AI/tháng hoặc dung lượng thư viện số. Service sở hữu đếm và chặn; quota lấy từ `TenantReplica`.
- **Hết hạn license:** chuyển sang chế độ **chỉ đọc** 30 ngày (gateway chặn POST/PUT/DELETE), sau đó mới chặn hẳn. Không bao giờ xoá dữ liệu tự động.
- **Phụ thuộc giữa module** được kiểm tra khi bán. Ví dụ `CIRCULATION` cần `CATALOG` + `HOLDINGS`; `AI` cần `SEARCH`; `PAYMENT` có ích khi có `CIRCULATION` hoặc `SPACE`.

## 4. Bản sao dữ liệu tham chiếu

| Bảng bản sao | Ở service | Nguồn / event | Field |
|---|---|---|---|
| `TenantReplica` | tất cả | tenant / `TenantProvisioned`, `TenantUpdated`, `ModuleLicenseChanged` | id, mã, tên, trạng thái, module, quota, múi giờ |
| `PatronReplica` | circulation, digital, space, payment, school | patron / `Reader*` | id, publicId, số thẻ, họ tên, loại bạn đọc, lớp/khoá, hạn thẻ, trạng thái khoá, email/điện thoại (chỉ khi service gửi tin) |
| `ItemReplica` | circulation | holdings / `Item*`, kèm snapshot từ `BibUpdated` | barcode, bibId, kho, điểm lưu thông, loại tài liệu (cho chính sách), trạng thái vật lý, nhan đề/tác giả (hiển thị) |
| `BibSnapshot` | holdings, acquisition, digital | catalog / `Bib*` | bibId, nhan đề, tác giả, ISBN, ký hiệu phân loại, ảnh bìa |
| `UserSnapshot` | audit, reporting | identity / `User*` | id, họ tên, tài khoản |

Quy tắc chung cho mọi bảng bản sao:
- **Chỉ đọc** trong service giữ bản sao; chỉ consumer được ghi.
- Có cột `SourceVersion` và `SyncedAt`.
- Lệch thì dựng lại qua `/internal/export` ([03 §6](03-giao-tiep.md#6-xử-lý-lỗi-và-độ-bền)).
- **Dữ liệu cá nhân:** chỉ sao chép field thực sự cần. Xoá bạn đọc (`ReaderDeleted`) thì mọi bản sao phải xoá hoặc ẩn danh.

## 5. Cache — Redis

| Key | TTL | Xoá khi |
|---|---|---|
| `tenant:host:{host}` → tenantId | 1h | `TenantUpdated` |
| `tenant:{id}:licenses` | 5 phút | `ModuleLicenseChanged` |
| `tenant:{id}:params:{service}` | 30 phút | `SystemParameterChanged` |
| `perm:{userId}:{stamp}` | 30 phút | `PermissionChanged` |
| `jwt:revoked:{jti}` | đến hạn token | Đăng xuất, đổi mật khẩu, khoá tài khoản |
| `opac:{tenant}:home` (trang chủ OPAC) | 2 phút | `NewsPublished`, `BibPublished` |
| `ai:{tenant}:answer:{hash}` | 24h | `EbookUpdated` (theo tài liệu nguồn) |

- `IMemoryCache` chỉ dùng làm L1 ngắn hạn (≤60s) phía trước Redis. Bỏ cách cache MemoryCache dài hạn như hiện nay, vì nó không đồng bộ được khi có nhiều pod.
- Cấu hình Redis Sentinel (3 node), `maxmemory-policy allkeys-lru`. Redis không phải nguồn sự thật: mất Redis thì hệ thống chạy chậm hơn nhưng không sai.

## 6. Elasticsearch, MinIO, Zebra

- **Elasticsearch** chỉ chứa read model và do `search` sở hữu toàn bộ index:
  - `{env}-bib`: sách in, có availability.
  - `{env}-ebook`.
  - `{env}-chunk`: nội dung + vector.
  - `{env}-news`: tuỳ chọn.

  Một index dùng chung cho mọi tenant, trường `tenantId` bắt buộc có trong mọi query (filter alias theo tenant cho truy vấn admin). Có thể xoá và dựng lại hoàn toàn từ service nguồn. Giữ cơ chế failover hiện có (`OpacSearchFailoverService`, `SearchCircuitBreaker`): khi ES lỗi, `search` trả kết quả rút gọn từ cache hoặc bảng tóm tắt trong DB của chính nó, **không** truy vấn DB của catalog.
- **MinIO** chia bucket theo service:
  - `digital-files` (private, chỉ truy cập qua presigned URL ngắn hạn sau khi kiểm tra chính sách số).
  - `media-public` (ảnh bìa, ảnh CMS).
  - `patron-photos` (private).
  - `reports` (private).

  Object key có tiền tố `{tenantId}/`.
- **Zebra Z39.50:** container do `search` quản lý, đọc file ISO2709 do `search` xuất tăng dần từ `BibUpdated` qua volume chung (PVC ReadWriteMany) hoặc MinIO sync.
