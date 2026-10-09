/* =============================================================================
   Đổi cột PolicyCircDocGroup.DocGroupId từ INT (trỏ bảng DocGroup riêng, thừa — không dùng ở đâu khác
   trong hệ thống) sang BIGINT (trỏ BibType.Id — danh mục loại tài liệu chính thức, đã dùng sẵn ở
   bib-list/bib-type). Tab "Nhóm tài liệu" ở /admin/circ-places đổi nguồn dropdown từ DocGroupService sang
   BibTypeService tương ứng ở phía code.

   ĐÃ XÁC NHẬN trước khi viết script này: bảng PolicyCircDocGroup đang có 0 dòng trên CSDL Postgres đang
   chạy (kiểm tra trực tiếp qua psql) — an toàn đổi kiểu cột trực tiếp, không cần bước migrate/khớp dữ liệu
   cũ. Nếu chạy script này trên 1 CSDL KHÁC đã có dữ liệu thật ở bảng này, PHẢI kiểm tra lại số dòng trước
   — ALTER COLUMN sang BIGINT không tự map lại Id cũ (DocGroup.Id) sang Id mới (BibType.Id) vì đây là 2
   danh mục có bộ Id hoàn toàn độc lập.

   Không xoá bảng DocGroup (dữ liệu cũ có thể còn tham chiếu nơi khác chưa rà hết) — chỉ ngừng dùng ở
   riêng màn hình "Nhóm tài liệu" này.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF EXISTS (SELECT 1 FROM [dbo].[PolicyCircDocGroup])
BEGIN
    RAISERROR(N'PolicyCircDocGroup đang có dữ liệu — DỪNG LẠI, kiểm tra kỹ trước khi đổi kiểu cột (script này giả định bảng rỗng).', 16, 1);
END
ELSE
BEGIN
    ALTER TABLE [dbo].[PolicyCircDocGroup] ALTER COLUMN [DocGroupId] BIGINT NOT NULL;
    PRINT N'Đã đổi PolicyCircDocGroup.DocGroupId sang BIGINT.';
END

-- Kiểm tra
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PolicyCircDocGroup' AND COLUMN_NAME = 'DocGroupId';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM printbook."PolicyCircDocGroup") THEN
        RAISE EXCEPTION 'PolicyCircDocGroup đang có dữ liệu — DỪNG LẠI, kiểm tra kỹ trước khi đổi kiểu cột (script này giả định bảng rỗng).';
    ELSE
        ALTER TABLE printbook."PolicyCircDocGroup" ALTER COLUMN "DocGroupId" TYPE BIGINT;
        RAISE NOTICE 'Đã đổi PolicyCircDocGroup.DocGroupId sang BIGINT.';
    END IF;
END $$;

-- Kiểm tra
SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = 'printbook' AND table_name = 'PolicyCircDocGroup' AND column_name = 'DocGroupId';
*/
