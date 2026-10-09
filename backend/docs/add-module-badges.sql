/* =============================================================================
   Thêm module BADGES (con của 'READERS') — tra quyền cho màn quản trị "Huy hiệu đọc"
   (frontend: /admin/badges). Cấp full quyền cho user 'admin'/'admin_pdp'. Script idempotent.

   Chọn READERS làm module cha vì Huy hiệu đọc (Gamification) là tính năng gắn liền với hoạt động
   đọc/mượn của bạn đọc — cùng nhóm nghiệp vụ quản lý bạn đọc, tương tự cách STAT_CHAT được parent dưới
   DASHBOARD dù không phải là 1 link điều hướng con thật sự trong menu.ts (xem add-module-statchat.sql).
   Điều hướng thật trong menu.ts đặt mục "Huy hiệu đọc" trong nhóm CIRCULATION cạnh READERS, không lệ
   thuộc vào ParentId này — ParentId ở đây chỉ mang tính phân loại/tài liệu hoá.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.

   SỬA 2026-09-20 (phát hiện qua test UI trực tiếp Đợt 9, lỗi có từ Đợt 6): Status chèn nhầm = 1 (Ẩn)
   thay vì 2 (Hoạt động) — UsersRepository.GetPermissionsAsync lọc `m.Status == 2` nên toàn bộ quyền
   Can_Add/Can_Edit/Can_Delete của module này bị loại khỏi API GetPermission dù đã cấp đúng, khiến nút
   "Thêm/Sửa/Xóa" trên trang quản trị liên quan không bao giờ hiện. Cùng lỗi ở 6 module khác seed cùng
   Đợt 6 (STAT_CHAT, SCHEDULED_REPORT, SMS_ZALO_CONFIG, NOTIFICATION_LOG, STUDY_ROOM_BOOKING,
   ROOM_BOOKING_CONFIG) — đã sửa riêng từng file. Thêm UPDATE bên dưới để tự sửa môi trường đã lỡ chạy.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'BADGES')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'READERS'),
        N'Huy hiệu đọc', 'BADGES', 'military_tech', '/admin/badges', 1, 2);
    PRINT N'Đã thêm module BADGES.';
END ELSE PRINT N'Module BADGES đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode = 'BADGES' AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LoginName IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode = 'BADGES'
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền BADGES cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link FROM [cms].[Module] m WHERE m.ModuleCode = 'BADGES';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'READERS'),
    'Huy hiệu đọc', 'BADGES', 'military_tech', '/admin/badges', 1, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'BADGES');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" = 'BADGES' AND "Status" <> 2;

-- LƯU Ý: LoginName trong Users là CHAR có padding khoảng trắng cuối chuỗi (và admin_PDP viết hoa
-- 'PDP'), nên so khớp "LoginName IN ('admin','admin_pdp')" tuyệt đối KHÔNG khớp — phải TRIM + ILIKE.
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE TRIM("LoginName") ILIKE 'admin' OR TRIM("LoginName") ILIKE 'admin_pdp') u
WHERE m."ModuleCode" = 'BADGES'
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link" FROM cms."Module" WHERE "ModuleCode" = 'BADGES';
*/
