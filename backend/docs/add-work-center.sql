/* =============================================================================
   Đợt 15 — Trung tâm công việc (port từ ELIB-LRC AdminWorkAssignment, bản đầu). Tạo 1 bảng metadata
   phân công dbo.AdminWorkAssignment — không đụng cột nào ở 3 bảng nguồn (map.RoomBooking/
   Ebook.DocumentSubmission/Ebook.Review). Khác ELIB-LRC (đơn tenant): thêm cột TenantId (null = tài
   khoản đặc quyền, đúng quy ước AdminTask.TenantId). Cột Version (không phải Revision) khớp quy ước
   compare-and-swap đã dùng ở AdminTask.Version.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL — LƯU Ý: ELIBAPIDbContext.OnModelCreating map schema "dbo" (SQL Server) -> "public"
   (Postgres, đúng quy ước toàn dự án — xem add-admin-task-monitor-retention.sql). Bảng thật trên CSDL
   live (Postgres) nằm ở public, KHÔNG phải dbo.
   Chạy phần tương ứng với database đang dùng. Idempotent, chỉ cộng thêm, không sửa dữ liệu nguồn.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminWorkAssignment')
BEGIN
    CREATE TABLE [dbo].[AdminWorkAssignment] (
        [SourceType]     varchar(30)   NOT NULL,
        [SourcePublicId] uniqueidentifier NOT NULL,
        [AssigneeId]     bigint        NULL,
        [DueAtUtc]       datetime2     NULL,
        [Version]        int           NOT NULL,
        [UpdatedBy]      bigint        NOT NULL,
        [UpdatedAtUtc]   datetime2     NOT NULL,
        [TenantId]       bigint        NULL,
        CONSTRAINT [PK_AdminWorkAssignment] PRIMARY KEY ([SourceType], [SourcePublicId])
    );
    CREATE INDEX [IX_AdminWorkAssignment_SourceType_AssigneeId_DueAtUtc]
        ON [dbo].[AdminWorkAssignment] ([SourceType], [AssigneeId], [DueAtUtc]);
    CREATE INDEX [IX_AdminWorkAssignment_TenantId] ON [dbo].[AdminWorkAssignment] ([TenantId]);
    PRINT N'Đã tạo bảng dbo.AdminWorkAssignment.';
END ELSE PRINT N'Bảng dbo.AdminWorkAssignment đã tồn tại — bỏ qua.';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
CREATE TABLE IF NOT EXISTS public."AdminWorkAssignment" (
    "SourceType"     varchar(30) NOT NULL,
    "SourcePublicId" uuid        NOT NULL,
    "AssigneeId"     bigint      NULL,
    "DueAtUtc"       timestamp   NULL,
    "Version"        integer     NOT NULL,
    "UpdatedBy"      bigint      NOT NULL,
    "UpdatedAtUtc"   timestamp   NOT NULL,
    "TenantId"       bigint      NULL,
    CONSTRAINT "PK_AdminWorkAssignment" PRIMARY KEY ("SourceType", "SourcePublicId")
);
CREATE INDEX IF NOT EXISTS "IX_AdminWorkAssignment_SourceType_AssigneeId_DueAtUtc"
    ON public."AdminWorkAssignment" ("SourceType", "AssigneeId", "DueAtUtc");
CREATE INDEX IF NOT EXISTS "IX_AdminWorkAssignment_TenantId" ON public."AdminWorkAssignment" ("TenantId");
*/
