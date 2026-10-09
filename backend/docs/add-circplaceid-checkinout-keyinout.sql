-- Đợt 7 — Giai đoạn 1: thêm cột CircPlaceId (điểm lưu thông) cho CheckIn/CheckOut/KeyIn/KeyOut
-- Sinh từ migration EF Core: 20260918065413_AddCircPlaceIdToCheckInOutKeyInOut
-- (scaffold bằng DatabaseProvider=SqlServer theo backend/docs/EF-MIGRATIONS-WORKFLOW.md, dịch tay sang
-- Postgres ở PHẦN 2, chỉ PHẦN 2 được chạy thật trên CSDL live). Idempotent — chạy lại nhiều lần không lỗi.

-- ============================================================
-- PHẦN 1 — SQL Server
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[PrintBook].[CheckIn]') AND name = 'CircPlaceId')
    ALTER TABLE [PrintBook].[CheckIn] ADD [CircPlaceId] bigint NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[PrintBook].[CheckOut]') AND name = 'CircPlaceId')
    ALTER TABLE [PrintBook].[CheckOut] ADD [CircPlaceId] bigint NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[PrintBook].[KeyIn]') AND name = 'CircPlaceId')
    ALTER TABLE [PrintBook].[KeyIn] ADD [CircPlaceId] bigint NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[PrintBook].[KeyOut]') AND name = 'CircPlaceId')
    ALTER TABLE [PrintBook].[KeyOut] ADD [CircPlaceId] bigint NULL;

-- ============================================================
-- PHẦN 2 — PostgreSQL (chạy thật trên CSDL live)
-- Lưu ý: schema "PrintBook" bị hạ lowercase thành "printbook" trên Postgres (xem vòng lặp
-- Database.IsSqlServer()==false ở cuối ELIBAPIDbContext.OnModelCreating) — không phải giữ nguyên "PrintBook".
-- ============================================================
ALTER TABLE "printbook"."CheckIn"  ADD COLUMN IF NOT EXISTS "CircPlaceId" bigint;
ALTER TABLE "printbook"."CheckOut" ADD COLUMN IF NOT EXISTS "CircPlaceId" bigint;
ALTER TABLE "printbook"."KeyIn"    ADD COLUMN IF NOT EXISTS "CircPlaceId" bigint;
ALTER TABLE "printbook"."KeyOut"   ADD COLUMN IF NOT EXISTS "CircPlaceId" bigint;
