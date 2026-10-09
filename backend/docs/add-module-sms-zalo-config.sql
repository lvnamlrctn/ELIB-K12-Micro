/* =============================================================================
   Thêm 2 module con của 'SYSTEM' cho hạ tầng SMS/Zalo ZNS (Đợt 6, Giai đoạn 2):
   - SMS_ZALO_CONFIG — cấu hình access-token SpeedSMS/Zalo OA theo tenant.
   - NOTIFICATION_LOG — tra cứu lịch sử gửi SMS/Zalo (chỉ đọc).
   Cấp full quyền cho user 'admin'/'admin_pdp'. Script idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.

   SỬA 2026-09-20 (phát hiện qua test UI trực tiếp Đợt 9): Status chèn nhầm = 1 (Ẩn) thay vì 2 (Hoạt
   động) cho cả 2 module — khiến nút "Thêm/Sửa/Xóa" trên 2 trang quản trị liên quan không bao giờ hiện
   dù đã cấp quyền đúng (xem add-module-donvi.sql để biết chi tiết nguyên nhân). Thêm UPDATE bên dưới
   để tự sửa môi trường đã lỡ chạy bản cũ.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'SMS_ZALO_CONFIG')
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'SYSTEM'),
        N'Cấu hình SMS/Zalo', 'SMS_ZALO_CONFIG', 'sms', '/admin/notification-channel-config', 90, 2);

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'NOTIFICATION_LOG')
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'SYSTEM'),
        N'Lịch sử gửi SMS/Zalo', 'NOTIFICATION_LOG', 'history', '/admin/notification-log', 91, 2);

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode IN ('SMS_ZALO_CONFIG', 'NOTIFICATION_LOG') AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
-- LoginName là cột padding/fixed-length trên CSDL thật ('admin               ') — phải TRIM trước khi
-- so khớp, nếu không chèn 0 dòng âm thầm.
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LOWER(TRIM(LoginName)) IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode IN ('SMS_ZALO_CONFIG', 'NOTIFICATION_LOG')
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã thêm module SMS_ZALO_CONFIG/NOTIFICATION_LOG và cấp quyền admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link FROM [cms].[Module] m WHERE m.ModuleCode IN ('SMS_ZALO_CONFIG', 'NOTIFICATION_LOG');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'SYSTEM'),
    'Cấu hình SMS/Zalo', 'SMS_ZALO_CONFIG', 'sms', '/admin/notification-channel-config', 90, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'SMS_ZALO_CONFIG');

INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'SYSTEM'),
    'Lịch sử gửi SMS/Zalo', 'NOTIFICATION_LOG', 'history', '/admin/notification-log', 91, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'NOTIFICATION_LOG');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" IN ('SMS_ZALO_CONFIG', 'NOTIFICATION_LOG') AND "Status" <> 2;

-- LƯU Ý: "LoginName" là cột padding/fixed-length trên CSDL thật ('admin               ') và tài khoản
-- PDP viết hoa 'admin_PDP' (không phải 'admin_pdp') — so khớp tuyệt đối IN (...) như bản gốc
-- add-module-statchat.sql chèn ĐÚNG 0 dòng một cách âm thầm. Dùng TRIM+ILIKE để so khớp đúng.
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE (TRIM("LoginName") ILIKE 'admin' OR TRIM("LoginName") ILIKE 'admin_pdp') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" IN ('SMS_ZALO_CONFIG', 'NOTIFICATION_LOG')
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link" FROM cms."Module" WHERE "ModuleCode" IN ('SMS_ZALO_CONFIG', 'NOTIFICATION_LOG');
*/
