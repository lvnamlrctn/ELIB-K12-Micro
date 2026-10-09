/* =============================================================================
   Tạo bảng Ebook.ItemReservation — hàng đợi đặt trước tài liệu số khi
   EbookItem.PrintCopies đã đạt giới hạn đồng thời (xem MyLibraryController /
   EbookItemReservationRepository). Script idempotent — chạy lại không lỗi.

   Lưu ý: các bảng entity của dự án này KHÔNG áp dụng qua `dotnet ef database update`
   trên PostgreSQL (EF Core Migrations trong ELIBAPI.Infrastructure/Migrations chỉ đại
   diện đúng cho SQL Server — xem docs/POSTGRESQL_MIGRATION.md mục "Bước 4"), nên bảng
   mới luôn được tạo bằng SQL tay như file này, cho cả 2 hệ quản trị.

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
    WHERE s.name = 'Ebook' AND t.name = 'ItemReservation'
)
BEGIN
    CREATE TABLE [Ebook].[ItemReservation]
    (
        [Id]             BIGINT IDENTITY(1,1) NOT NULL,
        [EbookItemId]    BIGINT NOT NULL,
        [ReaderId]       BIGINT NOT NULL,
        [RequestedAt]    DATETIME2      NOT NULL,
        [Status]         INT            NOT NULL,
        [ReadyAt]        DATETIME2      NULL,
        [ReadyExpiresAt] DATETIME2      NULL,
        [FulfilledAt]    DATETIME2      NULL,
        [CancelledAt]    DATETIME2      NULL,
        [IsDelete]       INT            NULL,
        [CreatedRowBy]   BIGINT         NULL,
        [UpdateRowBy]    BIGINT         NULL,
        [CreatedRowDate] DATETIME2      NULL,
        [UpdatedRowDate] DATETIME2      NULL,
        [TenantId]       BIGINT         NULL,
        [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        CONSTRAINT [PK_ItemReservation] PRIMARY KEY CLUSTERED ([Id])
    );
    CREATE INDEX [IX_ItemReservation_EbookItemId] ON [Ebook].[ItemReservation] ([EbookItemId]);
    CREATE INDEX [IX_ItemReservation_ReaderId]    ON [Ebook].[ItemReservation] ([ReaderId]);
    CREATE INDEX [IX_ItemReservation_TenantId]    ON [Ebook].[ItemReservation] ([TenantId]);
    PRINT N'Đã tạo bảng Ebook.ItemReservation.';
END
ELSE
    PRINT N'Bảng Ebook.ItemReservation đã tồn tại — bỏ qua.';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
-- Lưu ý casing: ELIBAPIDbContext.OnModelCreating hạ chữ thường TOÀN BỘ tên schema cho
-- PostgreSQL (kể cả khai báo [Table(..., Schema="Ebook")] trong C#) -- vật lý là "ebook"
-- (không quote hoa), khớp ghi chú "existing DB uses lowercase schema names" trong
-- docs/POSTGRESQL_MIGRATION.md. Chỉ TÊN BẢNG mới giữ nguyên chữ hoa/thường như C#.
CREATE TABLE IF NOT EXISTS ebook."ItemReservation"
(
    "Id"             BIGSERIAL PRIMARY KEY,
    "EbookItemId"    BIGINT NOT NULL,
    "ReaderId"       BIGINT NOT NULL,
    "RequestedAt"    TIMESTAMP NOT NULL,
    "Status"         INT NOT NULL,
    "ReadyAt"        TIMESTAMP,
    "ReadyExpiresAt" TIMESTAMP,
    "FulfilledAt"    TIMESTAMP,
    "CancelledAt"    TIMESTAMP,
    "IsDelete"       INT,
    "CreatedRowBy"   BIGINT,
    "UpdateRowBy"    BIGINT,
    "CreatedRowDate" TIMESTAMP,
    "UpdatedRowDate" TIMESTAMP,
    "TenantId"       BIGINT,
    "PublicId"       UUID NOT NULL DEFAULT gen_random_uuid()
);

CREATE INDEX IF NOT EXISTS "IX_ItemReservation_EbookItemId" ON ebook."ItemReservation" ("EbookItemId");
CREATE INDEX IF NOT EXISTS "IX_ItemReservation_ReaderId"    ON ebook."ItemReservation" ("ReaderId");
CREATE INDEX IF NOT EXISTS "IX_ItemReservation_TenantId"    ON ebook."ItemReservation" ("TenantId");

-- Kiểm tra
SELECT * FROM ebook."ItemReservation" LIMIT 5;
*/
