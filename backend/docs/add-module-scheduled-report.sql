/* =============================================================================
   Thêm module SCHEDULED_REPORT (con của 'SYSTEM') — quyền cho màn hình cấu hình
   báo cáo định kỳ tự động gửi email (ScheduledReportController).
   Cấp full quyền cho user 'admin'/'admin_pdp'. Script idempotent.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.

   SỬA 2026-09-20 (phát hiện qua test UI trực tiếp Đợt 9): Status chèn nhầm = 1 (Ẩn) thay vì 2 (Hoạt
   động) — khiến nút "Thêm/Sửa/Xóa" trên trang quản trị liên quan không bao giờ hiện dù đã cấp quyền
   đúng (xem add-module-donvi.sql để biết chi tiết nguyên nhân). Thêm UPDATE bên dưới để tự sửa môi
   trường đã lỡ chạy bản cũ.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [cms].[Module] WHERE ModuleCode = 'SCHEDULED_REPORT')
BEGIN
    INSERT INTO [cms].[Module] (PublicId, ParentId, Name, ModuleCode, Icon, Link, SortOrder, Status)
    VALUES (NEWID(), (SELECT Id FROM [cms].[Module] WHERE ModuleCode = 'SYSTEM'),
        N'Báo cáo định kỳ', 'SCHEDULED_REPORT', 'schedule_send', '/admin/scheduled-report', 7, 2);
    PRINT N'Đã thêm module SCHEDULED_REPORT.';
END ELSE PRINT N'Module SCHEDULED_REPORT đã tồn tại — bỏ qua.';

UPDATE [cms].[Module] SET Status = 2 WHERE ModuleCode = 'SCHEDULED_REPORT' AND Status <> 2;

INSERT INTO [cms].[Permission] (PublicId, UserId, ModuleId, Can_Access, Can_View, Can_Add, Can_Edit, Can_Delete)
SELECT NEWID(), u.Id, m.Id, 2, 2, 2, 2, 2
FROM [cms].[Module] m
CROSS JOIN (SELECT Id FROM [dbo].[Users] WHERE LoginName IN ('admin', 'admin_pdp') AND (IsDelete IS NULL OR IsDelete != 2)) u
WHERE m.ModuleCode = 'SCHEDULED_REPORT'
  AND NOT EXISTS (SELECT 1 FROM [cms].[Permission] x WHERE x.UserId = u.Id AND x.ModuleId = m.Id);

PRINT N'Đã cấp quyền SCHEDULED_REPORT cho admin/admin_pdp.';

-- Kiểm tra
SELECT m.Id, m.ModuleCode, m.Name, m.Link FROM [cms].[Module] m WHERE m.ModuleCode = 'SCHEDULED_REPORT';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO cms."Module" ("PublicId", "ParentId", "Name", "ModuleCode", "Icon", "Link", "SortOrder", "Status")
SELECT gen_random_uuid(), (SELECT "Id" FROM cms."Module" WHERE "ModuleCode" = 'SYSTEM'),
    'Báo cáo định kỳ', 'SCHEDULED_REPORT', 'schedule_send', '/admin/scheduled-report', 7, 2
WHERE NOT EXISTS (SELECT 1 FROM cms."Module" WHERE "ModuleCode" = 'SCHEDULED_REPORT');

UPDATE cms."Module" SET "Status" = 2 WHERE "ModuleCode" = 'SCHEDULED_REPORT' AND "Status" <> 2;

INSERT INTO cms."Permission" ("PublicId", "UserId", "ModuleId", "Can_Access", "Can_View", "Can_Add", "Can_Edit", "Can_Delete")
SELECT gen_random_uuid(), u."Id", m."Id", 2, 2, 2, 2, 2
FROM cms."Module" m
CROSS JOIN (SELECT "Id" FROM public."Users" WHERE "LoginName" IN ('admin', 'admin_pdp') AND ("IsDelete" IS NULL OR "IsDelete" != 2)) u
WHERE m."ModuleCode" = 'SCHEDULED_REPORT'
  AND NOT EXISTS (SELECT 1 FROM cms."Permission" x WHERE x."UserId" = u."Id" AND x."ModuleId" = m."Id");

-- Kiểm tra
SELECT "Id", "ModuleCode", "Name", "Link" FROM cms."Module" WHERE "ModuleCode" = 'SCHEDULED_REPORT';
*/
