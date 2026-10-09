/* =============================================================================
   Thêm 3 module con của 'ADMIN_TASK' (Đợt 13 — giám sát tác vụ người khác + dọn payload/result cũ).
   KHÔNG có mục menu riêng — Link là URL giả lập (không khớp route Angular thật), chỉ dùng làm khoá tra
   quyền cho `*appCan="'view'; url: '...'"` trong 2 tab mới của trang /admin/admin-tasks (xem
   PermissionService.build() chỉ index module có Link khác NULL — module Link=NULL sẽ không bao giờ khớp
   được appCan dù đã cấp quyền, đúng bài học rút ra khi khảo sát Đợt 13).

   - ADMIN_TASK_MONITOR    -> quyền 'view' cho tab Giám sát
   - ADMIN_TASK_CONTROL    -> quyền 'edit' cho nút Tạm dừng/Tiếp tục trong tab Giám sát (tách khỏi MONITOR
                              để có thể cấp "chỉ xem" mà không cho can thiệp)
   - ADMIN_TASK_RETENTION  -> quyền 'view'/'edit' cho tab Dọn dữ liệu

   Cấp full quyền cho user 'admin'/'admin_pdp' (đúng phạm vi hẹp như chính ADMIN_TASK — quyền nhìn/can
   thiệp xuyên-tenant, không cấp rộng). Script idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.

   LƯU Ý: Status chèn đúng NGAY TỪ ĐẦU = 2 (Hoạt động), không phải 1 (Ẩn) — xem add-module-admin-task.sql.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

DECLARE @parentId BIGINT = (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'ADMIN_TASK');

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'ADMIN_TASK_MONITOR')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), @parentId, N'Giám sát tác vụ', 'ADMIN_TASK_MONITOR', 'monitoring', '/admin/admin-tasks/monitor', 1, 2);
    PRINT N'Đã thêm module ADMIN_TASK_MONITOR.';
END ELSE PRINT N'Module ADMIN_TASK_MONITOR đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'ADMIN_TASK_CONTROL')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), @parentId, N'Can thiệp tác vụ', 'ADMIN_TASK_CONTROL', 'settings_backup_restore', '/admin/admin-tasks/monitor/control', 2, 2);
    PRINT N'Đã thêm module ADMIN_TASK_CONTROL.';
END ELSE PRINT N'Module ADMIN_TASK_CONTROL đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'ADMIN_TASK_RETENTION')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), @parentId, N'Dọn dữ liệu cũ', 'ADMIN_TASK_RETENTION', 'auto_delete', '/admin/admin-tasks/retention', 3, 2);
    PRINT N'Đã thêm module ADMIN_TASK_RETENTION.';
END ELSE PRINT N'Module ADMIN_TASK_RETENTION đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode IN ('ADMIN_TASK_MONITOR', 'ADMIN_TASK_CONTROL', 'ADMIN_TASK_RETENTION') AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LOWER(TRIM(LoginName)) IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode IN ('ADMIN_TASK_MONITOR', 'ADMIN_TASK_CONTROL', 'ADMIN_TASK_RETENTION')
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền 3 module Đợt 13 cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link, m.Status FROM [cms].[Module] m
WHERE m.ModuleCode IN ('ADMIN_TASK_MONITOR', 'ADMIN_TASK_CONTROL', 'ADMIN_TASK_RETENTION');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
DO $$
DECLARE parent_id BIGINT := (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'ADMIN_TASK');
BEGIN
    INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
    SELECT gen_random_uuid(), parent_id, 'Giám sát tác vụ', 'ADMIN_TASK_MONITOR', 'monitoring', '/admin/admin-tasks/monitor', 1, 2
    WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'ADMIN_TASK_MONITOR');

    INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
    SELECT gen_random_uuid(), parent_id, 'Can thiệp tác vụ', 'ADMIN_TASK_CONTROL', 'settings_backup_restore', '/admin/admin-tasks/monitor/control', 2, 2
    WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'ADMIN_TASK_CONTROL');

    INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
    SELECT gen_random_uuid(), parent_id, 'Dọn dữ liệu cũ', 'ADMIN_TASK_RETENTION', 'auto_delete', '/admin/admin-tasks/retention', 3, 2
    WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'ADMIN_TASK_RETENTION');
END $$;

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" IN ('ADMIN_TASK_MONITOR', 'ADMIN_TASK_CONTROL', 'ADMIN_TASK_RETENTION') AND "Status" <> 2;

-- LƯU Ý: "LoginName" là cột padding/fixed-length trên CSDL thật ('admin               ') và tài khoản PDP
-- viết hoa 'admin_PDP' — dùng TRIM+ILIKE để so khớp đúng, so khớp tuyệt đối IN (...) sẽ chèn 0 dòng
-- âm thầm.
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE (TRIM("LoginName") ILIKE 'admin' OR TRIM("LoginName") ILIKE 'admin_pdp') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" IN ('ADMIN_TASK_MONITOR', 'ADMIN_TASK_CONTROL', 'ADMIN_TASK_RETENTION')
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link", "Status" FROM cms."Module"
WHERE "ModuleCode" IN ('ADMIN_TASK_MONITOR', 'ADMIN_TASK_CONTROL', 'ADMIN_TASK_RETENTION');
*/
