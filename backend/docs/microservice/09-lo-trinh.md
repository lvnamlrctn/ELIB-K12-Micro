# 09 — Lộ trình

## 1. Nguyên tắc

- **Đi theo gói bán.** Mỗi giai đoạn kết thúc bằng một tập module **bán được** cho khách hàng mới, không phải một tập service "xong kỹ thuật".
- **Monolith tiếp tục phục vụ khách hàng hiện tại** suốt quá trình. Không ETL dữ liệu ([ADR-001](adr/ADR-001-viet-lai.md)).
- **Checklist parity** là [02 Phụ lục A](02-phan-ra-service.md#phụ-lục-a--ánh-xạ-controller). Một module chỉ được bán khi 100% controller của nó đã port và có test.
- **Backlog parity:** mọi tính năng mới thêm vào monolith trong thời gian viết lại (kiểu các đợt "port LRC") phải mở ticket parity tương ứng cho hệ mới **cùng lúc** với khi merge vào monolith.
- **Ước lượng thời gian** chốt sau GĐ0, dựa trên tốc độ thực tế port controller CRUD và nghiệp vụ. Kích thước tương đối từng giai đoạn tính theo số controller ở bảng dưới.

## 2. Các giai đoạn

| GĐ | Mục tiêu | Service | Controller cần port |
|---|---|---|---|
| 0 | Nền tảng chạy được end-to-end | gateway, identity, tenant, notification, audit, media | 37 |
| 1 | **MVP bán được: Sách in cơ bản + OPAC tra cứu** | patron, catalog, holdings, circulation, search (+ BFF MyLibrary bản cơ bản) | 119 |
| 2 | Đủ phân hệ chính | acquisition, serials, digital, payment, portal, reporting | 103 |
| 3 | Mở rộng | space, ai, school | 47 |
| 4 | Sẵn sàng go-live diện rộng | (hardening) | — |

### GĐ0 — Nền tảng

**Deliverable**
- Repo `elib-platform` theo [07 §1](07-mau-service.md#1-cấu-trúc-monorepo-mới), Nx workspace rỗng có `opac` và `admin` (đăng nhập, layout, menu theo license).
- Building blocks: Hosting, Tenancy (+ RLS), Authorization, Persistence, Messaging (outbox/inbox), Crud, Observability, Resilience, Testing.
- `Elib.Contracts` v0.x, quy trình kiểm tra tương thích contract trong CI.
- Cluster dev + staging. Operator CNPG, RabbitMQ, Redis, ECK, MinIO. Stack quan sát Grafana. ArgoCD. Library chart `elib-service`.
- Service: `gateway` (route động, tenant theo host, license, rate limit), `identity` (OpenIddict, nhân viên + bạn đọc nội bộ, quyền, OTP/captcha), `tenant` (đơn vị, tổ chức, tham số, license, **saga khởi tạo đơn vị**), `notification` (email trước; SMS/Zalo ở GĐ2), `audit`, `media`.

**Tiêu chí hoàn thành**
- Tạo một đơn vị mới bằng UI, saga chạy xong, admin đầu tiên đăng nhập được qua `<madonvi>.thuvientn.vn/admin`, và thấy đúng menu theo license.
- Tenant-leak test chạy xanh trong CI; trace từ gateway tới service tới consumer hiển thị trên Grafana.

**Trạng thái (2026-10-09, môi trường DEV docker-compose)**
- ✅ Tạo đơn vị bằng UI → saga → admin đầu tiên đăng nhập trên host đơn vị, menu theo license (e2e `deploy/dev/e2e_gd0.py`).
- ✅ Tenant-leak test (`src/Tests/Elib.Platform.LeakTests`) chạy trên PostgreSQL thật trong CI (`.github/workflows/ci.yml`).
- ✅ Kiểm tra tương thích contract: `src/Contracts/tests` so với `contracts.snapshot.txt` — xoá/đổi kiểu/thêm `required` làm CI đỏ.
- ✅ OpenTelemetry (building block Observability, bật bằng `OTEL_EXPORTER_OTLP_ENDPOINT`); đã thấy trace gateway → tenant trên Grafana/Tempo (`grafana/otel-lgtm`, profile `observability` của compose DEV — tắt mặc định vì image ~3,7 GB).
- ⏳ Cluster K8s dev/staging, operator, ArgoCD, library chart, deploy một service qua PR — chờ có hạ tầng.
- Deploy một service lên staging bằng PR trong `deploy/` mà không ảnh hưởng service khác.

### GĐ1 — MVP Sách in

**Deliverable**
- `patron`: hồ sơ, loại bạn đọc, lớp/khoá, import Excel, ảnh, in thẻ.
- `catalog`: MARC21, worksheet, từ điển, AACR2/ISBD, nhập từ Z39.50 (client nằm trong `search`).
- `holdings`: đăng ký cá biệt, kho, giao nhận, chuyển kho, kiểm kê, thanh lý, mất sách.
- `circulation`: mượn/trả/gia hạn/đặt mượn, chính sách, điểm lưu thông, phạt (thu tiền mặt), SIP2, nhắc hạn.
- `search`: index sách in, OPAC tra cứu + chi tiết, failover, Zebra export.
- OPAC: trang chủ tối thiểu, tra cứu, chi tiết, đăng nhập bạn đọc, MyLibrary (đang mượn, lịch sử, đặt mượn).
- Đăng nhập bạn đọc qua LDAP/API ngoài theo đơn vị.

**Tiêu chí hoàn thành**
- Kịch bản E2E: biên mục → đăng ký cá biệt → nhập kho → mượn → OPAC báo "đã mượn" → trả → nhắc hạn/quá hạn → phạt → thu tiền mặt.
- Load test: 300 lượt mượn/trả mỗi phút trên 200 đơn vị đồng thời, p95 < 300 ms; tra cứu OPAC 200 RPS, p95 < 800 ms.
- Chaos: tắt `search` và `notification` thì mượn/trả vẫn chạy; event được xử lý khi bật lại.
- Thử nghiệm (pilot) với 1–3 đơn vị mới.

### GĐ2 — Đủ phân hệ chính

- `acquisition`: đặt mua, nhận, ngân sách/quỹ, nhà cung cấp, báo cáo bổ sung → `ReceiptCompleted` → holdings.
- `serials`: ấn phẩm định kỳ, kỳ phát hành, đóng tập.
- `digital` + `digital-indexer`: tài liệu số, bộ sưu tập, chính sách, mượn số, trang đọc, theo dõi đọc, đánh giá, nộp tài liệu. Indexer viết lại trên nền `ebook-indexer`, nhận việc qua RabbitMQ và không còn đọc DB trực tiếp.
- `search`: index ebook + chunk/vector, tìm kiếm hợp nhất.
- `payment`: VietQR/VNPAY/Sepay, saga thu phí, đối soát, tài khoản nhận theo đơn vị; tab Phí/Nợ trên OPAC.
- `portal`: CMS đầy đủ cho OPAC.
- `reporting`: dashboard, báo cáo liên phân hệ, báo cáo định kỳ qua email; `notification` thêm SMS và Zalo ZNS.

**Tiêu chí hoàn thành:** checklist parity 100% cho các module trên; E2E thanh toán sandbox VNPAY/Sepay (gồm IPN trùng lặp, hết hạn, thu tiền mặt song song).

### GĐ3 — Mở rộng

- `space`: sơ đồ tầng, đặt phòng học nhóm (khung 30 phút, thành viên nhóm, lịch công khai), check-in (thẻ và khuôn mặt), kiểm soát ra vào, quầy tiếp đón, chìa khoá tủ.
- `ai`: RAG chat SSE (truy xuất qua `search`), trợ lý tìm tài liệu, phân tích ảnh bìa, nhận diện khuôn mặt, thống kê chat, quota theo đơn vị.
- `school`: chương trình đào tạo (Evaluate), EOffice, Gamification (huy hiệu theo event).

**Tiêu chí hoàn thành:**
- Checklist parity 100%.
- Tắt `ai` thì OPAC và check-in phòng vẫn chạy (check-in chuyển sang quét thẻ).
- Đo chi phí LLM theo tenant trên dashboard.

### GĐ4 — Sẵn sàng go-live diện rộng

- Pentest bên ngoài; rà soát OWASP ASVS L2; kiểm thử cô lập tenant thủ công trên các endpoint nhạy cảm.
- DR: diễn tập restore PITR từng service, mất một node, mất RabbitMQ.
- Runbook vận hành (`elibctl`), cảnh báo/SLO hoàn chỉnh, tài liệu triển khai profile tỉnh (k3s).
- Load test toàn hệ ở quy mô mục tiêu của một tỉnh.
- Đóng băng contract v1 (`Elib.Contracts` 1.0).

## 3. Tổ chức đội

Một nhóm platform giữ BuildingBlocks, gateway, identity, tenant và hạ tầng. Các nhóm nghiệp vụ được giao theo gói:

| Nhóm | Sở hữu |
|---|---|
| Platform | gateway, identity, tenant, notification, audit, media, BuildingBlocks, CI/CD, K8s |
| Sách in | catalog, holdings, circulation, acquisition, serials |
| Số & tra cứu | digital, digital-indexer, search, ai |
| Dịch vụ | patron, payment, space, portal, reporting, school |
| Frontend | Nx workspace, shared libs. Thành viên có thể nhúng vào từng nhóm nghiệp vụ theo feature lib |

Mỗi service có **một nhóm sở hữu duy nhất**, ghi trong `CODEOWNERS`.

## 4. Rủi ro và giảm thiểu

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Phạm vi viết lại lớn (306 controller), kéo dài, monolith tiếp tục thay đổi | Cao | Đi theo gói bán, có MVP bán được ở GĐ1; `Crud` building block giúp port nhanh danh mục; backlog parity bắt buộc |
| Đội chưa quen hệ phân tán (event, eventual consistency, saga) | Cao | BuildingBlocks che phần khó (outbox, inbox, tenancy); template service; review kiến trúc ở GĐ0; tài liệu này + ADR |
| Sai lệch dữ liệu giữa bản sao và nguồn | Trung bình | Outbox/inbox, `SourceVersion`, đối soát hằng đêm, endpoint `/internal/export` để dựng lại |
| Chi phí và độ phức tạp K8s cho khách hàng on-prem cấp tỉnh | Trung bình | Profile k3s gọn, host gộp, cùng chart; đóng gói bộ cài; hỗ trợ vận hành từ xa |
| Rò rỉ dữ liệu giữa đơn vị | Cao (hậu quả) | 4 lớp bảo vệ ([04 §2.1](04-du-lieu-va-tenant.md#21-bốn-lớp-bảo-vệ)), test bắt buộc trong CI |
| Hiệu năng do nhiều hop | Trung bình | Nghiệp vụ lõi không gọi sync; tối đa 1 hop gRPC; load test mỗi giai đoạn |
| Hai hệ song song gây tốn nguồn lực bảo trì | Trung bình | Đóng băng tính năng lớn trên monolith sau GĐ2; chỉ sửa lỗi và yêu cầu bắt buộc của khách hàng hiện tại |
| Phụ thuộc LLM ngoài (chi phí, dữ liệu cá nhân) | Trung bình | Quota theo đơn vị, cache câu trả lời, bật/tắt theo đơn vị, không gửi PII không cần thiết |

## 5. Sau go-live (ngoài phạm vi hiện tại)

- **Chuyển khách hàng cũ** sang hệ mới. Không nằm trong kế hoạch này. Nếu cần sau này, công cụ ETL có thể dùng chính endpoint import và event của hệ mới, chuyển theo từng đơn vị.
- **SSO cấp Sở/Bộ** (OIDC federation) qua `identity`.
- **Kho phân tích riêng** (ClickHouse) cho `reporting` nếu khối lượng event vượt khả năng của PostgreSQL.
