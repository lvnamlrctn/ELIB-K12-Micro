# ADR-008 — Job nền thuộc từng service

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Monolith chạy 13 recurring job Hangfire trên DB chung, nhiều job truy vấn dữ liệu của nhiều phân hệ. Gom job vào một "scheduler service" chung sẽ tạo lại coupling dữ liệu.

## Quyết định
- Job thuộc về service sở hữu dữ liệu nó xử lý. Ánh xạ ở [02 Phụ lục C](../02-phan-ra-service.md#phụ-lục-c--job-nền).
- Mỗi service dùng Hangfire với storage trong DB của chính nó, chạy theo từng tenant có license qua `TenantJobRunner`.
- Job có thể thay bằng event thì chuyển sang event, ví dụ huy hiệu và Zebra export.
- Tác vụ vận hành một lần chạy bằng Kubernetes Job.

## Hệ quả
- (+) Tắt hoặc gỡ một module thì job của module đó cũng tắt theo.
- (+) Lỗi job của một service không ảnh hưởng service khác.
- (−) Có nhiều dashboard Hangfire. Chúng được gom đường dẫn qua gateway `/ops/{service}/hangfire`, và metric chung được đưa lên Grafana.
