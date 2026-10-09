/* =============================================================================
   Thêm module STAT_CHAT (con của 'DASHBOARD') — chỉ dùng để tra quyền cho widget
   trợ lý thống kê AdminStatChat, không phải route điều hướng thật.
   Cấp full quyền cho user 'admin'/'admin_pdp'. Script idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.

   SỬA 2026-09-20 (phát hiện qua test UI trực tiếp Đợt 9): Status chèn nhầm = 1 (Ẩn) thay vì 2 (Hoạt
   động) — khiến nút "Thêm/Sửa/Xóa" liên quan không bao giờ hiện dù đã cấp quyền đúng (xem
   add-module-donvi.sql để biết chi tiết nguyên nhân). Thêm UPDATE bên dưới để tự sửa môi trường đã
   lỡ chạy bản cũ.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'STAT_CHAT')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'DASHBOARD'),
        N'Trợ lý thống kê', 'STAT_CHAT', 'smart_toy', '/admin/stat-chat', 2, 2);
    PRINT N'Đã thêm module STAT_CHAT.';
END ELSE PRINT N'Module STAT_CHAT đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode = 'STAT_CHAT' AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LoginName IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode = 'STAT_CHAT'
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền STAT_CHAT cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link FROM [cms].[Module] m WHERE m.ModuleCode = 'STAT_CHAT';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'DASHBOARD'),
    'Trợ lý thống kê', 'STAT_CHAT', 'smart_toy', '/admin/stat-chat', 2, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'STAT_CHAT');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" = 'STAT_CHAT' AND "Status" <> 2;

INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE "LoginName" IN ('admin', 'admin_pdp') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" = 'STAT_CHAT'
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link" FROM cms."Module" WHERE "ModuleCode" = 'STAT_CHAT';
*/
