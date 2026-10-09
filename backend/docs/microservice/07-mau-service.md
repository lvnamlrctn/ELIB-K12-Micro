# 07 — Mẫu service và quy ước code

## 1. Cấu trúc monorepo mới

Repo mới có tên đề xuất `elib-platform`, tách khỏi repo monolith.

```
elib-platform/
├── src/
│   ├── BuildingBlocks/                 # NuGet nội bộ, version chung
│   │   ├── Elib.BuildingBlocks.Domain          # BusinessRuleException/NotFoundException — không phụ thuộc gì, Domain dùng được
│   │   ├── Elib.BuildingBlocks.Hosting         # AddElibServiceDefaults(): wiring chuẩn mọi service
│   │   ├── Elib.BuildingBlocks.Tenancy         # ITenantContext, TenantJobRunner, RLS interceptor
│   │   ├── Elib.BuildingBlocks.Authorization   # JWT/JWKS, [Permission], [RequiresModule], IPermissionChecker
│   │   ├── Elib.BuildingBlocks.Persistence     # EF base DbContext, soft delete, PublicId, audit fields, query filter
│   │   ├── Elib.BuildingBlocks.Messaging       # MassTransit + outbox/inbox, envelope, consumer base idempotent
│   │   ├── Elib.BuildingBlocks.Crud            # CrudController<TEntity,TSearch,TRequest> (kế thừa GenericController)
│   │   ├── Elib.BuildingBlocks.AdminTasks      # tác vụ hàng loạt theo chunk (port AdminTask hiện có)
│   │   ├── Elib.BuildingBlocks.Storage         # MinIO client, presigned URL
│   │   ├── Elib.BuildingBlocks.Observability   # OpenTelemetry, Serilog → OTLP, che PII
│   │   ├── Elib.BuildingBlocks.Resilience      # Polly pipelines chuẩn, gRPC client factory
│   │   └── Elib.BuildingBlocks.Testing         # Testcontainers fixtures, tenant-leak test harness
│   ├── Contracts/
│   │   ├── Elib.Contracts.Events               # integration events (record), semver
│   │   └── Elib.Contracts.Grpc                 # file .proto
│   ├── Gateway/Elib.Gateway                    # YARP + BFF
│   ├── Services/
│   │   ├── Identity/ Tenant/ Patron/ Notification/ Media/ Audit/
│   │   ├── Catalog/ Holdings/ Circulation/ Acquisition/ Serials/
│   │   ├── Digital/ (+ digital-indexer/ Python) Search/
│   │   └── Payment/ Space/ Ai/ Portal/ Reporting/ School/
│   └── Hosts/                                  # host gộp cho profile tỉnh
│       ├── Elib.PlatformHost                   # tenant + notification + audit + media
│       └── Elib.BackofficeHost                 # acquisition + serials + school + reporting
├── web/                                        # Nx workspace (xem 08)
├── deploy/
│   ├── charts/elib-service/                    # library chart
│   ├── charts/<service>/values.yaml
│   └── argocd/<env>/
├── tools/elibctl/                              # CLI vận hành: replay queue, reindex, provision tenant
└── docs/                                       # chuyển bộ tài liệu này sang đây khi khởi tạo repo
```

## 2. Cấu trúc một service

```
src/Services/Circulation/
├── Elib.Circulation.Api/            # Program.cs, endpoints/controllers, gRPC services, DI
├── Elib.Circulation.Application/    # use case (command/query handler), validator, DTO, mapping
├── Elib.Circulation.Domain/         # entity, value object, domain rule (chính sách mượn, tính phạt)
├── Elib.Circulation.Infrastructure/ # CirculationDbContext, migrations, consumers, replica, jobs, external clients
└── tests/
    ├── Elib.Circulation.UnitTests/
    └── Elib.Circulation.IntegrationTests/   # Testcontainers + WebApplicationFactory
```

