/* =============================================================================
   Tạo bảng dbo.ScheduledReport — cấu hình báo cáo định kỳ tự động gửi email
   (ScheduledReportController + ScheduledReportEmailJob). Script idempotent.

   LƯU Ý SCHEMA: entity khai báo [Table("ScheduledReport", Schema = "dbo")] nhưng
   ELIBAPIDbContext.OnModelCreating ánh xạ schema "dbo" sang "public" khi chạy trên
   PostgreSQL — bảng thật trên Postgres nằm ở "public", không phải "dbo".

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo' AND t.name = 'ScheduledReport'
)
BEGIN
    CREATE TABLE [dbo].[ScheduledReport]
    (
        [Id]               BIGINT IDENTITY(1,1) NOT NULL,
        [Name]             NVARCHAR(MAX) NULL,
        [ReportType]       NVARCHAR(50)  NULL,
        [ReportParamsJson] NVARCHAR(MAX) NULL,
        [FrequencyType]    INT NOT NULL DEFAULT 1,
        [DayOfWeek]        INT NULL,
        [DayOfMonth]       INT NULL,
        [TimeOfDay]        NVARCHAR(5) NULL,
        [RecipientEmails]  NVARCHAR(MAX) NULL,
        [Status]           INT NOT NULL DEFAULT 2,
        [LastRunAt]        DATETIME2 NULL,
        [LastRunStatus]    NVARCHAR(MAX) NULL,
        [IsDelete]         INT NULL,
        [CreatedRowBy]     BIGINT NULL,
        [UpdateRowBy]      BIGINT NULL,
        [CreatedRowDate]   DATETIME2 NULL,
        [UpdatedRowDate]   DATETIME2 NULL,
        [PublicId]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]         BIGINT NULL,
        CONSTRAINT [PK_ScheduledReport] PRIMARY KEY CLUSTERED ([Id])
    );
    CREATE INDEX [IX_ScheduledReport_TenantId] ON [dbo].[ScheduledReport] ([TenantId]);
    PRINT N'Đã tạo bảng dbo.ScheduledReport.';
END
ELSE
    PRINT N'Bảng dbo.ScheduledReport đã tồn tại — bỏ qua.';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
CREATE TABLE IF NOT EXISTS public."ScheduledReport"
(
    "Id"               BIGSERIAL PRIMARY KEY,
    "Name"             TEXT,
    "ReportType"       VARCHAR(50),
    "ReportParamsJson" TEXT,
    "FrequencyType"    INTEGER NOT NULL DEFAULT 1,
    "DayOfWeek"        INTEGER,
    "DayOfMonth"       INTEGER,
    "TimeOfDay"        VARCHAR(5),
    "RecipientEmails"  TEXT,
    "Status"           INTEGER NOT NULL DEFAULT 2,
    "LastRunAt"        TIMESTAMP,
    "LastRunStatus"    TEXT,
    "IsDelete"         INTEGER,
    "CreatedRowBy"     BIGINT,
    "UpdateRowBy"      BIGINT,
    "CreatedRowDate"   TIMESTAMP,
    "UpdatedRowDate"   TIMESTAMP,
    "PublicId"         UUID NOT NULL DEFAULT gen_random_uuid(),
    "TenantId"         BIGINT
);

CREATE INDEX IF NOT EXISTS "IX_ScheduledReport_TenantId" ON public."ScheduledReport" ("TenantId");

-- Kiểm tra
SELECT * FROM public."ScheduledReport" LIMIT 5;
*/
