/* =============================================================================
   Seed 3 dòng dbo.SystemParameter làm mẫu email cho job nền nhắc hạn mượn mới:
   - DueSoonReminderJob → EMAIL_PRINT_DUE_SOON, EMAIL_PRINT_OVERDUE, EMAIL_EBOOK_DUE_SOON

   TenantId = NULL (mẫu dùng chung mọi tenant — SystemParameterService tự fallback về dòng này khi
   tenant hiện tại chưa có mẫu riêng). Không seed thì job vẫn chạy đúng, chỉ bỏ qua bước gửi email
   (đã có check IsNullOrWhiteSpace) — script này không bắt buộc để tính năng "chạy được", chỉ cần để
   tính năng "gửi được thông báo".

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'EMAIL_PRINT_DUE_SOON' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, TenantId)
    VALUES (NEWID(), 'EMAIL_PRINT_DUE_SOON', N'<p>Chào {ReaderName},</p><p>Tài liệu <b>{BibTitle}</b> của bạn sắp đến hạn trả vào {DueDate}. Vui lòng trả đúng hạn hoặc gia hạn nếu vẫn cần sử dụng.</p>', NULL);
    PRINT N'Đã thêm mẫu email EMAIL_PRINT_DUE_SOON.';
END ELSE PRINT N'Mẫu email EMAIL_PRINT_DUE_SOON đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'EMAIL_PRINT_OVERDUE' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, TenantId)
    VALUES (NEWID(), 'EMAIL_PRINT_OVERDUE', N'<p>Chào {ReaderName},</p><p>Tài liệu <b>{BibTitle}</b> của bạn đã quá hạn trả từ {ExpiredDate}. Vui lòng trả tài liệu sớm để tránh phát sinh phí phạt.</p>', NULL);
    PRINT N'Đã thêm mẫu email EMAIL_PRINT_OVERDUE.';
END ELSE PRINT N'Mẫu email EMAIL_PRINT_OVERDUE đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'EMAIL_EBOOK_DUE_SOON' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, TenantId)
    VALUES (NEWID(), 'EMAIL_EBOOK_DUE_SOON', N'<p>Chào {ReaderName},</p><p>Quyền truy cập tài liệu số <b>{EbookTitle}</b> của bạn sắp hết hạn vào {DueDate}. Vui lòng gia hạn nếu vẫn cần đọc.</p>', NULL);
    PRINT N'Đã thêm mẫu email EMAIL_EBOOK_DUE_SOON.';
END ELSE PRINT N'Mẫu email EMAIL_EBOOK_DUE_SOON đã tồn tại — bỏ qua.';

-- Kiểm tra
SELECT Code, TenantId FROM [dbo].[SystemParameter] WHERE Code IN ('EMAIL_PRINT_DUE_SOON','EMAIL_PRINT_OVERDUE','EMAIL_EBOOK_DUE_SOON');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "TenantId")
SELECT gen_random_uuid(), 'EMAIL_PRINT_DUE_SOON', '<p>Chào {ReaderName},</p><p>Tài liệu <b>{BibTitle}</b> của bạn sắp đến hạn trả vào {DueDate}. Vui lòng trả đúng hạn hoặc gia hạn nếu vẫn cần sử dụng.</p>', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'EMAIL_PRINT_DUE_SOON' AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "TenantId")
SELECT gen_random_uuid(), 'EMAIL_PRINT_OVERDUE', '<p>Chào {ReaderName},</p><p>Tài liệu <b>{BibTitle}</b> của bạn đã quá hạn trả từ {ExpiredDate}. Vui lòng trả tài liệu sớm để tránh phát sinh phí phạt.</p>', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'EMAIL_PRINT_OVERDUE' AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "TenantId")
SELECT gen_random_uuid(), 'EMAIL_EBOOK_DUE_SOON', '<p>Chào {ReaderName},</p><p>Quyền truy cập tài liệu số <b>{EbookTitle}</b> của bạn sắp hết hạn vào {DueDate}. Vui lòng gia hạn nếu vẫn cần đọc.</p>', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'EMAIL_EBOOK_DUE_SOON' AND "TenantId" IS NULL);

-- Kiểm tra
SELECT "Code", "TenantId" FROM public.systemparameter WHERE "Code" IN ('EMAIL_PRINT_DUE_SOON','EMAIL_PRINT_OVERDUE','EMAIL_EBOOK_DUE_SOON');
*/
