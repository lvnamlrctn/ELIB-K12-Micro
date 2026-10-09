/* =============================================================================
   Đợt 8 — Thêm module SUBJECT_TREE (Cây cơ sở môn học, đọc-only, /admin/subject-tree).
   Đặt ngang hàng (ParentId=NULL) với EVALUATEDEGREE/EVALUATEPROGRAM/NGANHHOC/MONHOC — 4 module này
   trong CSDL live đều ParentId=NULL, nhóm "Cơ sở môn học" ở menu FE chỉ là gom nhóm phía client
   (menu.ts), không có module cha thật trong cms.Module.
   Cấp full quyền cho user 'admin'/'admin_pdp' — dùng TRIM(...) ILIKE thay vì so khớp chính xác vì
   LoginName là cột padded/fixed-length (đã gặp bug insert 0 dòng quyền ở các script trước). Script
   idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL

   SỬA 2026-09-20 (phát hiện qua test UI trực tiếp, cùng lỗi với add-module-donvi.sql): Status ban đầu
   chèn nhầm = 1 (Ẩn) thay vì 2 (Hoạt động) — UsersRepository.GetPermissionsAsync lọc `m.Status == 2`
   nên toàn bộ Permission của module Status=1 bị loại khỏi API GetPermission dù đã cấp Can_*=2 đúng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'SUBJECT_TREE')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), NULL, N'Cây cơ sở môn học', 'SUBJECT_TREE', 'account_tree', '/admin/subject-tree', 6, 2);
    PRINT N'Đã thêm module SUBJECT_TREE.';
END ELSE PRINT N'Module SUBJECT_TREE đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode = 'SUBJECT_TREE' AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE (LTRIM(RTRIM(LoginName)) = 'admin' OR LTRIM(RTRIM(LoginName)) = 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode = 'SUBJECT_TREE'
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền SUBJECT_TREE cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link FROM [cms].[Module] m WHERE m.ModuleCode = 'SUBJECT_TREE';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   ============================================================================= */

INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), NULL, 'Cây cơ sở môn học', 'SUBJECT_TREE', 'account_tree', '/admin/subject-tree', 6, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'SUBJECT_TREE');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" = 'SUBJECT_TREE' AND "Status" <> 2;

INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE (TRIM("LoginName") = 'admin' OR TRIM("LoginName") = 'admin_pdp' OR TRIM("LoginName") = 'admin_PDP') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" = 'SUBJECT_TREE'
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link" FROM cms."Module" WHERE "ModuleCode" = 'SUBJECT_TREE';
