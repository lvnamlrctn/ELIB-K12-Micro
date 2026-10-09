/* =============================================================================
   Thêm 4 module CMS còn thiếu (Trang tĩnh/Video/Liên hệ/Nhóm liên hệ — cha 'NEWS', backend
   PageController/VideoController/ContactController/ContactGroupController đã có sẵn, chỉ thiếu
   frontend + module seed) vào cms.Module, cấp full quyền cho user 'admin'/'admin_pdp'.
   Script idempotent — chạy lại không lỗi, không duplicate.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

-- 1a. Thêm 4 module (nếu chưa có)
IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'PAGE')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'NEWS'),
        N'Trang tĩnh', 'PAGE', 'pages', '/admin/page', 11, 1);
    PRINT N'Đã thêm module PAGE.';
END ELSE PRINT N'Module PAGE đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'VIDEO')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'NEWS'),
        N'Video', 'VIDEO', 'video_library', '/admin/video', 12, 1);
    PRINT N'Đã thêm module VIDEO.';
END ELSE PRINT N'Module VIDEO đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'CONTACT')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'NEWS'),
        N'Liên hệ', 'CONTACT', 'contact_mail', '/admin/contact', 13, 1);
    PRINT N'Đã thêm module CONTACT.';
END ELSE PRINT N'Module CONTACT đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'CONTACTGROUP')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'NEWS'),
        N'Nhóm liên hệ', 'CONTACTGROUP', 'contacts', '/admin/contactgroup', 14, 1);
    PRINT N'Đã thêm module CONTACTGROUP.';
END ELSE PRINT N'Module CONTACTGROUP đã tồn tại — bỏ qua.';

-- 1b. Cấp full quyền cho admin/admin_pdp trên cả 4 module (nếu chưa có)
INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LoginName IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode IN ('PAGE', 'VIDEO', 'CONTACT', 'CONTACTGROUP')
  AND NOT EXISTS (
    SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id
  );

PRINT N'Đã cấp quyền PAGE/VIDEO/CONTACT/CONTACTGROUP cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.SortOrder
FROM [cms].[Module] m WHERE m.ModuleCode IN ('PAGE', 'VIDEO', 'CONTACT', 'CONTACTGROUP');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
-- 2a. Sync sequence (phòng trường hợp data import bên ngoài)
SELECT setval('cms."Module_Id_seq"', (SELECT COALESCE(MAX("Id"),0) FROM cms."Module"));
SELECT setval('cms."Permission_Id_seq"', (SELECT COALESCE(MAX("Id"),0) FROM cms."Permission"));

-- 2b. Thêm 4 module (nếu chưa có)
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid()::text, (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'NEWS'),
    'Trang tĩnh', 'PAGE', 'pages', '/admin/page', 11, 1
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'PAGE');

INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid()::text, (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'NEWS'),
    'Video', 'VIDEO', 'video_library', '/admin/video', 12, 1
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'VIDEO');

INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid()::text, (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'NEWS'),
    'Liên hệ', 'CONTACT', 'contact_mail', '/admin/contact', 13, 1
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'CONTACT');

INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid()::text, (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'NEWS'),
    'Nhóm liên hệ', 'CONTACTGROUP', 'contacts', '/admin/contactgroup', 14, 1
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'CONTACTGROUP');

-- 2c. Cấp full quyền cho admin/admin_pdp trên cả 4 module (nếu chưa có)
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid()::text, u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE "LoginName" IN ('admin', 'admin_pdp') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" IN ('PAGE', 'VIDEO', 'CONTACT', 'CONTACTGROUP')
  AND NOT EXISTS (
    SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id"
  );

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "SortOrder"
FROM cms."Module" WHERE "ModuleCode" IN ('PAGE', 'VIDEO', 'CONTACT', 'CONTACTGROUP');
*/
