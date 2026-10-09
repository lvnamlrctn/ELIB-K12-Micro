# ADR-003 — Cô lập tenant: shared DB + global query filter + RLS

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Hệ phục vụ hàng trăm đến hàng nghìn đơn vị, mỗi đơn vị có dữ liệu nhỏ. Hiện tại tenant được lọc thủ công (`BaseRepository.ApplyTenantFilter` bằng reflection) và 69 controller truy vấn DbContext trực tiếp, nên rủi ro rò rỉ cao. Database-per-tenant thì quá tốn kém để vận hành ở quy mô này.

## Quyết định
- Trong mỗi service: shared schema, cột `tenant_id` bắt buộc trên mọi bảng nghiệp vụ.
- Bảo vệ bằng EF Core global query filter + interceptor gán/khoá `TenantId`, cộng thêm **PostgreSQL Row-Level Security** dựa trên `SET app.tenant_id`.
- Đọc chéo đơn vị (cấp Sở) chỉ qua `tenant_scope` trong token và chỉ cho truy vấn đọc.
- Bộ tenant-leak test bắt buộc trong CI.

## Hệ quả
- (+) Hai lớp độc lập (ứng dụng + DB). Lỗi ở một lớp không làm lộ dữ liệu.
- (+) Chi phí vận hành thấp, khởi tạo đơn vị mới nhanh (chỉ cần seed).
- (−) RLS cần quản lý connection cẩn thận: `SET` mỗi lần mở connection hoặc lấy từ pool; khi dùng PgBouncer phải dùng `SET LOCAL` trong transaction.
- (−) Một đơn vị cực lớn có thể ảnh hưởng hiệu năng chung. Có thể tách riêng cluster sau mà không đổi code.
