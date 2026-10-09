/* =============================================================================
   Đợt 22.4 — cột Store.Capacity (sức chứa kho, dùng cho tỷ lệ lấp đầy ở dashboard Bổ sung & Kho) +
   bảng Ebook.DigitalStorageAuditResult (kết quả DigitalStorageAuditJob — đối soát MinIO/EbookFile, hiện
   ở tab DevOps). Migration EF tương ứng: 20260925171309_AddStoreCapacityAndDigitalStorageAudit (scaffold
   ép SQL Server theo backend/docs/EF-MIGRATIONS-WORKFLOW.md). Trên SQL Server, Database.Migrate() tự chạy
   migration lúc khởi động. Script này dành cho PostgreSQL (Migrate() không chạy).

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL — đã KIỂM TRA TRỰC TIẾP trên CSDL dev: Npgsql/EF tự hạ chữ thường tên schema khi tạo
   bảng trước đây (khác "dbo"→"public" là quy ước cố ý) — "PrintBook" thật ra là printbook, "Ebook" thật ra
   là ebook, không viết hoa như thuộc tính [Table(Schema=...)] phía C#.
   Chạy phần tương ứng với database đang dùng. Idempotent, chỉ cộng thêm, không sửa dữ liệu nghiệp vụ.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PrintBook.Store') AND name = 'Capacity')
    ALTER TABLE [PrintBook].[Store] ADD [Capacity] int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'Ebook' AND t.name = 'DigitalStorageAuditResult')
BEGIN
    CREATE TABLE [Ebook].[DigitalStorageAuditResult] (
        [Id]               bigint IDENTITY(1,1) NOT NULL,
        [RunAt]            datetime2 NOT NULL,
        [TotalObjects]     bigint NOT NULL,
        [TotalSizeBytes]   bigint NOT NULL,
        [OrphanCount]      bigint NOT NULL,
        [DbFileCount]      bigint NOT NULL,
        [OrphanSampleJson] nvarchar(max) NULL,
        [BrokenFileCount]  bigint NULL,
        [BrokenSampleJson] nvarchar(max) NULL,
        CONSTRAINT [PK_DigitalStorageAuditResult] PRIMARY KEY ([Id])
    );
    PRINT N'Đã tạo bảng Ebook.DigitalStorageAuditResult.';
END ELSE PRINT N'Bảng Ebook.DigitalStorageAuditResult đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20260925171309_AddStoreCapacityAndDigitalStorageAudit')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260925171309_AddStoreCapacityAndDigitalStorageAudit', '10.0.4');


/* =============================================================================
   PHẦN 2 — POSTGRESQL (bỏ comment khối dưới rồi chạy)
   =============================================================================

ALTER TABLE printbook."Store" ADD COLUMN IF NOT EXISTS "Capacity" integer NULL;

CREATE TABLE IF NOT EXISTS ebook."DigitalStorageAuditResult" (
    "Id"               bigserial PRIMARY KEY,
    "RunAt"            timestamp NOT NULL,
    "TotalObjects"     bigint    NOT NULL,
    "TotalSizeBytes"   bigint    NOT NULL,
    "OrphanCount"      bigint    NOT NULL,
    "DbFileCount"      bigint    NOT NULL,
    "OrphanSampleJson" text      NULL,
    "BrokenFileCount"  bigint    NULL,
    "BrokenSampleJson" text      NULL
);

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260925171309_AddStoreCapacityAndDigitalStorageAudit', '10.0.4'
WHERE NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20260925171309_AddStoreCapacityAndDigitalStorageAudit');
*/
