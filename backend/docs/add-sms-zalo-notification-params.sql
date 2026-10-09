/* =============================================================================
   Seed cờ bật/tắt + template rỗng cho hạ tầng SMS/Zalo ZNS (Đợt 6, Giai đoạn 2) — đọc qua
   ISystemParameterService.GetValueAsync/IsEnabledAsync(code, tenantId):
   - SMS_NOTIFICATIONS_ENABLED / ZALO_NOTIFICATIONS_ENABLED — cờ tổng, mặc định '0' (tắt).
   - SMS_{eventCode} — nội dung mẫu SMS cho từng sự kiện (để trống, admin tự điền sau khi có access-token
     SpeedSMS thật qua UI Tham số hệ thống).
   - ZALO_TEMPLATE_{eventCode} — Template ID ZNS cho từng sự kiện (để trống, admin tự điền sau khi có
     Official Action + template được Zalo duyệt).
   8 sự kiện: PRINT_HOLD_SUCCESS, EBOOK_RESERVATION_READY, PRINT_DUE_SOON, PRINT_OVERDUE, EBOOK_DUE_SOON,
   BADGE_EARNED, ROOM_BOOKING_APPROVED, ROOM_BOOKING_REJECTED.
   TenantId = NULL (dùng chung mọi tenant, mỗi tenant tự override sau nếu cần). Script idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'SMS_NOTIFICATIONS_ENABLED' AND TenantId IS NULL)
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
    VALUES (NEWID(), 'SMS_NOTIFICATIONS_ENABLED', '0', N'Bật/tắt gửi thông báo qua SMS (SpeedSMS). "1" = bật, "0" = tắt.', NULL);

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'ZALO_NOTIFICATIONS_ENABLED' AND TenantId IS NULL)
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
    VALUES (NEWID(), 'ZALO_NOTIFICATIONS_ENABLED', '0', N'Bật/tắt gửi thông báo qua Zalo ZNS. "1" = bật, "0" = tắt.', NULL);

DECLARE @events TABLE (EventCode NVARCHAR(50), DescriptionVn NVARCHAR(300));
INSERT INTO @events VALUES
    ('PRINT_HOLD_SUCCESS',      N'Đặt mượn sách in thành công'),
    ('EBOOK_RESERVATION_READY', N'Đã đến lượt mượn tài liệu số đặt trước'),
    ('PRINT_DUE_SOON',          N'Sách in sắp đến hạn trả'),
    ('PRINT_OVERDUE',           N'Sách in quá hạn trả'),
    ('EBOOK_DUE_SOON',          N'Tài liệu số sắp hết hạn mượn'),
    ('BADGE_EARNED',            N'Đạt huy hiệu đọc mới'),
    ('ROOM_BOOKING_APPROVED',   N'Đặt phòng học nhóm được duyệt'),
    ('ROOM_BOOKING_REJECTED',   N'Đặt phòng học nhóm bị từ chối');

INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
SELECT NEWID(), 'SMS_' + e.EventCode, '', N'Nội dung mẫu SMS — ' + e.DescriptionVn, NULL
FROM @events e
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'SMS_' + e.EventCode AND TenantId IS NULL);

INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
SELECT NEWID(), 'ZALO_TEMPLATE_' + e.EventCode, '', N'Zalo ZNS Template ID — ' + e.DescriptionVn, NULL
FROM @events e
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'ZALO_TEMPLATE_' + e.EventCode AND TenantId IS NULL);

PRINT N'Đã seed tham số SMS/Zalo ZNS.';

-- Kiểm tra
SELECT Code, Value, TenantId FROM [dbo].[SystemParameter]
WHERE Code IN ('SMS_NOTIFICATIONS_ENABLED','ZALO_NOTIFICATIONS_ENABLED')
   OR Code LIKE 'SMS\_%' ESCAPE '\' OR Code LIKE 'ZALO\_TEMPLATE\_%' ESCAPE '\'
ORDER BY Code;


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'SMS_NOTIFICATIONS_ENABLED', '0', 'Bật/tắt gửi thông báo qua SMS (SpeedSMS). "1" = bật, "0" = tắt.', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'SMS_NOTIFICATIONS_ENABLED' AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'ZALO_NOTIFICATIONS_ENABLED', '0', 'Bật/tắt gửi thông báo qua Zalo ZNS. "1" = bật, "0" = tắt.', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'ZALO_NOTIFICATIONS_ENABLED' AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'SMS_' || e.code, '', 'Nội dung mẫu SMS — ' || e.desc_vn, NULL
FROM (VALUES
    ('PRINT_HOLD_SUCCESS',      'Đặt mượn sách in thành công'),
    ('EBOOK_RESERVATION_READY', 'Đã đến lượt mượn tài liệu số đặt trước'),
    ('PRINT_DUE_SOON',          'Sách in sắp đến hạn trả'),
    ('PRINT_OVERDUE',           'Sách in quá hạn trả'),
    ('EBOOK_DUE_SOON',          'Tài liệu số sắp hết hạn mượn'),
    ('BADGE_EARNED',            'Đạt huy hiệu đọc mới'),
    ('ROOM_BOOKING_APPROVED',   'Đặt phòng học nhóm được duyệt'),
    ('ROOM_BOOKING_REJECTED',   'Đặt phòng học nhóm bị từ chối')
) AS e(code, desc_vn)
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'SMS_' || e.code AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'ZALO_TEMPLATE_' || e.code, '', 'Zalo ZNS Template ID — ' || e.desc_vn, NULL
FROM (VALUES
    ('PRINT_HOLD_SUCCESS',      'Đặt mượn sách in thành công'),
    ('EBOOK_RESERVATION_READY', 'Đã đến lượt mượn tài liệu số đặt trước'),
    ('PRINT_DUE_SOON',          'Sách in sắp đến hạn trả'),
    ('PRINT_OVERDUE',           'Sách in quá hạn trả'),
    ('EBOOK_DUE_SOON',          'Tài liệu số sắp hết hạn mượn'),
    ('BADGE_EARNED',            'Đạt huy hiệu đọc mới'),
    ('ROOM_BOOKING_APPROVED',   'Đặt phòng học nhóm được duyệt'),
    ('ROOM_BOOKING_REJECTED',   'Đặt phòng học nhóm bị từ chối')
) AS e(code, desc_vn)
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'ZALO_TEMPLATE_' || e.code AND "TenantId" IS NULL);

-- Kiểm tra
SELECT "Code", "Value", "TenantId" FROM public.systemparameter
WHERE "Code" IN ('SMS_NOTIFICATIONS_ENABLED','ZALO_NOTIFICATIONS_ENABLED')
   OR "Code" LIKE 'SMS\_%' ESCAPE '\' OR "Code" LIKE 'ZALO\_TEMPLATE\_%' ESCAPE '\'
ORDER BY "Code";
*/
