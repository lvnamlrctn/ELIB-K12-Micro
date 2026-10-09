using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Ghi nhận + tổng hợp thống kê chất lượng tìm kiếm OPAC (Đợt 9, port từ ELIB-LRC
/// SearchQualityService) — chỉ ghi lượt tìm trang 1 có từ khoá (bỏ tải nền không từ khoá/trang tiếp theo),
/// không lưu định danh bạn đọc, lọc TenantId khi đọc báo cáo (khác LRC đơn-tenant).</summary>
public class SearchQualityService(ELIBAPIDbContext db, ILogger<SearchQualityService> logger)
{
    public async Task RecordAsync(UnifiedSearchRequest request, long total, long elapsedMs, bool failed, bool usedFallback)
    {
        if (request.Page != 1) return;

        var text = string.Join(" ", new[]
            {
                request.Q, request.MetaQ, request.Title, request.Author, request.Publisher,
                request.Keyword, request.Summary, request.Isbn, request.Ddc, request.Content, request.CallNumber
            }.Where(s => !string.IsNullOrWhiteSpace(s)))
            .Trim().ToLowerInvariant();
        if (text.Length == 0) return;
        if (text.Length > 200) text = text[..200];

        try
        {
            db.SearchObservations.Add(new SearchObservation
            {
                OccurredAt = DateTime.UtcNow,
                Query = text,
                DocType = request.DocType ?? "all",
                Total = total,
                ElapsedMs = elapsedMs,
                Failed = failed,
                UsedFallback = usedFallback,
                TenantId = request.ResolvedTenantId
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ghi SearchObservation thất bại — không chặn kết quả tra cứu.");
        }
    }

    public async Task<object> ReportAsync(int days, long? tenantId)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.Date.AddDays(1 - days);

        var q = db.SearchObservations.Where(x => x.OccurredAt >= since);
        if (tenantId.HasValue) q = q.Where(x => x.TenantId == tenantId);

        var rows = await q.ToListAsync();

        var total = rows.Count;
        var failures = rows.Count(x => x.Failed);
        var empty = rows.Count(x => !x.Failed && x.Total == 0);
        var fallbackCount = rows.Count(x => x.UsedFallback);
        var averageMs = rows.Count > 0 ? rows.Average(x => x.ElapsedMs) : 0;
        var nonFailed = total - failures;
        var emptyRate = nonFailed > 0 ? 100.0 * empty / nonFailed : 0;
        var fallbackRate = total > 0 ? 100.0 * fallbackCount / total : 0;

        var topEmpty = rows.Where(x => !x.Failed && x.Total == 0)
            .GroupBy(x => x.Query).Select(g => new { query = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(30).ToList();

        var topQueries = rows.GroupBy(x => x.Query).Select(g => new { query = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(30).ToList();

        var daily = rows.GroupBy(x => x.OccurredAt.Date)
            .Select(g => new { date = g.Key, count = g.Count(), empty = g.Count(x => !x.Failed && x.Total == 0), failed = g.Count(x => x.Failed) })
            .OrderBy(x => x.date).ToList();

        return new
        {
            since, until = DateTime.UtcNow, total, failures, empty, averageMs, emptyRate, fallbackCount, fallbackRate,
            topEmpty, topQueries, daily
        };
    }

    public async Task<int> PurgeOldAsync(int retentionDays = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        return await db.SearchObservations.Where(x => x.OccurredAt < cutoff).ExecuteDeleteAsync();
    }
}
