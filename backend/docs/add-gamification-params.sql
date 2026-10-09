/* =============================================================================
   Seed 1 dòng dbo.SystemParameter làm cờ bật/tắt tính năng Huy hiệu đọc (Gamification):
   - GAMIFICATION_ENABLED — "1" = bật, "0" = tắt. Đọc qua ISystemParameterService.IsEnabledAsync(code,
     tenantId) — mỗi tenant có thể tự thêm dòng riêng (TenantId = id của tenant đó) để bật/tắt độc lập,
     dòng TenantId = NULL bên dưới chỉ là fallback mặc định dùng chung khi tenant chưa có dòng riêng.

   KHÔNG seed GAMIFICATION_ENABLED_AT ở đây — BadgeEvaluationJob tự ghi mốc này (theo từng TenantId) vào
   lần chạy đầu tiên sau khi tenant đó bật GAMIFICATION_ENABLED, đúng nghĩa "chỉ tính hoạt động từ lúc
   bật, không tính hồi tố" (xem BadgeEvaluationJob.GetOrSetCutoffAsync).

   Giá trị mặc định '0' (tắt) — không bật gì cho tenant nào cho tới khi admin chủ động bật qua UI Tham số
   hệ thống. Script idempotent, chạy lại an toàn.

   PHẦN 1: SQL Server
   PHẦN 2: PostgreSQL
   Chạy phần tương ứng với database đang dùng.
   ============================================================================= */


/* =============================================================================
   PHẦN 1 — SQL SERVER
   ============================================================================= */

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemParameter] WHERE Code = 'GAMIFICATION_ENABLED' AND TenantId IS NULL)
BEGIN
    INSERT INTO [dbo].[SystemParameter] (PublicId, Code, Value, DescriptionVn, TenantId)
    VALUES (NEWID(), 'GAMIFICATION_ENABLED', '0', N'Bật/tắt tính năng Huy hiệu đọc (Gamification). "1" = bật, "0" = tắt. Mỗi tenant có thể thêm dòng riêng (TenantId khác NULL) để bật/tắt độc lập.', NULL);
    PRINT N'Đã thêm tham số GAMIFICATION_ENABLED.';
END ELSE PRINT N'Tham số GAMIFICATION_ENABLED đã tồn tại — bỏ qua.';

-- Kiểm tra
SELECT Code, Value, TenantId FROM [dbo].[SystemParameter] WHERE Code = 'GAMIFICATION_ENABLED';


/* =============================================================================
   PHẦN 2 — POSTGRESQL
   (Bỏ comment phần dưới và chạy trên PostgreSQL)
   ============================================================================= */

/*
INSERT INTO public.systemparameter ("PublicId", "Code", "Value", "DescriptionVn", "TenantId")
SELECT gen_random_uuid(), 'GAMIFICATION_ENABLED', '0', 'Bật/tắt tính năng Huy hiệu đọc (Gamification). "1" = bật, "0" = tắt. Mỗi tenant có thể thêm dòng riêng (TenantId khác NULL) để bật/tắt độc lập.', NULL
WHERE NOT EXISTS (SELECT 1 FROM public.systemparameter WHERE "Code" = 'GAMIFICATION_ENABLED' AND "TenantId" IS NULL);

-- Kiểm tra
SELECT "Code", "Value", "TenantId" FROM public.systemparameter WHERE "Code" = 'GAMIFICATION_ENABLED';
*/
