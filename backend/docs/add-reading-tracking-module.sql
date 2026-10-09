/* =============================================================================
   Thêm module READING_TRACKING vào cms.Module + cấp quyền cho admin users.
   Script idempotent — chạy lại không lỗi, không duplicate.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

-- 1a. Thêm module READING_TRACKING (nếu chưa có)
IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'READING_TRACKING')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (
        NEWID(),
        (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'DOC_STORE'),
        N'Theo dõi đọc tài liệu',
        'READING_TRACKING',
        'auto_stories',
        '/admin/reading-tracking',
        3,
        1
    );
    PRINT N'Đã thêm module READING_TRACKING.';
END
ELSE
    PRINT N'Module READING_TRACKING đã tồn tại — bỏ qua.';

-- 1b. Cấp quyền cho user lvnam và admin (nếu chưa có)
INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (
    SELECT Id FROM [dbo].[Users]
    WHERE LoginName IN ('lvnam', 'admin') AND IsDelete != 2
) u
WHERE m.ModuleCode = 'READING_TRACKING'
  AND NOT EXISTS (
    SELECT 1 FROM [cms].[Permission] p
    WHERE p.UserId = u.Id AND p.ModuleId = m.Id
  );

PRINT N'Đã cấp quyền READING_TRACKING cho lvnam/admin (nếu chưa có).';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.SortOrder
FROM [cms].[Module] m WHERE m.ModuleCode = 'READING_TRACKING';

SELECT p.UserId, u.LoginName, p.ModuleId, p.Can_View, p.Can_Add, p.Can_Edit, p.Can_Delete
FROM [cms].[Permission] p
JOIN [dbo].[Users] u ON u.Id = p.UserId
JOIN [cms].[Module] m ON m.Id = p.ModuleId
WHERE m.ModuleCode = 'READING_TRACKING';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
-- 2a. Thêm module READING_TRACKING (nếu chưa có)
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT
    gen_random_uuid()::text,
    (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'DOC_STORE'),
    'Theo dõi đọc tài liệu',
    'READING_TRACKING',
    'auto_stories',
    '/admin/reading-tracking',
    3,
    1
WHERE NOT EXISTS (
    SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'READING_TRACKING'
);

-- 2b. Cấp quyền cho user lvnam và admin (nếu chưa có)
INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT
    gen_random_uuid()::text,
    u."Id",
    m."Id",
    2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (
    SELECT "Id" FROM public."Users"
    WHERE TRIM("LoginName") IN ('lvnam', 'admin') AND "IsDelete" != 2
) u
WHERE m."ModuleCode" = 'READING_TRACKING'
  AND NOT EXISTS (
    SELECT 1 FROM cms."Permission" p
    WHERE p."UserId" = u."Id" AND p."ModuleId" = m."Id"
  );

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "SortOrder"
FROM cms."Module" WHERE "ModuleCode" = 'READING_TRACKING';

SELECT p."UserId", u."LoginName", p."ModuleId", p."Can_View", p."Can_Add", p."Can_Edit", p."Can_Delete"
FROM cms."Permission" p
JOIN public."Users" u ON u."Id" = p."UserId"
JOIN cms."Module" m ON m."Id" = p."ModuleId"
WHERE m."ModuleCode" = 'READING_TRACKING';
*/
