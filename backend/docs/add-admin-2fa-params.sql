/* =============================================================================
   Seed 2 dòng dbo.SystemParameter làm cờ bật/tắt bảo mật 2 lớp (CAPTCHA + OTP email) cho ĐĂNG NHẬP
   ADMIN — song song với 2 cờ tương tự đã có sẵn cho đăng nhập bạn đọc (READER_LOGIN_CAPTCHA_ENABLED,
   READER_LOGIN_OTP_ENABLED), đọc qua ISystemParameterService.IsEnabledAsync(code, tenantId):
   - ADMIN_LOGIN_CAPTCHA_ENABLED — Lớp bảo mật 1: yêu cầu CAPTCHA trước khi tra tài khoản (AuthController.
     GetCaptcha / AuthService.LoginAsync).
   - ADMIN_LOGIN_OTP_ENABLED     — Lớp bảo mật 2: yêu cầu mã OTP gửi qua email sau khi mật khẩu đúng
     (AuthController.VerifyOtp / AuthService.VerifyOtpAsync).

   TenantId = NULL (mặc định dùng chung mọi tenant — SystemParameterService tự fallback về dòng này khi
   tenant hiện tại chưa có mẫu riêng). Giá trị mặc định '0' (tắt) — không seed thì IsEnabledAsync coi như
   chưa bật, hành vi đăng nhập admin y hệt trước khi có tính năng này. Script idempotent, chạy lại an toàn.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'ADMIN_LOGIN_CAPTCHA_ENABLED' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
    VALUES (NEWID(), 'ADMIN_LOGIN_CAPTCHA_ENABLED', '0', N'Bật/tắt yêu cầu CAPTCHA khi đăng nhập ADMIN (lớp bảo mật thứ 1, trước khi tra tài khoản). "1" = bật, "0" = tắt.', NULL);
    PRINT N'Đã thêm tham số ADMIN_LOGIN_CAPTCHA_ENABLED.';
END ELSE PRINT N'Tham số ADMIN_LOGIN_CAPTCHA_ENABLED đã tồn tại — bỏ qua.';

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'ADMIN_LOGIN_OTP_ENABLED' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
    VALUES (NEWID(), 'ADMIN_LOGIN_OTP_ENABLED', '0', N'Bật/tắt yêu cầu mã OTP gửi qua email khi đăng nhập ADMIN (lớp bảo mật thứ 2, sau khi mật khẩu đúng). "1" = bật, "0" = tắt.', NULL);
    PRINT N'Đã thêm tham số ADMIN_LOGIN_OTP_ENABLED.';
END ELSE PRINT N'Tham số ADMIN_LOGIN_OTP_ENABLED đã tồn tại — bỏ qua.';

-- Kiểm tra
SELECT Code, Value, TenantId FROM [dbo].[SystemParameter] WHERE Code IN ('ADMIN_LOGIN_CAPTCHA_ENABLED','ADMIN_LOGIN_OTP_ENABLED');


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'ADMIN_LOGIN_CAPTCHA_ENABLED', '0', 'Bật/tắt yêu cầu CAPTCHA khi đăng nhập ADMIN (lớp bảo mật thứ 1, trước khi tra tài khoản). "1" = bật, "0" = tắt.', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'ADMIN_LOGIN_CAPTCHA_ENABLED' AND "TenantId" IS NULL);

INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'ADMIN_LOGIN_OTP_ENABLED', '0', 'Bật/tắt yêu cầu mã OTP gửi qua email khi đăng nhập ADMIN (lớp bảo mật thứ 2, sau khi mật khẩu đúng). "1" = bật, "0" = tắt.', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'ADMIN_LOGIN_OTP_ENABLED' AND "TenantId" IS NULL);

-- Kiểm tra
SELECT "Code", "Value", "TenantId" FROM public.systemparameter WHERE "Code" IN ('ADMIN_LOGIN_CAPTCHA_ENABLED','ADMIN_LOGIN_OTP_ENABLED');
*/
