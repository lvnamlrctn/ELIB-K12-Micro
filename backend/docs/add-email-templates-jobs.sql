/* =============================================================================
   Seed 3 dòng dbo.SystemParameter làm mẫu email cho các job nền mới:
   - BookRequestExpiryJob  → EMAIL_PRINT_HOLD_EXPIRED
   - EbookLoanExpiryJob    → EMAIL_EBOOK_EXPIRED, EMAIL_EBOOK_RESERVATION_READY

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

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'EMAIL_PRINT_HOLD_EXPIRED' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, TenantId)
    VALUES (NEWID(), 'EMAIL_PRINT_HOLD_EXPIRED', N'<p>Chào {ReaderName},</p><p>Yêu cầu giữ chỗ tài liệu <b>{BibTitle}</b> của bạn đã quá hạn lấy ({ExpiredDate}) và đã bị huỷ. Vui lòng đặt giữ chỗ lại nếu vẫn cần mượn.</p>', NULL);
    PRINT N'Đã thêm mẫu email EMAIL_PRINT_HOLD_EXPIRED.';
END ELSE PRINT N'Mẫu email EMAIL_PRINT_HOLD_EXPIRED đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'EMAIL_EBOOK_EXPIRED' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, TenantId)
    VALUES (NEWID(), 'EMAIL_EBOOK_EXPIRED', N'<p>Chào {ReaderName},</p><p>Quyền truy cập tài liệu số <b>{EbookTitle}</b> của bạn đã hết hạn lúc {ExpiredDate}. Vui lòng mượn lại nếu vẫn cần đọc.</p>', NULL);
    PRINT N'Đã thêm mẫu email EMAIL_EBOOK_EXPIRED.';
END ELSE PRINT N'Mẫu email EMAIL_EBOOK_EXPIRED đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'EMAIL_EBOOK_RESERVATION_READY' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, TenantId)
    VALUES (NEWID(), 'EMAIL_EBOOK_RESERVATION_READY', N'<p>Chào {ReaderName},</p><p>Đã đến lượt bạn mượn tài liệu số <b>{EbookTitle}</b> đã đặt trước. Vui lòng vào hệ thống bấm Mượn trước {ReadyExpiresAt}, nếu không lượt mượn sẽ được chuyển cho người tiếp theo.</p>', NULL);
    PRINT N'Đã thêm mẫu email EMAIL_EBOOK_RESERVATION_READY.';
END ELSE PRINT N'Mẫu email EMAIL_EBOOK_RESERVATION_READY đã tồn tại — bỏ qua.';

-- Kiểm tra
SELECT Code, TenantId FROM [dbo].[SystemParameter] WHERE Code IN ('EMAIL_PRINT_HOLD_EXPIRED','EMAIL_EBOOK_EXPIRED','EMAIL_EBOOK_RESERVATION_READY');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "TenantId")
SELECT gen_random_uuid(), 'EMAIL_PRINT_HOLD_EXPIRED', '<p>Chào {ReaderName},</p><p>Yêu cầu giữ chỗ tài liệu <b>{BibTitle}</b> của bạn đã quá hạn lấy ({ExpiredDate}) và đã bị huỷ. Vui lòng đặt giữ chỗ lại nếu vẫn cần mượn.</p>', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'EMAIL_PRINT_HOLD_EXPIRED' AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "TenantId")
SELECT gen_random_uuid(), 'EMAIL_EBOOK_EXPIRED', '<p>Chào {ReaderName},</p><p>Quyền truy cập tài liệu số <b>{EbookTitle}</b> của bạn đã hết hạn lúc {ExpiredDate}. Vui lòng mượn lại nếu vẫn cần đọc.</p>', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'EMAIL_EBOOK_EXPIRED' AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "TenantId")
SELECT gen_random_uuid(), 'EMAIL_EBOOK_RESERVATION_READY', '<p>Chào {ReaderName},</p><p>Đã đến lượt bạn mượn tài liệu số <b>{EbookTitle}</b> đã đặt trước. Vui lòng vào hệ thống bấm Mượn trước {ReadyExpiresAt}, nếu không lượt mượn sẽ được chuyển cho người tiếp theo.</p>', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'EMAIL_EBOOK_RESERVATION_READY' AND "TenantId" IS NULL);

-- Kiểm tra
SELECT "Code", "TenantId" FROM public.systemparameter WHERE "Code" IN ('EMAIL_PRINT_HOLD_EXPIRED','EMAIL_EBOOK_EXPIRED','EMAIL_EBOOK_RESERVATION_READY');
*/
