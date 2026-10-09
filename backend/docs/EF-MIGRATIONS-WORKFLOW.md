# Quy trình EF Core Migrations trong dự án ELIB (đa nhà cung cấp CSDL)

## Nguyên nhân gốc — đã xác minh thực nghiệm ngày 2026-09-12

`dotnet ef migrations add <tên>` chạy với cấu hình mặc định của repo (`DatabaseProvider: "PostgreSQL"`
trong `appsettings.json`) **sẽ luôn crash** với `NullReferenceException` sâu bên trong
`MigrationsModelDiffer` của EF Core (`Initialize(ColumnOperation...)` /
`ColumnBase.get_ProviderValueComparer()`). Đây **không phải** bug ngẫu nhiên hay do 1-2 cột bị lệch —
đã xác minh trực tiếp bằng thực nghiệm:

- Toàn bộ lịch sử migration (`ELIBAPI.Infrastructure/Migrations/*.cs`, tính đến ngày viết tài liệu này)
  được **scaffold dưới SQL Server** — mọi cột trong mọi migration đều có kiểu tường minh dạng SQL Server
  (`"nvarchar(max)"`, `"datetime2"`, `"uniqueidentifier"`, `"bit"`, ...), **0 file** có kiểu dạng Npgsql
  (`"text"`, `"timestamp"`, `"uuid"`, `"boolean"`).
- Các chuỗi kiểu này **không tồn tại trong PostgreSQL** (`CREATE TABLE x (a nvarchar(max))` báo lỗi
  `type "nvarchar" does not exist` — đã test trực tiếp trên CSDL Postgres live).
- Khi `dotnet ef migrations add` chạy với provider Npgsql đang active, bộ so khớp (differ) phải duyệt
  **toàn bộ** model (~400 entity) và tra cứu type-mapping Postgres cho từng cột đã ghi trong snapshot —
  với hầu hết cột, tra cứu trả về `null` vì Npgsql không hiểu các chuỗi kiểu SQL Server, và code nội bộ
  của EF Core không kiểm tra null trước khi dùng → `NullReferenceException`.
- Đã xác nhận: chạy `DatabaseProvider=SqlServer dotnet ef migrations add ...` (ép provider design-time
  sang SQL Server, khớp đúng "phương ngữ" mà toàn bộ lịch sử migration được viết) thì **scaffold thành
  công ngay lập tức**, không cần sửa gì thêm.
- Đã xác nhận thêm: `Database.Migrate()`/`dotnet ef database update` **không hề** chạy các file migration
  gốc (chứa kiểu SQL Server) trực tiếp lên CSDL Postgres — bảng `__EFMigrationsHistory` trên Postgres chỉ
  được cập nhật thủ công (bằng `INSERT` tay hoặc script) để "đánh dấu đã áp dụng" sau khi thay đổi schema
  thật đã được thực hiện qua các script `backend/docs/*.sql` (PHẦN 2 — PostgreSQL, dịch tay từ migration).
  Đây chính là quy ước NGẦM đã tồn tại xuyên suốt lịch sử migration của dự án, chỉ là chưa từng được ghi
  lại — từ nay áp dụng đúng quy trình dưới đây một cách có ý thức thay vì ngẫu nhiên phát hiện lại.

## Quy trình đúng khi cần thay đổi schema (thêm bảng/cột/đổi kiểu, v.v.)

1. **Sửa entity C#** (`ELIBAPI.Core/Entities/...`) như bình thường.
2. **Scaffold migration LUÔN ép provider SQL Server**, bất kể CSDL đang chạy thật của môi trường bạn là
   gì (SQL Server hay PostgreSQL) — vì toàn bộ lịch sử migration/snapshot chỉ hiểu "phương ngữ" này:
   ```bash
   cd backend
   DatabaseProvider=SqlServer dotnet ef migrations add <TenMigration> --project ELIBAPI.Infrastructure --startup-project ELIBAPI.API
   ```
   (PowerShell: `$env:DatabaseProvider='SqlServer'; dotnet ef migrations add ...`)
3. **Đọc file migration vừa sinh** (`Up()`/`Down()`) để biết chính xác các operation (CreateTable/
   AddColumn/AlterColumn/...) và kiểu cột SQL Server tương ứng.
4. **Nếu CSDL thật đang chạy là SQL Server**: chạy `dotnet ef database update` bình thường (không cần
   `DatabaseProvider=SqlServer` override vì đó vốn đã là provider mặc định của môi trường đó) — EF tự áp
   dụng đúng.
