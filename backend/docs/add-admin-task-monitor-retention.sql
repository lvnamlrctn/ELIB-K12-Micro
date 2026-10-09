/* =============================================================================
   Đợt 13 — Giám sát tác vụ AdminTask của người khác + Dọn payload/result cũ (port từ ELIB-LRC). Thêm 3
   cột vào dbo.AdminTask (RetentionHold/PayloadPurgedAt/ResultPurgedAt), tạo 2 bảng mới:
   dbo.AdminTaskControlEvent, dbo.AdminTaskRetentionRun. Sinh từ migration EF
   20260921064620_AddAdminTaskMonitorRetention (scaffold ép SQL Server theo
   backend/docs/EF-MIGRATIONS-WORKFLOW.md).

   PHẦN 1: SQL Server (khớp nguyên văn migration Up())
   PHẦN 2: PostgreSQL (dịch kiểu: nvarchar(max)->text, nvarchar(N)->varchar(N), datetime2->timestamp,
           uniqueidentifier->uuid, bit->boolean)
   Chạy phần tương ứng với database đang dùng. Idempotent.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdminTask') AND name = 'RetentionHold')
BEGIN
    ALTER TABLE [dbo].[AdminTask] ADD [RetentionHold] bit NOT NULL CONSTRAINT [DF_AdminTask_RetentionHold] DEFAULT (0);
    ALTER TABLE [dbo].[AdminTask] DROP CONSTRAINT [DF_AdminTask_RetentionHold];
    ALTER TABLE [dbo].[AdminTask] ADD [PayloadPurgedAt] datetime2 NULL;
    ALTER TABLE [dbo].[AdminTask] ADD [ResultPurgedAt] datetime2 NULL;
    PRINT N'Đã thêm 3 cột retention vào dbo.AdminTask.';
END ELSE PRINT N'Cột RetentionHold đã tồn tại trên dbo.AdminTask — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdminTask') AND name = 'IX_AdminTask_State_FinishedAt')
    CREATE INDEX [IX_AdminTask_State_FinishedAt] ON [dbo].[AdminTask] ([State], [FinishedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminTaskControlEvent')
BEGIN
    CREATE TABLE [dbo].[AdminTaskControlEvent] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [OperatorId] bigint NOT NULL,
        [OwnerId] bigint NOT NULL,
        [Action] nvarchar(16) NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [StateBefore] nvarchar(24) NOT NULL,
        [StateAfter] nvarchar(24) NOT NULL,
        [RequestId] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminTaskControlEvent] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_AdminTaskControlEvent_RequestId] ON [dbo].[AdminTaskControlEvent] ([RequestId]);
    CREATE INDEX [IX_AdminTaskControlEvent_TaskId] ON [dbo].[AdminTaskControlEvent] ([TaskId]);
    PRINT N'Đã tạo bảng dbo.AdminTaskControlEvent.';
END ELSE PRINT N'Bảng dbo.AdminTaskControlEvent đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminTaskRetentionRun')
BEGIN
    CREATE TABLE [dbo].[AdminTaskRetentionRun] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [FinishedAt] datetime2 NOT NULL,
        [DryRun] bit NOT NULL,
        [TaskCount] int NOT NULL,
        [ChunkCount] int NOT NULL,
        [EstimatedBytes] bigint NOT NULL,
        CONSTRAINT [PK_AdminTaskRetentionRun] PRIMARY KEY ([Id])
    );
    PRINT N'Đã tạo bảng dbo.AdminTaskRetentionRun.';
END ELSE PRINT N'Bảng dbo.AdminTaskRetentionRun đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20260921064620_AddAdminTaskMonitorRetention')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260921064620_AddAdminTaskMonitorRetention', '10.0.4');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
-- LƯU Ý: ELIBAPIDbContext.OnModelCreating map schema "dbo" (SQL Server) -> "public" (Postgres) qua biến
-- dboSchema — bảng thật nằm ở public, KHÔNG phải dbo (Postgres không có schema "dbo" trong CSDL live này).
ALTER TABLE public."AdminTask" ADD COLUMN IF NOT EXISTS "RetentionHold" boolean NOT NULL DEFAULT false;
ALTER TABLE public."AdminTask" ADD COLUMN IF NOT EXISTS "PayloadPurgedAt" timestamp NULL;
ALTER TABLE public."AdminTask" ADD COLUMN IF NOT EXISTS "ResultPurgedAt" timestamp NULL;

CREATE INDEX IF NOT EXISTS "IX_AdminTask_State_FinishedAt" ON public."AdminTask" ("State", "FinishedAt");

CREATE TABLE IF NOT EXISTS public."AdminTaskControlEvent" (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "OperatorId" bigint NOT NULL,
    "OwnerId" bigint NOT NULL,
    "Action" varchar(16) NOT NULL,
    "Reason" text NOT NULL,
    "StateBefore" varchar(24) NOT NULL,
    "StateAfter" varchar(24) NOT NULL,
    "RequestId" varchar(100) NOT NULL,
    "CreatedAt" timestamp NOT NULL,
    CONSTRAINT "PK_AdminTaskControlEvent" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminTaskControlEvent_RequestId" ON public."AdminTaskControlEvent" ("RequestId");
CREATE INDEX IF NOT EXISTS "IX_AdminTaskControlEvent_TaskId" ON public."AdminTaskControlEvent" ("TaskId");

CREATE TABLE IF NOT EXISTS public."AdminTaskRetentionRun" (
    "Id" uuid NOT NULL,
    "StartedAt" timestamp NOT NULL,
    "FinishedAt" timestamp NOT NULL,
    "DryRun" boolean NOT NULL,
    "TaskCount" integer NOT NULL,
    "ChunkCount" integer NOT NULL,
    "EstimatedBytes" bigint NOT NULL,
    CONSTRAINT "PK_AdminTaskRetentionRun" PRIMARY KEY ("Id")
);

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260921064620_AddAdminTaskMonitorRetention', '10.0.4'
WHERE NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20260921064620_AddAdminTaskMonitorRetention');
*/
