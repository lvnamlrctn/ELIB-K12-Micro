# ADR-004 — RabbitMQ + MassTransit, transactional outbox/inbox

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Các service cần trao đổi event tin cậy: không mất event khi DB đã commit, và không xử lý trùng gây sai số liệu. Tải của nghiệp vụ thư viện ở mức vừa. Đội ngũ dùng .NET. Không có nhu cầu replay luồng event dài hạn kiểu Kafka.

## Quyết định
- RabbitMQ (quorum queues, chạy bằng RabbitMQ Cluster Operator) làm message broker.
- MassTransit **v8** (Apache 2.0) — ghim 8.5.x. Từ v9 MassTransit dùng license thương mại; nâng cấp là quyết định mua license riêng.
- MassTransit làm thư viện: EF Core outbox cho publisher, inbox để loại trùng cho consumer, saga state machine cho quy trình nhiều bước (thanh toán, khởi tạo đơn vị).

## Hệ quả
- (+) Đảm bảo at-least-once, có outbox để không mất event, có công cụ retry/dead-letter sẵn.
- (+) Đơn giản để vận hành ở profile tỉnh.
- (−) Không lưu event lâu dài để replay. Dựng lại read model bằng endpoint `/internal/export` của service nguồn.
- (−) Consumer phải idempotent. Building block `Messaging` cung cấp base class bắt buộc.