5. **Nếu CSDL thật đang chạy là PostgreSQL** (trường hợp phổ biến nhất trong môi trường làm việc hiện
   tại): **không** chạy `dotnet ef database update` (sẽ lỗi vì kiểu cột SQL Server không hợp lệ trên
   Postgres). Thay vào đó:
   a. Viết tay 1 script `backend/docs/<mo-ta>.sql` theo đúng khuôn PHẦN 1 (SQL Server, giữ nguyên/copy
      từ migration) + PHẦN 2 (PostgreSQL, dịch tay kiểu cột: `nvarchar(max)`→`text`, `datetime2`→
      `timestamp`, `uniqueidentifier`→`uuid`, `bit`→`boolean`, `nvarchar(N)`→`varchar(N)`, giữ nguyên
      `int`/`bigint`), idempotent (`IF NOT EXISTS`/`CREATE TABLE IF NOT EXISTS`).
   b. Chạy PHẦN 2 trực tiếp lên CSDL Postgres live (qua `psycopg2`, xem `reference_db_direct_access`
      trong bộ nhớ của agent, hoặc bất kỳ client Postgres nào).
   c. **Baseline migration vào `__EFMigrationsHistory` trên Postgres** — bảng này chỉ có 2 cột
      `("MigrationId" text, "ProductVersion" text)`, KHÔNG chạy `Up()` của migration, chỉ đánh dấu đã
      áp dụng để `Database.Migrate()` không cố chạy lại (và lỗi) migration đó mỗi lần khởi động app:
      ```sql
      INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
      VALUES ('<TenMigration đầy đủ, vd 20260912142434_TenMigration>', '10.0.4');
      ```
6. **Xác nhận đã khớp**: chạy lại bước 2 với 1 tên migration probe bất kỳ — `Up()`/`Down()` phải HOÀN
   TOÀN RỖNG (nghĩa là snapshot đã khớp model). Sau đó `dotnet ef migrations remove
   --project ELIBAPI.Infrastructure --startup-project ELIBAPI.API --force` (vẫn với
   `DatabaseProvider=SqlServer`) để xoá migration probe — lệnh này sẽ báo không kết nối được SQL Server
   thật (bình thường, vì SQL Server ở `103.97.134.58` có thể không truy cập được từ mọi mạng), vẫn xoá
   file + revert snapshot cục bộ thành công.

## Vì sao KHÔNG scaffold trực tiếp dưới Npgsql (mặc định của repo)

Đã thử và xác nhận: `dotnet ef migrations add` chạy với provider mặc định (Npgsql,
`DatabaseProvider=PostgreSQL` trong `appsettings.json`) **luôn crash**, kể cả khi model/snapshot đã hoàn
toàn khớp nhau (không còn thay đổi thật nào) — vì differ vẫn phải duyệt toàn bộ ~400 entity đã tồn tại từ
trước, tất cả đều mang kiểu cột SQL Server trong snapshot. Việc này sẽ không tự hết cho tới khi TOÀN BỘ
lịch sử migration được viết lại từ đầu dưới Npgsql (rebase 1 lần, rủi ro cao, không nằm trong phạm vi vá
lỗi nhanh) — vì vậy quy trình chính thức của dự án là **luôn ép SQL Server khi scaffold**, bất kể CSDL
thật đang chạy.

## Việc CHƯA làm (để lại có chủ đích, ngoài phạm vi vá nhanh)

- **Rebase toàn bộ lịch sử migration sang dual-provider chuẩn** (2 migration project/assembly riêng biệt
  cho SQL Server và PostgreSQL, đúng khuyến nghị chính thức của EF Core cho multi-provider) — rủi ro cao,
  cần 1 phiên riêng có kiểm thử kỹ trên cả 2 CSDL trước khi làm, không nên làm vội.
- Đã tìm thấy `backend/Migrations.Postgres.bak/` — dấu vết của 1 lần thử làm lịch sử migration Postgres
  riêng trước đây (chỉ có 2 migration: `InitialCreate`, `AddSerialApprovedFields`, dừng ở
  2026-07-11), rồi bị bỏ dở để quay về cách dùng chung 1 lịch sử SQL Server + script tay như hiện tại.
  Giữ nguyên thư mục `.bak` này làm tài liệu tham khảo, không xoá, không tiếp tục nếu chưa có quyết định
  rõ ràng đầu tư vào hướng dual-provider chuẩn.

## Sự cố cụ thể đã khắc phục ngày 2026-09-12

Trong lúc triển khai Đợt 6 (SMS/Zalo ZNS, Huy hiệu đọc, Đặt phòng học nhóm), phát hiện snapshot
(`ELIBAPIDbContextModelSnapshot.cs`) bị trôi so với model C# hiện tại ở 6 điểm:
`PolicyCircDocGroup.DocGroupId` (int→bigint, đổi ở Đợt 5 bằng SQL tay, chưa từng ghi vào snapshot) và 5
bảng đã tồn tại thật trên CSDL từ trước nhưng chưa từng được migration nào ghi nhận tạo ra:
`DocumentSubmission`, `ItemReservation` (EbookItemReservation), `ReaderPhoto`, `ReaderPreference`,
`ScheduledReport`. Đã scaffold 1 migration duy nhất
(`20260912142434_SyncGamificationNotificationRoomBookingAndUntrackedTables`) gộp cả việc đồng bộ 6 điểm
trôi này VÀ 6 bảng mới của Đợt 6 (`Badge`, `ReaderBadge`, `NotificationChannelConfig`, `NotificationLog`,
`RoomBooking`, `RoomBookingConfig`), rồi baseline (không chạy `Up()`, vì toàn bộ 11 bảng + cột đã tồn tại
thật trên Postgres live) — đã xác nhận sau đó scaffold 1 migration probe rỗng dưới SQL Server cho kết quả
hoàn toàn trống, nghĩa là model/snapshot nay đã khớp nhau tuyệt đối.
