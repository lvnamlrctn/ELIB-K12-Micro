# ADR-001 — Viết lại toàn bộ thay vì strangler

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Monolith có 306 controller, một DbContext 242 bảng, coupling dày: `Readers` bị 44 file truy vấn, `Barcodes` 43, `BibXmls` 42. Có hai cách đi tới microservice:
- **Strangler:** tách dần từng phần. Cách này buộc phải giữ DB chung rất lâu, hoặc đồng bộ hai chiều giữa monolith và service mới.
- **Viết lại.**

Mục tiêu đã chốt là scale độc lập, deploy độc lập, cô lập lỗi và bán theo module. Hệ mới chỉ phục vụ **khách hàng mới**.

## Quyết định
Viết lại thành hệ `elib-platform` mới. Monolith tiếp tục phục vụ khách hàng hiện tại. Không migrate dữ liệu khách hàng cũ trong phạm vi này.

## Hệ quả
- (+) Thiết kế ranh giới dữ liệu sạch ngay từ đầu, không bị giới hạn bởi schema cũ.
- (+) Không cần đồng bộ hai chiều monolith ↔ service.
- (−) Có thời gian dài chưa có gì bán được. Giảm thiểu bằng MVP Sách in ở GĐ1 ([09](../09-lo-trinh.md)).
- (−) Phải bảo trì hai hệ song song. Cần backlog parity và đóng băng tính năng lớn trên monolith sau GĐ2.
- (−) Khách hàng cũ muốn chuyển sang sẽ cần một dự án ETL riêng sau này.
