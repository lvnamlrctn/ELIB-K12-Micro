# ADR-002 — Database-per-service trên PostgreSQL / CloudNativePG

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Có hai yêu cầu: deploy độc lập và cô lập lỗi. Cả hai đòi hỏi mỗi service đổi được schema mà không ảnh hưởng service khác. Monolith hiện chạy PostgreSQL, nhưng vẫn mang gánh nặng hỗ trợ SQL Server và schema bị trôi giữa hai DB.

## Quyết định
- Mỗi service có một database và role riêng trên PostgreSQL 16, vận hành bằng CloudNativePG.
- Không join chéo, không dblink/FDW.
- Schema quản lý bằng EF Core migrations, chạy qua Kubernetes Job trước khi rollout.
- Hệ mới chỉ hỗ trợ PostgreSQL.

## Hệ quả
- (+) Ranh giới rõ, migrate độc lập, scale hoặc tách cluster cho service nóng.
- (+) Bỏ được DDL viết tay lúc khởi động và lệch schema giữa hai loại DB.
- (−) Không còn truy vấn join liên phân hệ. Cần bản sao cục bộ ([ADR-007](ADR-007-local-replica.md)) và `reporting` cho báo cáo tổng hợp.
- (−) Nhiều database hơn phải backup và giám sát. CNPG tự động hoá phần này.
