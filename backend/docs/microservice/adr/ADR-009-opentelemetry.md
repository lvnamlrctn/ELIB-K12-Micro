# ADR-009 — OpenTelemetry + Grafana (Prometheus, Loki, Tempo)

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Trong hệ phân tán, cần lần theo một request qua gateway, các service và consumer, và lọc được theo đơn vị. Tài liệu đề xuất cũ dùng Seq, chỉ phủ log. Monolith chỉ có `ILogger` thuần.

## Quyết định
- Mọi service dùng OpenTelemetry SDK (traces, metrics, logs) qua building block `Observability`, gửi OTLP tới OTel Collector.
- Lưu trữ: Prometheus (metrics), Loki (logs), Tempo (traces). Hiển thị và cảnh báo bằng Grafana + Alertmanager.
- Mọi tín hiệu gắn `tenant.id` và `correlation.id`.

## Hệ quả
- (+) Một chuẩn mở cho cả ba loại tín hiệu. Đổi backend mà không phải sửa code.
- (+) Trace xuyên qua RabbitMQ (MassTransit hỗ trợ sẵn).
- (−) Stack quan sát phải vận hành thêm. Profile tỉnh dùng cấu hình gọn (single-binary Loki/Tempo, retention ngắn).
