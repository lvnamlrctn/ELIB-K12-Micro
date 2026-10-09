/* =============================================================================
   Thêm module ADMIN_TASK (con của 'SYSTEM') — trang quản trị tác vụ nền (Đợt 10, "AdminTask v2":
   nhập/cập nhật độc giả hàng loạt chạy nền, xem trước → xác nhận). Cấp full quyền cho user
   'admin'/'admin_pdp'. Script idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.

   LƯU Ý (rút kinh nghiệm từ lỗi Status=1 đã gặp 9 lần ở Đợt 6/8/9 — xem add-module-donvi.sql): Status
   chèn đúng NGAY TỪ ĐẦU = 2 (Hoạt động), không phải 1 (Ẩn) — nếu không, nút "Thêm/Sửa/Xóa" trên trang
   /admin/admin-tasks sẽ không bao giờ hiện dù đã cấp quyền đúng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'ADMIN_TASK')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'SYSTEM'),
        N'Tác vụ nền', 'ADMIN_TASK', 'pending_actions', '/admin/admin-tasks', 8, 2);
    PRINT N'Đã thêm module ADMIN_TASK.';
END ELSE PRINT N'Module ADMIN_TASK đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode = 'ADMIN_TASK' AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LOWER(TRIM(LoginName)) IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode = 'ADMIN_TASK'
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền ADMIN_TASK cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link, m.Status FROM [cms].[Module] m WHERE m.ModuleCode = 'ADMIN_TASK';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'SYSTEM'),
    'Tác vụ nền', 'ADMIN_TASK', 'pending_actions', '/admin/admin-tasks', 8, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'ADMIN_TASK');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" = 'ADMIN_TASK' AND "Status" <> 2;

-- LƯU Ý: "LoginName" là cột padding/fixed-length trên CSDL thật ('admin               ') và tài khoản PDP
-- viết hoa 'admin_PDP' — dùng TRIM+ILIKE để so khớp đúng, so khớp tuyệt đối IN (...) sẽ chèn 0 dòng
-- âm thầm.
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE (TRIM("LoginName") ILIKE 'admin' OR TRIM("LoginName") ILIKE 'admin_pdp') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" = 'ADMIN_TASK'
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link", "Status" FROM cms."Module" WHERE "ModuleCode" = 'ADMIN_TASK';
*/
