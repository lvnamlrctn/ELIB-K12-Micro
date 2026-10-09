using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Bọc IElasticsearchService.SearchUnifiedAsync bằng circuit breaker + timeout race — khi ES lỗi
/// hoặc chậm quá TimeoutMs, tự động chuyển sang tìm kiếm DB (kém hơn: không xếp hạng liên quan/facet/
/// highlight, chỉ quét Title/Author/Publisher/Keyword bằng LIKE, giới hạn 500 bản ghi gần nhất). Đợt 9,
/// port từ ELIB-LRC OpacSearchFailoverService — thêm lọc TenantId ở nhánh fallback (ELIB đa tenant).
/// Cấu hình: ElasticsearchSettings:OpacFailover:{Enabled,TimeoutMs,FailureThreshold,OpenSeconds}.</summary>
public class OpacSearchFailoverService(
    IElasticsearchService elastic,
    ELIBAPIDbContext db,
    SearchCircuitBreaker breaker,
    IConfiguration config)
{
    private const int MaxScanRows = 500;

    private bool Enabled => config.GetValue("ElasticsearchSettings:OpacFailover:Enabled", true);
    private int TimeoutMs => config.GetValue("ElasticsearchSettings:OpacFailover:TimeoutMs", 4000);
    private int FailureThreshold => config.GetValue("ElasticsearchSettings:OpacFailover:FailureThreshold", 3);
    private TimeSpan OpenDuration => TimeSpan.FromSeconds(config.GetValue("ElasticsearchSettings:OpacFailover:OpenSeconds", 30));

    public async Task<UnifiedSearchResponse> SearchAsync(UnifiedSearchRequest request)
    {
        if (!Enabled || !breaker.ShouldTryElastic(FailureThreshold, OpenDuration))
        {
            var fb = await DbFallbackAsync(request);
            fb.UsedFallback = true;
            return fb;
        }

        var esTask = elastic.SearchUnifiedAsync(request);
        var winner = await Task.WhenAny(esTask, Task.Delay(TimeoutMs));

        if (winner == esTask)
        {
            try
            {
                var result = await esTask;
                breaker.RecordSuccess();
                result.UsedFallback = false;
                return result;
            }
            catch
            {
                breaker.RecordFailure(FailureThreshold, OpenDuration);
            }
        }
        else
        {
            // ES chậm hơn TimeoutMs — không có CancellationToken xuyên suốt IElasticsearchService nên
            // không huỷ được request đang bay, chỉ quan sát để tránh unobserved-task-exception crash.
            breaker.RecordFailure(FailureThreshold, OpenDuration);
            _ = esTask.ContinueWith(t => { if (t.IsFaulted) _ = t.Exception; }, TaskScheduler.Default);
        }

        var fallback = await DbFallbackAsync(request);
        fallback.UsedFallback = true;
        return fallback;
    }

    private async Task<UnifiedSearchResponse> DbFallbackAsync(UnifiedSearchRequest request)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var q        = (request.Q ?? request.MetaQ ?? "").Trim();
        var tenantId = request.ResolvedTenantId;

        var items = new List<UnifiedSearchItem>();
        long total = 0;

        if (request.DocType != "digital")
        {
            var (printItems, printTotal) = await PrintFallbackAsync(q, tenantId, request);
            items.AddRange(printItems);
            total += printTotal;
        }
        if (request.DocType != "print")
        {
            var (digitalItems, digitalTotal) = await DigitalFallbackAsync(q, tenantId, request);
            items.AddRange(digitalItems);
            total += digitalTotal;
        }

        items = OrderItems(items, request.SortBy).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToList();

        return new UnifiedSearchResponse
        {
            Total = total, Page = page, PageSize = pageSize,
            Items = items, Facets = new UnifiedSearchFacets(), UsedFallback = true
        };
    }

    private async Task<(List<UnifiedSearchItem> Items, long Total)> PrintFallbackAsync(string q, long? tenantId, UnifiedSearchRequest request)
    {
        var query =
            from b in db.Bibs
            where b.IsDelete != 2 && b.Status == "f" && (!tenantId.HasValue || b.TenantId == tenantId)
            join x in db.BibXmls on b.Bibid equals x.BibId into xj
            from x in xj.DefaultIfEmpty()
            select new { b, x };

        // Trước đây tầng dự phòng bỏ qua hẳn bộ lọc bộ sưu tập — nay lọc như ES (gồm cả bộ sưu tập con).
        var collIds = CollectionFilterIds(request);
        if (collIds != null) query = query.Where(r => r.b.CollectionId.HasValue && collIds.Contains(r.b.CollectionId.Value));

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r =>
                (r.x.Title != null && r.x.Title.Contains(q)) ||
                (r.x.Author != null && r.x.Author.Contains(q)) ||
                (r.x.Publisher != null && r.x.Publisher.Contains(q)) ||
                (r.x.Keyword != null && r.x.Keyword.Contains(q)) ||
                (r.x.PublishDate != null && r.x.PublishDate.Contains(q)));

        var rows = await query.OrderByDescending(r => r.b.Bibid).Take(MaxScanRows).ToListAsync();
        var total = rows.Count;

        var items = rows.Select(r => new UnifiedSearchItem
        {
            GroupId     = $"print:{r.b.Bibid}",
            DocType     = "print",
            PublicId    = r.b.PublicId.ToString(),
            Title       = r.x?.Title,
            Author      = r.x?.Author,
            Publisher   = r.x?.Publisher,
            PublishDate = r.x?.PublishDate,
            PublishYear = ParseYear(r.x?.PublishDate),
            Ddc         = r.x?.DDC,
            Keyword     = r.x?.Keyword,
            Images      = r.b.Images,
            BibId       = r.b.Bibid,
            Mfn         = r.b.Mfn
        }).ToList();

        return (items, total);
    }

    private async Task<(List<UnifiedSearchItem> Items, long Total)> DigitalFallbackAsync(string q, long? tenantId, UnifiedSearchRequest request)
    {
        var query =
            from i in db.EbookItems
            where i.IsDelete != 2 && i.Status == 2 && (!tenantId.HasValue || i.TenantId == tenantId)
            join x in db.EbookItemXmls on i.Id equals x.Id into xj
            from x in xj.DefaultIfEmpty()
            select new { i, x };

        var collIds = CollectionFilterIds(request);
        if (collIds != null) query = query.Where(r => r.i.CollectionId.HasValue && collIds.Contains(r.i.CollectionId.Value));

        if (!string.IsNullOrWhiteSpace(request.Language)) query = query.Where(r => r.i.Language == request.Language);
        if (request.Free.HasValue) query = query.Where(r => (r.i.Free == 1) == request.Free.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r =>
                (r.x.Title != null && r.x.Title.Contains(q)) ||
                (r.x.Author != null && r.x.Author.Contains(q)) ||
                (r.x.Publisher != null && r.x.Publisher.Contains(q)) ||
                (r.x.Keyword != null && r.x.Keyword.Contains(q)) ||
                (r.x.PublishDate != null && r.x.PublishDate.Contains(q)));

        var rows = await query.OrderByDescending(r => r.i.Id).Take(MaxScanRows).ToListAsync();
        var total = rows.Count;

        var items = rows.Select(r => new UnifiedSearchItem
        {
            GroupId     = $"digital:{r.i.Id}",
            DocType     = "digital",
            PublicId    = r.i.PublicId.ToString(),
            Title       = r.x?.Title,
            Author      = r.x?.Author,
            Publisher   = r.x?.Publisher,
            PublishDate = r.x?.PublishDate,
            PublishYear = ParseYear(r.x?.PublishDate),
            Keyword     = r.x?.Keyword,
            Language    = r.i.Language,
            Images      = r.i.Images,
            EbookId     = r.i.Id,
            Free        = r.i.Free == 1
        }).ToList();

        return (items, total);
    }

    // "newest"/"relevance" (không có BM25 ở DB) đều quy về mới nhất theo năm xuất bản — không giả vờ có
    // xếp hạng liên quan khi đang chạy chế độ dự phòng.
    private static IEnumerable<UnifiedSearchItem> OrderItems(List<UnifiedSearchItem> items, string? sortBy) => sortBy switch
    {
        "oldest" => items.OrderBy(i => i.PublishYear ?? 0),
        "title"  => items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase),
        _        => items.OrderByDescending(i => i.PublishYear ?? 0)
    };

    private static List<long>? CollectionFilterIds(UnifiedSearchRequest request)
    {
        if (request.ResolvedCollectionIds is { Count: > 0 }) return request.ResolvedCollectionIds;
        return long.TryParse(request.CollectionId, out var id) ? [id] : null;
    }

    private static int? ParseYear(string? publishDate)
    {
        if (string.IsNullOrWhiteSpace(publishDate)) return null;
        var digits = new string(publishDate.Where(char.IsDigit).ToArray());
        if (digits.Length >= 4 && int.TryParse(digits[..4], out var y)) return y;
        return null;
    }
}
