# 01 — Hiện trạng và mục tiêu

## 1. Hiện trạng (monolith `ELIBAPI.sln`)

| Hạng mục | Thực tế trong code |
|---|---|
| Project | `ELIBAPI.API` (controller, filter, middleware), `ELIBAPI.Core` (entity, DTO, interface), `ELIBAPI.Infrastructure` (DbContext, repository, ~80 service, job). `ELIBAPI.Framwork` rỗng |
| Quy mô API | 306 controller nghiệp vụ, ~2.330 endpoint; phần lớn CRUD qua `GenericController<TEntity,TSearch,TRequest>` (8 endpoint chuẩn) |
| Dữ liệu | **1** `ELIBAPIDbContext`, **242 DbSet**, 9 nhóm schema (cms, dbo, ebook, eoffice, printbook, evaluate, map, gamification, payment) |
| Database | PostgreSQL (đang dùng) và SQL Server (vẫn hỗ trợ). Với Postgres, schema được đổi bằng DDL viết tay trong `Program.cs`, `RoomBookingSchema.cs`, `PaymentSchema.cs`; với SQL Server, bằng 61 EF migration |
| Multi-tenant | Shared DB, cột `TenantId`, lọc thủ công trong `BaseRepository.ApplyTenantFilter` bằng reflection. **69 controller inject `ELIBAPIDbContext` trực tiếp** và phải tự lọc tenant |
| Tenant public | Lấy theo host `<madonvi>.thuvientn.vn` (`PublicHostTenantFilter`) |
| Auth | JWT HS256 tự phát; `[Permission("MODULE","action")]` + claim `perm`/`pstamp`; token còn được nhận qua `?token=`; LDAP/API ngoài cho bạn đọc (`ExternalReaderAuth`) |
| Job | Hangfire (DB chung), 13 recurring job: PurgeDeletedRecords, ScheduledReportEmail, BookRequestExpiry, EbookLoanExpiry, DueSoonReminder, RoomBookingExpiry, PaymentExpiry, BadgeEvaluation, SavedSearchAlert, SearchObservationPurge, ZebraExport, DigitalStorageAudit, AdminTaskRetention; cộng `AdminTaskWorker` (HostedService) |
| Cache | `IMemoryCache` (27 file) — không chia sẻ được giữa nhiều instance |
| Tích hợp | MinIO, Elasticsearch (BM25 + kNN, failover sang DB), Zebra Z39.50, Gemini, Ollama, Email/SMS/Zalo ZNS, VNPAY/VietQR/Sepay, LDAP |
| Frontend | 1 app Angular 21 SSR chứa cả OPAC (`/`) và Admin (`/admin`) |
| Dịch vụ phụ | `ebook-indexer` (Python) đọc thẳng DB/MinIO/ES và **dùng chung AES key hard-code** với `AesEncryptionHelper.cs` |

### Điểm coupling (số file truy vấn trực tiếp từng DbSet)

| DbSet | File | Domain đang chạm vào |
|---|---|---|
| `Readers` | 44 | Lưu thông, ebook, đặt phòng, thanh toán, chat, huy hiệu, MyLibrary, ra vào |
| `Barcodes` | 43 | Kho, lưu thông, bổ sung, kiểm kê, thanh lý |
| `BibXmls` | 42 | Biên mục, tìm kiếm, lưu thông, báo cáo |
| `Bibs` | 30 | Biên mục, kho, OPAC |
| `Stores` | 20 | Kho, lưu thông, báo cáo |
| `Users` | 19 | Auth, nhật ký, phân quyền |

Ví dụ điển hình:
- `CirculationLoanController` đọc 13 bảng của 5 domain.
- `FineTicketService` nối lưu thông, biên mục, bạn đọc và thanh toán.

**Hệ quả.** Viết lại thành microservice thì bắt buộc phải định nghĩa lại quyền sở hữu dữ liệu ([02](02-phan-ra-service.md)) và thay các join chéo domain bằng event + bản sao cục bộ ([03](03-giao-tiep.md)). Không thể bê nguyên code sang.

### Vấn đề hiện tại mà kiến trúc mới phải giải quyết

1. **Không scale riêng phần được:** OPAC tra cứu, trang đọc ebook và AI chat (SSE, gọi LLM lâu) chạy chung process với nghiệp vụ quản trị.
2. **Lỗi lan rộng:** Gemini/Ollama chậm, ES sập hoặc job nặng đều làm ảnh hưởng toàn bộ API.
3. **Phát hành chung:** sửa một phân hệ thì phải deploy lại cả hệ thống.
4. **Không bán theo module ở mức kỹ thuật:** module chỉ là ẩn/hiện menu và quyền; mọi đơn vị vẫn chạy toàn bộ code và schema.
5. **Rủi ro rò rỉ tenant:** việc lọc `TenantId` phụ thuộc vào kỷ luật từng controller.
6. **Schema trôi:** DDL viết tay cho Postgres và EF migration cho SQL Server có thể lệch nhau.

## 2. Mục tiêu

| Mục tiêu | Thước đo |
|---|---|
| **Scale độc lập** | `opac-web`, `search`, `ai`, `digital`, `circulation` có HPA riêng; tăng tải OPAC không làm tăng tài nguyên `acquisition` |
| **Deploy độc lập** | Mỗi service có pipeline, image và Helm release riêng; deploy một service không cần deploy lại service khác (contract có version) |
| **Cô lập lỗi** | Tắt `ai`, `payment`, `search` hay `notification` thì mượn/trả, biên mục và đăng nhập vẫn chạy (kiểm bằng chaos test ở GĐ4) |
| **Bán theo module** | Đơn vị chưa mua module nào thì gateway trả 403 cho các route của module đó; UI ẩn module; job của module không chạy cho đơn vị đó |
| **Cô lập tenant** | 100% bảng nghiệp vụ có RLS; bộ test tự động truy cập chéo tenant đều thất bại |

## 3. Non-goals

- **Không migrate dữ liệu** khách hàng đang dùng monolith. Hệ mới chỉ triển khai cho khách hàng mới ([ADR-001](adr/ADR-001-viet-lai.md)).
- **Không bỏ SQL Server ngay trong code mới:** hệ mới chỉ hỗ trợ PostgreSQL.
- **Không tách nhỏ hơn mức bounded context.** Ví dụ: danh mục MARC không tách thành service riêng mà nằm trong `catalog`.
- **Không đổi nghiệp vụ.** Quy tắc mượn/trả, phạt, MARC, chính sách số… giữ nguyên ngữ nghĩa của monolith; đây là checklist parity trong [02](02-phan-ra-service.md#phụ-lục-a--ánh-xạ-controller).

## 4. Ràng buộc

- **Đội ngũ .NET:** giữ .NET và EF Core; chỉ `digital-indexer` (OCR/PDF) giữ Python.
- **Song song hai hệ:** monolith vẫn nhận tính năng mới (kiểu các đợt "port LRC"). Mọi tính năng mới của monolith phải được ghi vào backlog parity ([09](09-lo-trinh.md)).
- **Triển khai:** khách hàng là sở/phòng giáo dục và trường học, cần hai profile Kubernetes: SaaS tập trung và cụm nhỏ theo tỉnh ([06](06-nen-tang-k8s.md#2-profile-triển-khai)).
- **Tích hợp ngoài phải giữ:** SIP2 (máy mượn trả tự động), Z39.50, VNPAY/VietQR/Sepay (URL webhook ổn định), LDAP của từng đơn vị, thiết bị kiểm soát ra vào (`/api/access`).
