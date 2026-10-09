/* =============================================================================
   Đợt 8 (sửa lại) — Thêm module DONVI (Đơn vị đào tạo — Khoa/Viện/Trường, /admin/subject-units).
   Đặt ngang hàng (ParentId=NULL) với EVALUATEDEGREE/EVALUATEPROGRAM/NGANHHOC/MONHOC/SUBJECT_TREE.
   Cấp full quyền cho user 'admin'/'admin_pdp'. Script idempotent.

   SỬA 2026-09-20 (phát hiện qua test UI trực tiếp): Status ban đầu chèn nhầm = 1 (Ẩn) thay vì 2
   (Hoạt động) — đúng convention toàn hệ thống (1=Ẩn, 2=Hoạt động, xem cùng lỗi đã sửa ở Giai đoạn 3
   Đợt 7 cho Reader.Status). Vì [dbo].[Users]/UsersRepository.GetPermissionsAsync lọc
   `m.Status == 2`, module Status=1 khiến toàn bộ Permission của module này bị loại khỏi API
   GetPermission — nút "Thêm" trên trang /admin/subject-units không bao giờ hiện ra, dù đã cấp quyền
   Can_Add=2 đúng. Đã thêm UPDATE bên dưới để tự sửa trên môi trường đã lỡ chạy script cũ.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'DONVI')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), NULL, N'Đơn vị đào tạo', 'DONVI', 'corporate_fare', '/admin/subject-units', 5, 2);
    PRINT N'Đã thêm module DONVI.';
END ELSE PRINT N'Module DONVI đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode = 'DONVI' AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE (LTRIM(RTRIM(LoginName)) = 'admin' OR LTRIM(RTRIM(LoginName)) = 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode = 'DONVI'
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền DONVI cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link FROM [cms].[Module] m WHERE m.ModuleCode = 'DONVI';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   ============================================================================= */

INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), NULL, 'Đơn vị đào tạo', 'DONVI', 'corporate_fare', '/admin/subject-units', 5, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'DONVI');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" = 'DONVI' AND "Status" <> 2;

INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE (TRIM("LoginName") = 'admin' OR TRIM("LoginName") = 'admin_pdp' OR TRIM("LoginName") = 'admin_PDP') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" = 'DONVI'
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link" FROM cms."Module" WHERE "ModuleCode" = 'DONVI';
