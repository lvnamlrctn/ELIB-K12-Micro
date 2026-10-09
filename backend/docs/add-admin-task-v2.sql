/* =============================================================================
   Đợt 10 — Nền tảng AdminTask v2 (tác vụ nền cho thao tác hàng loạt, port từ ELIB-LRC). Tạo 3 bảng:
   dbo.AdminTask, dbo.AdminTaskChunk, dbo.AdminWorkerHeartbeat. Sinh từ migration EF
   20260920145254_AddAdminTaskV2 (scaffold ép SQL Server theo backend/docs/EF-MIGRATIONS-WORKFLOW.md).

   PHẦN 1: SQL Server (khớp nguyên văn migration Up())
   PHẦN 2: PostgreSQL (dịch kiểu: nvarchar(max)->text, nvarchar(N)->varchar(N), datetime2->timestamp,
           uniqueidentifier->uuid, bit->boolean)
   Chạy phần tương ứng với database đang dùng. Idempotent (IF NOT EXISTS / CREATE TABLE IF NOT EXISTS).
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminTask')
BEGIN
    CREATE TABLE [dbo].[AdminTask] (
        [Id] uniqueidentifier NOT NULL,
        [ActorId] bigint NOT NULL,
        [TenantId] bigint NULL,
        [Kind] nvarchar(40) NOT NULL,
        [State] nvarchar(24) NOT NULL,
        [Preview] bit NOT NULL,
        [Payload] nvarchar(max) NULL,
        [Result] nvarchar(max) NULL,
        [Error] nvarchar(max) NULL,
        [Attempts] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] int NOT NULL,
        [TotalChunks] int NOT NULL,
        [CompletedChunks] int NOT NULL,
        [TotalItems] int NOT NULL,
        [CompletedItems] int NOT NULL,
        [ConsecutiveFailures] int NOT NULL,
        [StopRequested] bit NOT NULL,
        [SourcePreviewId] uniqueidentifier NULL,
        [ReviewToken] nvarchar(450) NULL,
        [FinishedAt] datetime2 NULL,
        CONSTRAINT [PK_AdminTask] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_AdminTask_ActorId]    ON [dbo].[AdminTask] ([ActorId]);
    CREATE INDEX [IX_AdminTask_ReviewToken] ON [dbo].[AdminTask] ([ReviewToken]);
    CREATE INDEX [IX_AdminTask_State]      ON [dbo].[AdminTask] ([State]);
    PRINT N'Đã tạo bảng dbo.AdminTask.';
END ELSE PRINT N'Bảng dbo.AdminTask đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminTaskChunk')
BEGIN
    CREATE TABLE [dbo].[AdminTaskChunk] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [Position] int NOT NULL,
        [ItemCount] int NOT NULL,
        [Payload] nvarchar(max) NULL,
        [Result] nvarchar(max) NULL,
        [Completed] bit NOT NULL,
        CONSTRAINT [PK_AdminTaskChunk] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_AdminTaskChunk_TaskId_Position] ON [dbo].[AdminTaskChunk] ([TaskId], [Position]);
    PRINT N'Đã tạo bảng dbo.AdminTaskChunk.';
END ELSE PRINT N'Bảng dbo.AdminTaskChunk đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminWorkerHeartbeat')
BEGIN
    CREATE TABLE [dbo].[AdminWorkerHeartbeat] (
        [Id] nvarchar(100) NOT NULL,
        [LastSeen] datetime2 NOT NULL,
        [KeyFingerprint] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_AdminWorkerHeartbeat] PRIMARY KEY ([Id])
    );
    PRINT N'Đã tạo bảng dbo.AdminWorkerHeartbeat.';
END ELSE PRINT N'Bảng dbo.AdminWorkerHeartbeat đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20260920145254_AddAdminTaskV2')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260920145254_AddAdminTaskV2', '10.0.4');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
-- LƯU Ý: ELIBAPIDbContext.OnModelCreating map schema "dbo" (SQL Server) -> "public" (Postgres) qua biến
-- dboSchema (dòng ~275) — override cả [Table(Schema="dbo")] trên entity. Bảng thật nằm ở public, KHÔNG
-- phải dbo (Postgres không có schema "dbo" trong CSDL live này).
CREATE TABLE IF NOT EXISTS public."AdminTask" (
    "Id" uuid NOT NULL,
    "ActorId" bigint NOT NULL,
    "TenantId" bigint NULL,
    "Kind" varchar(40) NOT NULL,
    "State" varchar(24) NOT NULL,
    "Preview" boolean NOT NULL,
    "Payload" text NULL,
    "Result" text NULL,
    "Error" text NULL,
    "Attempts" integer NOT NULL,
    "CreatedAt" timestamp NOT NULL,
    "UpdatedAt" timestamp NOT NULL,
    "Version" integer NOT NULL,
    "TotalChunks" integer NOT NULL,
    "CompletedChunks" integer NOT NULL,
    "TotalItems" integer NOT NULL,
    "CompletedItems" integer NOT NULL,
    "ConsecutiveFailures" integer NOT NULL,
    "StopRequested" boolean NOT NULL,
    "SourcePreviewId" uuid NULL,
    "ReviewToken" varchar(450) NULL,
    "FinishedAt" timestamp NULL,
    CONSTRAINT "PK_AdminTask" PRIMARY KEY ("Id")
);
CREATE INDEX IF NOT EXISTS "IX_AdminTask_ActorId"     ON public."AdminTask" ("ActorId");
CREATE INDEX IF NOT EXISTS "IX_AdminTask_ReviewToken" ON public."AdminTask" ("ReviewToken");
CREATE INDEX IF NOT EXISTS "IX_AdminTask_State"       ON public."AdminTask" ("State");

CREATE TABLE IF NOT EXISTS public."AdminTaskChunk" (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "Position" integer NOT NULL,
    "ItemCount" integer NOT NULL,
    "Payload" text NULL,
    "Result" text NULL,
    "Completed" boolean NOT NULL,
    CONSTRAINT "PK_AdminTaskChunk" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminTaskChunk_TaskId_Position" ON public."AdminTaskChunk" ("TaskId", "Position");

CREATE TABLE IF NOT EXISTS public."AdminWorkerHeartbeat" (
    "Id" varchar(100) NOT NULL,
    "LastSeen" timestamp NOT NULL,
    "KeyFingerprint" varchar(64) NOT NULL,
    CONSTRAINT "PK_AdminWorkerHeartbeat" PRIMARY KEY ("Id")
);

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260920145254_AddAdminTaskV2', '10.0.4'
WHERE NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20260920145254_AddAdminTaskV2');
*/
