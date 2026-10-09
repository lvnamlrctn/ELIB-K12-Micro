/* =============================================================================
   Đợt 15 — Trung tâm công việc: thêm module DOCUMENT_SUBMISSION vào cms.Module + cấp quyền cho user
   đã có quyền DOC_STORE hoặc EBOOKREVIEW (nhóm cán bộ quản lý tài liệu số) — nguồn "Tài liệu nộp" trước
   đây hoàn toàn chưa có quyền admin nào (chỉ có API bạn đọc gửi/xem của mình). Không có trang CRUD riêng
   ngoài Trung tâm công việc (/admin/work-center) — Link trỏ thẳng route đó.
   Script idempotent — chạy lại không lỗi, không duplicate.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'DOCUMENT_SUBMISSION')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (
        NEWID(),
        (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'DOC_STORE'),
        N'Tài liệu nộp (Trung tâm công việc)',
        'DOCUMENT_SUBMISSION',
        'upload_file',
        '/admin/work-center',
        5,
        1
    );
    PRINT N'Đã thêm module DOCUMENT_SUBMISSION.';
END
ELSE
    PRINT N'Module DOCUMENT_SUBMISSION đã tồn tại — bỏ qua.';

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), grp.UserId, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (
    SELECT DISTINCT p.UserId
    FROM [cms].[Permission] p
    JOIN [cms].[Module] m2 ON m2.Id = p.ModuleId
    WHERE m2.ModuleCode IN ('DOC_STORE', 'EBOOKREVIEW') AND p.Can_Access = 2
) grp
WHERE m.ModuleCode = 'DOCUMENT_SUBMISSION'
  AND NOT EXISTS (
    SELECT 1 FROM [cms].[Permission] x
    WHERE x.UserId = grp.UserId AND x.ModuleId = m.Id
  );

PRINT N'Đã cấp quyền DOCUMENT_SUBMISSION cho users có quyền DOC_STORE/EBOOKREVIEW.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.SortOrder
FROM [cms].[Module] m WHERE m.ModuleCode = 'DOCUMENT_SUBMISSION';

SELECT p.UserId, u.LoginName, p.ModuleId, p.Can_View, p.Can_Add, p.Can_Edit, p.Can_Delete
FROM [cms].[Permission] p
JOIN [dbo].[Users] u ON u.Id = p.UserId
JOIN [cms].[Module] m ON m.Id = p.ModuleId
WHERE m.ModuleCode = 'DOCUMENT_SUBMISSION';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
-- 2a. Sync sequence (phòng trường hợp data import bên ngoài)
SELECT setval('cms."Module_Id_seq"', (SELECT COALESCE(MAX("Id"),0) FROM cms."Module"));
SELECT setval('cms."Permission_Id_seq"', (SELECT COALESCE(MAX("Id"),0) FROM cms."Permission"));

-- 2b. Thêm module DOCUMENT_SUBMISSION (nếu chưa có)
-- LƯU Ý: cms.Module."PublicId"/cms.Permission."PublicId" là kiểu uuid trên CSDL live (khác giả định ::text
-- kiểu cũ của add-ebookreview-module.sql — đã xác nhận trực tiếp khi chạy script này, sửa lại cho đúng).
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT
    gen_random_uuid(),
    (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'DOC_STORE'),
    'Tài liệu nộp (Trung tâm công việc)',
    'DOCUMENT_SUBMISSION',
    'upload_file',
    '/admin/work-center',
    5,
    1
WHERE NOT EXISTS (
    SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'DOCUMENT_SUBMISSION'
);

-- 2c. Cấp quyền cho user đã có quyền DOC_STORE hoặc EBOOKREVIEW (nếu chưa có)
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT
    gen_random_uuid(),
    grp."UserId",
    m."Id",
    2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (
    SELECT DISTINCT p."UserId"
    FROM cms."Permission" p
    JOIN cms."Module" m2 ON m2."Id" = p."ModuleId"
    WHERE m2."ModuleCode" IN ('DOC_STORE', 'EBOOKREVIEW') AND p."Can_Access" = 2
) grp
WHERE m."ModuleCode" = 'DOCUMENT_SUBMISSION'
  AND NOT EXISTS (
    SELECT 1 FROM cms."Permission" x
    WHERE x."UserId" = grp."UserId" AND x."ModuleId" = m."Id"
  );

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "SortOrder"
FROM cms."Module" WHERE "ModuleCode" = 'DOCUMENT_SUBMISSION';

SELECT p."UserId", u."LoginName", p."ModuleId", p."Can_View", p."Can_Add", p."Can_Edit", p."Can_Delete"
FROM cms."Permission" p
JOIN public."Users" u ON u."Id" = p."UserId"
JOIN cms."Module" m ON m."Id" = p."ModuleId"
WHERE m."ModuleCode" = 'DOCUMENT_SUBMISSION';
*/
