/* =============================================================================
   Seed 1 dòng dbo.SystemParameter làm cờ bật/tắt tính năng Đặt phòng học nhóm (Feature C), đọc qua
   ISystemParameterService.IsEnabledAsync("ROOM_BOOKING_ENABLED", tenantId):
   - RoomBookingController (OPAC) — ẩn tab đặt phòng / chặn tạo booking mới khi tắt.
   - RoomBookingExpiryJob (Hangfire, mỗi giờ) — bỏ qua toàn bộ khi tắt.

   TenantId = NULL (mặc định dùng chung mọi tenant — SystemParameterService tự fallback về dòng này khi
   tenant hiện tại chưa có mẫu riêng; muốn bật/tắt riêng theo 1 tenant thì thêm 1 dòng khác cùng Code với
   TenantId cụ thể). Giá trị mặc định '0' (tắt) — không seed thì IsEnabledAsync coi như chưa bật. Script
   idempotent, chạy lại an toàn.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'ROOM_BOOKING_ENABLED' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
    VALUES (NEWID(), 'ROOM_BOOKING_ENABLED', '0', N'Bật/tắt tính năng Đặt phòng học nhóm (OPAC + job tự động từ chối/no-show/hoàn tất). "1" = bật, "0" = tắt.', NULL);
    PRINT N'Đã thêm tham số ROOM_BOOKING_ENABLED.';
END ELSE PRINT N'Tham số ROOM_BOOKING_ENABLED đã tồn tại — bỏ qua.';

-- Kiểm tra
SELECT Code, Value, TenantId FROM [dbo].[SystemParameter] WHERE Code = 'ROOM_BOOKING_ENABLED';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'ROOM_BOOKING_ENABLED', '0', 'Bật/tắt tính năng Đặt phòng học nhóm (OPAC + job tự động từ chối/no-show/hoàn tất). "1" = bật, "0" = tắt.', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'ROOM_BOOKING_ENABLED' AND "TenantId" IS NULL);

-- Kiểm tra
SELECT "Code", "Value", "TenantId" FROM public.systemparameter WHERE "Code" = 'ROOM_BOOKING_ENABLED';
*/
