/* =============================================================================
   Tạo bảng dbo.ReaderPreference — hồ sơ chủ đề/môn học bạn đọc quan tâm, dùng để
   chatbot (DocumentFinderService) gợi ý tài liệu theo hồ sơ. Script idempotent.

   LƯU Ý SCHEMA: entity khai báo [Table("ReaderPreference", Schema = "dbo")] nhưng
   ELIBAPIDbContext.OnModelCreating ánh xạ schema "dbo" sang "public" khi chạy trên
   PostgreSQL (biến dboSchema = Database.IsSqlServer() ? "dbo" : "public") — bảng thật
   trên Postgres nằm ở "public", không phải "dbo".

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
    WHERE s.name = 'dbo' AND t.name = 'ReaderPreference'
)
BEGIN
    CREATE TABLE [dbo].[ReaderPreference]
    (
        [Id]             BIGINT IDENTITY(1,1) NOT NULL,
        [ReaderId]       BIGINT NOT NULL,
        [SubjectId]      BIGINT NULL,
        [TopicId]        BIGINT NULL,
        [UpdatedRowDate] DATETIME2 NULL,
        [TenantId]       BIGINT NULL,
        CONSTRAINT [PK_ReaderPreference] PRIMARY KEY CLUSTERED ([Id])
    );
    CREATE UNIQUE INDEX [IX_ReaderPreference_ReaderId] ON [dbo].[ReaderPreference] ([ReaderId]);
    PRINT N'Đã tạo bảng dbo.ReaderPreference.';
END
ELSE
    PRINT N'Bảng dbo.ReaderPreference đã tồn tại — bỏ qua.';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
CREATE TABLE IF NOT EXISTS public."ReaderPreference"
(
    "Id"             BIGSERIAL PRIMARY KEY,
    "ReaderId"       BIGINT NOT NULL,
    "SubjectId"      BIGINT,
    "TopicId"        BIGINT,
    "UpdatedRowDate" TIMESTAMP,
    "TenantId"       BIGINT
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReaderPreference_ReaderId" ON public."ReaderPreference" ("ReaderId");

-- Kiểm tra
SELECT * FROM public."ReaderPreference" LIMIT 5;
*/
