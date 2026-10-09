/* =============================================================================
   Thêm 2 module con của 'STORE_MGMT' (Quản lý Kho — cha hiện tại của MAP_OBJECT/MAP_VISUAL, xem
   frontend/src/app/services/menu.ts) cho tính năng Đặt phòng học nhóm (Feature C):
   - STUDY_ROOM_BOOKING  — màn duyệt/từ chối yêu cầu đặt phòng (/admin/study-room-bookings).
   - ROOM_BOOKING_CONFIG — màn cấu hình phòng khả đặt (/admin/room-booking-config).
   Cấp full quyền cho user 'admin'/'admin_pdp' (so khớp không phân biệt hoa/thường — LoginName thực tế
   trên môi trường đang chạy là 'admin_PDP'). Script idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.

   SỬA 2026-09-20 (phát hiện qua test UI trực tiếp Đợt 9): Status chèn nhầm = 1 (Ẩn) thay vì 2
   (Hoạt động) cho cả 2 module — khiến nút "Thêm/Sửa/Xóa" trên 2 trang quản trị liên quan không bao
   giờ hiện dù đã cấp quyền đúng (xem add-module-donvi.sql để biết chi tiết nguyên nhân). Thêm UPDATE
   bên dưới để tự sửa môi trường đã lỡ chạy bản cũ.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'STUDY_ROOM_BOOKING')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'STORE_MGMT'),
        N'Duyệt đặt phòng học nhóm', 'STUDY_ROOM_BOOKING', 'event_available', '/admin/study-room-bookings', 20, 2);
    PRINT N'Đã thêm module STUDY_ROOM_BOOKING.';
END ELSE PRINT N'Module STUDY_ROOM_BOOKING đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'ROOM_BOOKING_CONFIG')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'STORE_MGMT'),
        N'Cấu hình phòng học nhóm', 'ROOM_BOOKING_CONFIG', 'meeting_room', '/admin/room-booking-config', 21, 2);
    PRINT N'Đã thêm module ROOM_BOOKING_CONFIG.';
END ELSE PRINT N'Module ROOM_BOOKING_CONFIG đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode IN ('STUDY_ROOM_BOOKING', 'ROOM_BOOKING_CONFIG') AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
-- LoginName là cột padding/fixed-length trên CSDL thật ('admin               ') — LOWER() không đủ,
-- phải TRIM trước khi so khớp, nếu không chèn 0 dòng âm thầm.
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LOWER(TRIM(LoginName)) IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode IN ('STUDY_ROOM_BOOKING', 'ROOM_BOOKING_CONFIG')
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền STUDY_ROOM_BOOKING/ROOM_BOOKING_CONFIG cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link, m.ParentId FROM [cms].[Module] m WHERE m.ModuleCode IN ('STUDY_ROOM_BOOKING', 'ROOM_BOOKING_CONFIG');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'STORE_MGMT'),
    'Duyệt đặt phòng học nhóm', 'STUDY_ROOM_BOOKING', 'event_available', '/admin/study-room-bookings', 20, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'STUDY_ROOM_BOOKING');

INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'STORE_MGMT'),
    'Cấu hình phòng học nhóm', 'ROOM_BOOKING_CONFIG', 'meeting_room', '/admin/room-booking-config', 21, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'ROOM_BOOKING_CONFIG');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" IN ('STUDY_ROOM_BOOKING', 'ROOM_BOOKING_CONFIG') AND "Status" <> 2;

INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE LOWER(TRIM("LoginName")) IN ('admin', 'admin_pdp') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" IN ('STUDY_ROOM_BOOKING', 'ROOM_BOOKING_CONFIG')
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link", "ParentId" FROM cms."Module" WHERE "ModuleCode" IN ('STUDY_ROOM_BOOKING', 'ROOM_BOOKING_CONFIG');
*/
