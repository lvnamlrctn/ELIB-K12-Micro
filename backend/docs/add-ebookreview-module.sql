/* =============================================================================
   Thêm module EBOOKREVIEW vào cms.Module + cấp quyền cho tất cả user
   đã có quyền READING_TRACKING.
   Script idempotent — chạy lại không lỗi, không duplicate.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

-- 1a. Thêm module EBOOKREVIEW (nếu chưa có)
IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'EBOOKREVIEW')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (
        NEWID(),
        (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'DOC_STORE'),
        N'Quản lý bình luận',
        'EBOOKREVIEW',
        'rate_review',
        '/admin/ebook-review',
        4,
        1
    );
    PRINT N'Đã thêm module EBOOKREVIEW.';
END
ELSE
    PRINT N'Module EBOOKREVIEW đã tồn tại — bỏ qua.';

-- 1b. Cấp quyền cho tất cả user đã có quyền READING_TRACKING (nếu chưa có)
INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), rt.UserId, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (
    SELECT DISTINCT p.UserId
    FROM [cms].[Permission] p
    JOIN [cms].[Module] m2 ON m2.Id = p.ModuleId
    WHERE m2.ModuleCode = 'READING_TRACKING' AND p.Can_Access = 2
) rt
WHERE m.ModuleCode = 'EBOOKREVIEW'
  AND NOT EXISTS (
    SELECT 1 FROM [cms].[Permission] x
    WHERE x.UserId = rt.UserId AND x.ModuleId = m.Id
  );

PRINT N'Đã cấp quyền EBOOKREVIEW cho users có quyền READING_TRACKING.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.SortOrder
FROM [cms].[Module] m WHERE m.ModuleCode = 'EBOOKREVIEW';

SELECT p.UserId, u.LoginName, p.ModuleId, p.Can_View, p.Can_Add, p.Can_Edit, p.Can_Delete
FROM [cms].[Permission] p
JOIN [dbo].[Users] u ON u.Id = p.UserId
JOIN [cms].[Module] m ON m.Id = p.ModuleId
WHERE m.ModuleCode = 'EBOOKREVIEW';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
-- 2a. Sync sequence (phòng trường hợp data import bên ngoài)
SELECT setval('cms."Module_Id_seq"', (SELECT COALESCE(MAX("Id"),0) FROM cms."Module"));
SELECT setval('cms."Permission_Id_seq"', (SELECT COALESCE(MAX("Id"),0) FROM cms."Permission"));

-- 2b. Thêm module EBOOKREVIEW (nếu chưa có)
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT
    gen_random_uuid()::text,
    (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'DOC_STORE'),
    'Quản lý bình luận',
    'EBOOKREVIEW',
    'rate_review',
    '/admin/ebook-review',
    4,
    1
WHERE NOT EXISTS (
    SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'EBOOKREVIEW'
);

-- 2c. Cấp quyền cho tất cả user đã có quyền READING_TRACKING (nếu chưa có)
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT
    gen_random_uuid()::text,
    rt."UserId",
    m."Id",
    2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (
    SELECT DISTINCT p."UserId"
    FROM cms."Permission" p
    JOIN cms."Module" m2 ON m2."Id" = p."ModuleId"
    WHERE m2."ModuleCode" = 'READING_TRACKING' AND p."Can_Access" = 2
) rt
WHERE m."ModuleCode" = 'EBOOKREVIEW'
  AND NOT EXISTS (
    SELECT 1 FROM cms."Permission" x
    WHERE x."UserId" = rt."UserId" AND x."ModuleId" = m."Id"
  );

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "SortOrder"
FROM cms."Module" WHERE "ModuleCode" = 'EBOOKREVIEW';

SELECT p."UserId", u."LoginName", p."ModuleId", p."Can_View", p."Can_Add", p."Can_Edit", p."Can_Delete"
FROM cms."Permission" p
JOIN public."Users" u ON u."Id" = p."UserId"
JOIN cms."Module" m ON m."Id" = p."ModuleId"
WHERE m."ModuleCode" = 'EBOOKREVIEW';
*/
