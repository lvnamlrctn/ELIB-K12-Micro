using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Entities.Gamification;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

/// <summary>
/// Quét định kỳ các huy hiệu (Gamification.Badge) đang bật, tính tiêu chí của từng bạn đọc dựa trên hoạt
/// động đọc/mượn kể TỪ MỐC BẬT TÍNH NĂNG (Gamification.GAMIFICATION_ENABLED_AT — không tính hồi tố lịch
/// sử cũ trước đó, theo đúng quyết định khi lập kế hoạch), cấp huy hiệu (ReaderBadge) cho bạn đọc đủ điều
/// kiện và chưa có, gửi email/SMS/Zalo nếu có cấu hình (rỗng = không gửi).
///
/// Khác ELIB-LRC (đơn-tenant, 1 cờ GAMIFICATION_ENABLED/GAMIFICATION_ENABLED_AT dùng chung): ELIB đa
/// tenant — danh mục Badge tenant-scoped (mỗi tenant tự định nghĩa bộ huy hiệu riêng), nên job gộp theo
/// TenantId của từng huy hiệu, tra cờ bật/tắt + mốc kích hoạt RIÊNG cho từng tenant qua overload
/// ISystemParameterService.*Async(code, tenantId) — KHÔNG dùng overload không tham số (đọc HttpContext,
/// không chạy được trong Hangfire job). Khi tính tiêu chí, lọc EbookLog/BookOut theo TenantId của bạn đọc
/// (join Readers) chứ không tin tưởng tuyệt đối EbookLog.TenantId — đúng tenant của huy hiệu.
/// </summary>
public class BadgeEvaluationJob(
    ELIBAPIDbContext db,
    IEmailService emailService,
    ISystemParameterService sysParam,
    INotificationDispatcher notificationDispatcher,
    ILogger<BadgeEvaluationJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        var badges = await db.Badges
            .Where(b => b.IsDelete != 2 && b.Status == 2 && b.CriteriaType != null && b.Threshold.HasValue)
            .ToListAsync();
        if (badges.Count == 0)
        {
            logger.LogInformation("BadgeEvaluationJob: chưa có huy hiệu nào đang bật.");
            return;
        }

        var existingSet = (await db.ReaderBadges
                .Where(rb => rb.IsDelete != 2)
                .Select(rb => new { rb.ReaderId, rb.BadgeId })
                .ToListAsync())
            .Select(x => (x.ReaderId, x.BadgeId))
            .ToHashSet();

        var newlyEarned = new List<ReaderBadge>();

        // Gộp theo TenantId — mỗi tenant có cờ GAMIFICATION_ENABLED/GAMIFICATION_ENABLED_AT riêng, và
        // danh mục Badge tenant-scoped nên mỗi tenant chỉ đối chiếu hoạt động của bạn đọc thuộc tenant đó.
        foreach (var tenantGroup in badges.GroupBy(b => b.TenantId))
        {
            var tenantId = tenantGroup.Key;

            if (!await sysParam.IsEnabledAsync("GAMIFICATION_ENABLED", tenantId))
            {
                logger.LogInformation("BadgeEvaluationJob: GAMIFICATION_ENABLED đang tắt cho TenantId={TenantId}, bỏ qua.", tenantId);
                continue;
            }

            var cutoff = await GetOrSetCutoffAsync(tenantId);

            // Gộp theo CriteriaType để mỗi loại chỉ quét dữ liệu nguồn 1 lần trong phạm vi tenant này,
            // dùng chung cho mọi huy hiệu cùng loại (có thể có nhiều huy hiệu "TotalDigitalReads" với
            // Threshold khác nhau).
            var valuesByType = new Dictionary<string, Dictionary<long, long>>();

            foreach (var badge in tenantGroup)
            {
                var values = await GetCriteriaValuesAsync(badge.CriteriaType!, cutoff, tenantId, valuesByType);
                foreach (var (readerId, value) in values)
                {
                    if (value < badge.Threshold!.Value) continue;
                    if (!existingSet.Add((readerId, badge.Id))) continue; // đã có huy hiệu này hoặc vừa thêm trong lượt quét này

                    newlyEarned.Add(new ReaderBadge
                    {
                        ReaderId       = readerId,
                        BadgeId        = badge.Id,
                        EarnedAt       = DateTime.Now,
                        IsDelete       = 1,
                        CreatedRowDate = DateTime.Now,
                        TenantId       = tenantId,
                        PublicId       = Guid.NewGuid()
                    });
                }
            }
        }

        if (newlyEarned.Count == 0)
        {
            logger.LogInformation("BadgeEvaluationJob: không có huy hiệu mới nào được cấp.");
            return;
        }

        db.ReaderBadges.AddRange(newlyEarned);
        await db.SaveChangesAsync();
        logger.LogInformation("BadgeEvaluationJob: đã cấp {Count} huy hiệu mới", newlyEarned.Count);

        await SendNotificationsAsync(newlyEarned, badges);
    }

    /// <summary>
    /// Đọc mốc GAMIFICATION_ENABLED_AT của 1 tenant; nếu chưa có (lần chạy đầu sau khi tenant đó bật
    /// GAMIFICATION_ENABLED) thì tự ghi mốc = thời điểm hiện tại — đúng nghĩa "chỉ tính hoạt động từ lúc
    /// bật", không cần màn hình riêng để admin nhập tay mốc này.
    /// </summary>
    private async Task<DateTime> GetOrSetCutoffAsync(long? tenantId)
    {
        var raw = await sysParam.GetValueAsync("GAMIFICATION_ENABLED_AT", tenantId);
        var cleaned = raw == null ? null : System.Text.RegularExpressions.Regex.Replace(raw, "<.*?>", "").Trim();
        if (DateTime.TryParse(cleaned, out var cutoff)) return cutoff;

        var now = DateTime.Now;
        var row = await db.SystemParameters.FirstOrDefaultAsync(x => x.Code == "GAMIFICATION_ENABLED_AT" && x.TenantId == tenantId && x.IsDelete != 2);
        if (row == null)
        {
            db.SystemParameters.Add(new SystemParameter
            {
                Code           = "GAMIFICATION_ENABLED_AT",
                Value          = now.ToString("o"),
                DescriptionVn  = "Mốc thời gian bật tính năng Huy hiệu đọc cho tenant này (tự ghi, không cần sửa tay) — huy hiệu chỉ tính hoạt động từ mốc này, không tính hồi tố",
                TenantId       = tenantId,
                IsDelete       = 1,
                CreatedRowDate = now,
                PublicId       = Guid.NewGuid()
            });
        }
        else
        {
            row.Value = now.ToString("o");
            row.UpdatedRowDate = now;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("BadgeEvaluationJob: ghi mốc GAMIFICATION_ENABLED_AT = {Cutoff} cho TenantId={TenantId}", now, tenantId);
        return now;
    }

    private async Task<Dictionary<long, long>> GetCriteriaValuesAsync(string criteriaType, DateTime cutoff, long? tenantId, Dictionary<string, Dictionary<long, long>> cache)
    {
        if (cache.TryGetValue(criteriaType, out var cached)) return cached;

        Dictionary<long, long> values = criteriaType switch
        {
            "TotalDigitalReads" => await (
                from x in db.EbookLogs
                join r in db.Readers on x.ReaderId equals r.Id
                where x.Type == 1 && x.IsDelete != 2 && x.Submited != null && x.Submited >= cutoff
                      && x.ReaderId.HasValue && r.TenantId == tenantId
                group x by x.ReaderId!.Value into g
                select new { ReaderId = g.Key, Count = (long)g.Count() }
            ).ToDictionaryAsync(x => x.ReaderId, x => x.Count),

            "DistinctDigitalTitles" => await (
                from x in db.EbookLogs
                join r in db.Readers on x.ReaderId equals r.Id
                where x.Type == 1 && x.IsDelete != 2 && x.Submited != null && x.Submited >= cutoff
                      && x.ReaderId.HasValue && r.TenantId == tenantId
                group x by x.ReaderId!.Value into g
                select new { ReaderId = g.Key, Count = (long)g.Select(y => y.Bookid).Distinct().Count() }
            ).ToDictionaryAsync(x => x.ReaderId, x => x.Count),

            "TotalPagesRead" => await (
                from x in db.EbookLogs
                join r in db.Readers on x.ReaderId equals r.Id
                where x.Type == 1 && x.IsDelete != 2 && x.Submited != null && x.Submited >= cutoff
                      && x.ReaderId.HasValue && r.TenantId == tenantId
                group x by x.ReaderId!.Value into g
                select new { ReaderId = g.Key, Count = (long)g.Sum(y => y.Page ?? 0) }
            ).ToDictionaryAsync(x => x.ReaderId, x => x.Count),

            "TotalPrintBorrows" => await (
                from x in db.BookOuts
                join r in db.Readers on x.ReaderId equals r.Id
                where x.IsDelete != 2 && x.ReaderId.HasValue && x.BorrowDate != null && x.BorrowDate >= cutoff
                      && r.TenantId == tenantId
                group x by x.ReaderId!.Value into g
                select new { ReaderId = g.Key, Count = (long)g.Count() }
            ).ToDictionaryAsync(x => x.ReaderId, x => x.Count),

            _ => new Dictionary<long, long>()
        };

        cache[criteriaType] = values;
        return values;
    }

    private async Task SendNotificationsAsync(List<ReaderBadge> newlyEarned, List<Badge> badges)
    {
        var badgeMap = badges.ToDictionary(b => b.Id, b => b);
        var readerIds = newlyEarned.Select(x => x.ReaderId).Distinct().ToList();
        var readers = await db.Readers.Where(x => readerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x);

        // Gộp theo TenantId của huy hiệu để tra mẫu email/SMS/Zalo đúng tenant (giống DueSoonReminderJob).
        foreach (var group in newlyEarned.GroupBy(rb => badgeMap.TryGetValue(rb.BadgeId, out var b) ? b.TenantId : null))
        {
            var tenantId = group.Key;
            var template = await sysParam.GetValueAsync("EMAIL_BADGE_EARNED", tenantId);

            foreach (var rb in group)
            {
                if (!readers.TryGetValue(rb.ReaderId, out var reader)) continue;
                if (!badgeMap.TryGetValue(rb.BadgeId, out var badge)) continue;

                var tokens = new Dictionary<string, string>
                {
                    ["ReaderName"] = $"{reader.FirstName} {reader.LastName}".Trim(),
                    ["BadgeName"]  = badge.Name ?? "",
                    ["EarnedDate"] = rb.EarnedAt.ToString("dd/MM/yyyy HH:mm"),
                };

                if (!string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(reader.Email))
                {
                    var body = EmailTemplateHelper.Render(template, tokens);
                    try
                    {
                        await emailService.SendAsync(reader.Email!, "Bạn vừa đạt được huy hiệu mới", body);
                        rb.NotifiedAt = DateTime.Now;
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "BadgeEvaluationJob: gửi email tới {Email} thất bại", reader.Email); }
                }

                // await thật (không fire-and-forget) — NotificationDispatcher dùng chung DbContext theo
                // scope của job này, fire-and-forget sẽ khiến DbContext bị dispose giữa chừng khi job kết thúc.
                await notificationDispatcher.DispatchSmsAsync(tenantId, reader.Phone, "BADGE_EARNED", tokens);
                await notificationDispatcher.DispatchZaloAsync(tenantId, reader.Phone, "BADGE_EARNED", tokens);
            }
        }
        await db.SaveChangesAsync();
    }
}
