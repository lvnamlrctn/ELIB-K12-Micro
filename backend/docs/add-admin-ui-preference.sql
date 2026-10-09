/* =============================================================================
   Đợt 21 — Cấu hình bảng theo tài khoản (port từ ELIB-LRC AdminUiPreference). Tạo 1 bảng
   dbo.AdminUiPreference: khoá (ActorId, PageKey), SettingsJson, Revision (chống ghi đè giữa 2 phiên),
   UpdatedAt (UTC). Khác ELIB-LRC (đơn tenant): thêm cột TenantId ghi nhận đơn vị của tài khoản lúc lưu.
   Migration EF tương ứng: 20260925125327_AddAdminUiPreferenceAndWorkAssignment (scaffold ép SQL Server theo
   backend/docs/EF-MIGRATIONS-WORKFLOW.md). Migration đó gộp luôn phần đồng bộ snapshot còn thiếu: bảng
   dbo.AdminWorkAssignment (Đợt 15 — trước chỉ có add-work-center.sql) và tên cột viết thường của PrintBook.Bib_Type
   (sửa lỗi BibType 500). Trên SQL Server, Database.Migrate() tự chạy migration lúc khởi động — Up() tạo 2 bảng
   CÓ ĐIỀU KIỆN nên an toàn cả khi script này hoặc add-work-center.sql đã chạy trước. Script này dành cho
   PostgreSQL (Migrate() không chạy) hoặc khi muốn tạo bảng tay trước khi cài bản mới.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL — schema "dbo" của EF được map sang "public" (xem ELIBAPIDbContext.OnModelCreating).
   Chạy phần tương ứng với database đang dùng. Idempotent, chỉ cộng thêm, không sửa dữ liệu nghiệp vụ.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminUiPreference')
BEGIN
    CREATE TABLE [dbo].[AdminUiPreference] (
        [ActorId]      bigint        NOT NULL,
        [PageKey]      nvarchar(40)  NOT NULL,
        [SettingsJson] nvarchar(max) NOT NULL,
        [Revision]     int           NOT NULL,
        [UpdatedAt]    datetime2     NOT NULL,
        [TenantId]     bigint        NULL,
        CONSTRAINT [PK_AdminUiPreference] PRIMARY KEY ([ActorId], [PageKey])
    );
    PRINT N'Đã tạo bảng dbo.AdminUiPreference.';
END ELSE PRINT N'Bảng dbo.AdminUiPreference đã tồn tại — bỏ qua.';

-- Chỉ chạy khi KHÔNG để Database.Migrate() tự áp dụng migration (vd tạo bảng tay trước), và chỉ sau khi
-- dbo.AdminWorkAssignment đã tồn tại (add-work-center.sql) + cột Bib_Type đã là chữ thường — nếu không, đừng
-- đánh dấu, để Migrate() tự chạy Up().
-- IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20260925125327_AddAdminUiPreferenceAndWorkAssignment')
--     INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
--     VALUES ('20260925125327_AddAdminUiPreferenceAndWorkAssignment', '10.0.4');


/* =============================================================================
   PHẦN 2 — POSTGRESQL (bỏ comment khối dưới rồi chạy)
   =============================================================================

CREATE TABLE IF NOT EXISTS public."AdminUiPreference" (
    "ActorId"      bigint      NOT NULL,
    "PageKey"      varchar(40) NOT NULL,
    "SettingsJson" text        NOT NULL,
    "Revision"     integer     NOT NULL,
    "UpdatedAt"    timestamp   NOT NULL,
    "TenantId"     bigint      NULL,
    CONSTRAINT "PK_AdminUiPreference" PRIMARY KEY ("ActorId", "PageKey")
);

-- Đánh dấu migration đã áp dụng (baseline — Postgres không chạy Up() vì kiểu cột SQL Server). Điều kiện: bảng
-- public."AdminWorkAssignment" đã có (add-work-center.sql) và cột printbook."Bib_Type" đã viết thường (id, name…).
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260925125327_AddAdminUiPreferenceAndWorkAssignment', '10.0.4'
WHERE NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20260925125327_AddAdminUiPreferenceAndWorkAssignment');
*/
