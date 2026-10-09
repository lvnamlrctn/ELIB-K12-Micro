using System.Diagnostics;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Public;

/// <summary>
/// Tìm kiếm GỘP tài liệu in + tài liệu số trên index <c>library_docs</c>.
/// Công khai (không cần JWT) theo đúng quy ước Public API.
/// Đợt 9: bọc qua OpacSearchFailoverService (circuit-breaker ES→DB) + ghi SearchQualityService.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/search")]
public class PublicUnifiedSearchController(
    IElasticsearchService        elastic,
    ELIBAPIDbContext              db,
    IMinioService                 minio,
    OpacSearchFailoverService     failover,
    SearchQualityService          quality) : ControllerBase
{
    [HttpPost("unified")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELIBAPI.API.Infrastructure.RateLimitingExtensions.OpacSearchPolicy)]
    public async Task<IActionResult> Unified([FromBody] UnifiedSearchRequest request)
    {
        request.Page     = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 50);

        // Client truyền TenantId dạng GUID (PublicId); ES lưu Id số → phân giải trước khi truy vấn.
        if (request.TenantId.HasValue && request.TenantId != Guid.Empty)
            request.ResolvedTenantId = await db.Tenants
                .Where(t => t.PublicId == request.TenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();

        request.ResolvedCollectionIds = await ResolveCollectionSubtreeAsync(request.CollectionId);

        var sw = Stopwatch.StartNew();
        UnifiedSearchResponse result;
        try
        {
            result = await failover.SearchAsync(request);
        }
        catch (Exception)
        {
            sw.Stop();
            await quality.RecordAsync(request, 0, sw.ElapsedMilliseconds, failed: true, usedFallback: false);
            throw;
        }
        sw.Stop();

        foreach (var item in result.Items)
            item.Images = ResolveImageUrl(item.Images);

        await quality.RecordAsync(request, result.Total, sw.ElapsedMilliseconds, failed: false, usedFallback: result.UsedFallback);

        return Ok(ApiResponse<UnifiedSearchResponse>.Ok(result));
    }

    /// <summary>Đợt 22.2 — "Có thể bạn quan tâm" ở trang chi tiết: tài liệu gần giống về nhan đề/tác giả/
    /// từ khoá, cùng phạm vi tenant với trang chi tiết đang xem, loại chính tài liệu đó ra. Nhận PublicId
    /// (đã có sẵn ở cả 2 trang chi tiết) thay vì groupId nội bộ của ES (tiền tố "print_"/"digital_" + Id số
    /// — chi tiết lập chỉ mục, không nên lộ ra frontend) — tự tra Id số tương ứng rồi mới build groupId.</summary>
    [HttpGet("unified/similar")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELIBAPI.API.Infrastructure.RateLimitingExtensions.OpacSearchPolicy)]
    public async Task<IActionResult> Similar([FromQuery] string docType, [FromQuery] Guid publicId, [FromQuery] Guid? tenantId = null, [FromQuery] int size = 8)
    {
        string? groupId = docType switch
        {
            "print" => await db.Bibs.Where(x => x.PublicId == publicId && x.IsDelete != 2)
                .Select(x => (long?)x.Bibid).FirstOrDefaultAsync() is { } bibId && bibId > 0 ? $"print_{bibId}" : null,
            "digital" => await db.EbookItems.Where(x => x.PublicId == publicId && x.IsDelete != 2)
                .Select(x => (long?)x.Id).FirstOrDefaultAsync() is { } itemId && itemId > 0 ? $"digital_{itemId}" : null,
            _ => null,
        };
        if (groupId == null) return Ok(ApiResponse<List<UnifiedSearchItem>>.Ok([]));

        long? resolvedTenantId = null;
        if (tenantId.HasValue && tenantId != Guid.Empty)
            resolvedTenantId = await db.Tenants
                .Where(t => t.PublicId == tenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();

        var items = await elastic.MoreLikeThisAsync(groupId, resolvedTenantId, Math.Clamp(size, 1, 20));
        foreach (var item in items) item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<List<UnifiedSearchItem>>.Ok(items));
    }

    /// <summary>Gợi ý nhan đề khi gõ — dùng chung cho cả 2 loại tài liệu.</summary>
    [HttpGet("unified/suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string? q = null, [FromQuery] Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<List<string>>.Ok([]));

        long? resolved = null;
        if (tenantId.HasValue && tenantId != Guid.Empty)
            resolved = await db.Tenants
                .Where(t => t.PublicId == tenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();

        // Tận dụng chính đường tìm kiếm gộp, chỉ lấy nhan đề của vài kết quả đầu —
        // tránh phải nuôi thêm một truy vấn suggest riêng cho index mới. Cũng đi qua failover để gợi ý
        // vẫn hoạt động (kém hơn) khi ES đang lỗi/mở circuit breaker.
        var result = await failover.SearchAsync(new UnifiedSearchRequest
        {
            Q = q, Page = 1, PageSize = 8, ResolvedTenantId = resolved
        });

        var titles = result.Items
            .Select(i => i.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .Distinct()
            .ToList();
        return Ok(ApiResponse<List<string>>.Ok(titles));
    }

    /// <summary>Gợi ý giá trị theo tiền tố cho 4 ô lọc nâng cao (Nhan đề/Tác giả/Nhà xuất bản/Từ khoá)
    /// ở trang Tìm kiếm — khác endpoint "unified/suggest" ở trên (chỉ gợi ý nhan đề, dùng cho ô tìm
    /// nhanh) vì cần gợi ý theo đúng field người dùng đang gõ, không phải luôn là nhan đề.</summary>
    [HttpGet("unified/suggest-field")]
    public async Task<IActionResult> SuggestField(
        [FromQuery] string  field,
        [FromQuery] string? q        = null,
        [FromQuery] Guid?   tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<List<string>>.Ok([]));

        long? resolved = null;
        if (tenantId.HasValue && tenantId != Guid.Empty)
            resolved = await db.Tenants
                .Where(t => t.PublicId == tenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();

        var suggestions = await elastic.SuggestFieldAsync(field, q, resolved);
        return Ok(ApiResponse<List<string>>.Ok(suggestions));
    }

    /// <summary>Bộ sưu tập được chọn + toàn bộ con/cháu: tải 1 lần (Id, ParentId) rồi duyệt BFS trong bộ nhớ —
    /// không N+1 như <c>EbookCollectionRepository.CollectSubtreeIdsAsync</c> (chỉ dùng khi xoá). Id không hợp lệ → null.</summary>
    private async Task<List<long>?> ResolveCollectionSubtreeAsync(string? collectionId)
    {
        if (!long.TryParse(collectionId, out var rootId)) return null;
        var edges = await db.EbookCollections.AsNoTracking()
            .Where(c => c.IsDelete != 2 && c.ParentId != null)
            .Select(c => new { c.Id, ParentId = c.ParentId!.Value })
            .ToListAsync();
        var children = edges.ToLookup(e => e.ParentId, e => e.Id);
        var result = new List<long> { rootId };
        var seen   = new HashSet<long> { rootId };
        for (var i = 0; i < result.Count; i++)
            foreach (var child in children[result[i]])
                if (seen.Add(child)) result.Add(child);
        return result;
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{minio.PublicBaseUrl}/{value}";
}
