/* =============================================================================
   Tạo bảng dbo.ReaderPhoto — ảnh khuôn mặt bổ sung của 1 bạn đọc (dùng cùng Reader.Photo khi nhận
   diện khuôn mặt để tăng độ ổn định khi đổi kiểu tóc/đeo kính/góc chụp khác nhau). Script idempotent.

   LƯU Ý SCHEMA: entity khai báo [Table("ReaderPhoto", Schema = "dbo")] nhưng
   ELIBAPIDbContext.OnModelCreating ánh xạ schema "dbo" sang "public" khi chạy trên PostgreSQL — bảng
   thật trên Postgres nằm ở "public", không phải "dbo".

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
    WHERE s.name = 'dbo' AND t.name = 'ReaderPhoto'
)
BEGIN
    CREATE TABLE [dbo].[ReaderPhoto]
    (
        [Id]             BIGINT IDENTITY(1,1) NOT NULL,
        [PublicId]       UNIQUEIDENTIFIER NOT NULL,
        [ReaderId]       BIGINT NOT NULL,
        [TenantId]       BIGINT NULL,
        [PhotoUrl]       NVARCHAR(500) NULL,
        [IsDelete]       INT NULL,
        [CreatedRowDate] DATETIME2 NULL,
        CONSTRAINT [PK_ReaderPhoto] PRIMARY KEY CLUSTERED ([Id])
    );
    CREATE INDEX [IX_ReaderPhoto_ReaderId] ON [dbo].[ReaderPhoto] ([ReaderId]);
    PRINT N'Đã tạo bảng dbo.ReaderPhoto.';
END
ELSE
    PRINT N'Bảng dbo.ReaderPhoto đã tồn tại — bỏ qua.';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
CREATE TABLE IF NOT EXISTS public."ReaderPhoto"
(
    "Id"             BIGSERIAL PRIMARY KEY,
    "PublicId"       UUID NOT NULL,
    "ReaderId"       BIGINT NOT NULL,
    "TenantId"       BIGINT,
    "PhotoUrl"       VARCHAR(500),
    "IsDelete"       INT,
    "CreatedRowDate" TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_ReaderPhoto_ReaderId" ON public."ReaderPhoto" ("ReaderId");

-- Kiểm tra
SELECT * FROM public."ReaderPhoto" LIMIT 5;
*/