- **Domain** không phụ thuộc EF, ASP.NET hay MassTransit. Quy tắc nghiệp vụ phức tạp được port từ monolith vào đây và có unit test riêng, ví dụ `FineTicketService`, chính sách lưu thông, lịch phòng theo khung 30 phút.
- **Application** dùng handler thuần (`ICommandHandler<T>` tự viết hoặc Mediator source-generator). Không bắt buộc MediatR.
- **Vertical slice cho nghiệp vụ lớn:** `Features/Loans/CreateLoan.cs` gom request, validator, handler và endpoint trong một file. CRUD danh mục dùng `Crud` building block.
- `Program.cs` mẫu:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddElibService("circulation", o =>
{
    o.UseDbContext<CirculationDbContext>();          // Postgres + tenancy + RLS + outbox
    o.UseMessaging(x => x.AddConsumersFromAssemblyContaining<LoanCreatedConsumer>());
    o.UseHangfire();                                 // storage trong DB của service
    o.RequireModule("CIRCULATION");
});
builder.Services.AddCirculationApplication();
var app = builder.Build();
app.MapElibDefaults();                               // /healthz, /ready, /metrics, /internal/export, OpenAPI
app.MapCirculationEndpoints();
app.Run();
```

## 3. Port CRUD từ monolith

Phần lớn ~2.330 endpoint là CRUD danh mục qua `GenericController<TEntity,TSearch,TRequest>`. `Elib.BuildingBlocks.Crud` tái hiện **đúng 8 endpoint chuẩn** và giữ quy ước của `backend/docs/API_PATTERN.md` và `CONVENTIONS.md`: soft delete (`IsDelete`), `PublicId`, phân trang, định dạng response. Có hai mục đích:

- Port mỗi controller danh mục chỉ mất vài phút: copy entity, `TSearch`, `TRequest`, rồi khai báo `[Permission]`.
- Frontend Admin dùng lại được service Angular hiện có, chỉ đổi base path.

Quy trình port một controller (áp dụng cho checklist ở [02 Phụ lục A](02-phan-ra-service.md#phụ-lục-a--ánh-xạ-controller)):

1. Chuyển entity sang `Domain` của service đích. Bỏ navigation property trỏ sang entity của service khác, thay bằng `xxxId` + snapshot nếu cần hiển thị.
2. Thay các truy vấn `_db.<DbSet của service khác>` bằng bản sao cục bộ ([04 §4](04-du-lieu-va-tenant.md#4-bản-sao-dữ-liệu-tham-chiếu)), event, hoặc gRPC trong danh sách được phép ([03 §4](03-giao-tiep.md#4-truy-vấn-đồng-bộ-grpc-nội-bộ)).
3. Bỏ mọi lọc `TenantId` thủ công; global filter + RLS lo việc này.
4. Ghi dữ liệu có hệ quả ngoài service thì phát event qua outbox.
5. Viết integration test cho endpoint, và chạy tenant-leak test.
6. Đánh dấu ✅ trong checklist parity.

## 4. Quy ước

| Hạng mục | Quy ước |
|---|---|
| Route | `/api/{resource}` bên trong service; gateway thêm tiền tố `/api/admin/{service}` hoặc `/api/opac/{service}` |
| API | REST + OpenAPI 3 (sinh client TypeScript cho frontend); lỗi theo `application/problem+json` với `code` ổn định (ví dụ `LOAN_LIMIT_EXCEEDED`) để frontend dịch |
| Đa ngôn ngữ | Giữ `Accept-Language` như hiện tại; thông điệp lỗi dịch ở service qua resource file |
| Thời gian | Lưu UTC (`timestamptz`); múi giờ đơn vị lấy từ `TenantReplica` khi tính hạn trả và khung giờ phòng |
| Đặt tên DB | `snake_case` (EFCore.NamingConventions); schema `public` + `hangfire` + `masstransit` |
| Phiên bản API | Thêm field thì không cần version. Thay đổi phá vỡ thì thêm `/v2` song song tối thiểu một chu kỳ phát hành |
| Cấu hình | `appsettings.json` chỉ chứa giá trị không nhạy cảm; secret qua biến môi trường từ External Secrets |
| Ngôn ngữ trong code | Tên kỹ thuật tiếng Anh; giữ tên nghiệp vụ quen thuộc của thủ thư khi không có từ tương đương rõ ràng (ví dụ `KiemKe` → `Stocktake`, nhưng ghi alias trong comment) |

## 5. Kiểm thử

| Loại | Phạm vi | Công cụ |
|---|---|---|
| Unit | Domain rules: chính sách mượn, phạt, khung giờ phòng, quyền | xUnit, FluentAssertions |
| Integration | Endpoint + DB + RabbitMQ thật | WebApplicationFactory, Testcontainers |
| Contract | Event (schema JSON so với bản đã phát hành) và gRPC (`buf breaking`) | Bước kiểm tra trong CI |
| Tenant-leak | Mọi endpoint của service | `Elib.BuildingBlocks.Testing` |
| E2E | Kịch bản nghiệp vụ xuyên service trên staging | Playwright (UI) + k6 (API) |
| Load | Mượn/trả giờ cao điểm, OPAC, AI chat | k6, chạy trước mỗi mốc go-live |
| Chaos | Tắt `ai`/`payment`/`search`/`notification`, kiểm tra nghiệp vụ lõi | Chaos Mesh (staging) |
