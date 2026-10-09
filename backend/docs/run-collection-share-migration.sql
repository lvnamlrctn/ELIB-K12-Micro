/* =============================================================================
   MIGRATION: Thêm cột Share vào bảng [Ebook].[collection]
   Chạy 1 lần duy nhất. Kiểm tra cột chưa tồn tại trước khi thêm.
   ============================================================================= */

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[Ebook].[collection]')
      AND name = 'Share'
)
BEGIN
    ALTER TABLE [Ebook].[collection] ADD [Share] INT NULL;
    PRINT 'Đã thêm cột Share vào [Ebook].[collection].';
END
ELSE
BEGIN
    PRINT 'Cột Share đã tồn tại, bỏ qua.';
END
