using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/ebook")]
public class PublicEbookSearchController : ControllerBase
{
    private readonly IElasticsearchService _elastic;
    private readonly ELIBAPIDbContext _db;
    private readonly IMinioService _minio;

    public PublicEbookSearchController(IElasticsearchService elastic, ELIBAPIDbContext db, IMinioService minio)
    {
        _elastic = elastic;
        _db = db;
        _minio = minio;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q            = null,
        [FromQuery] int     page         = 1,
        [FromQuery] int     pageSize     = 10,
        [FromQuery] string? collectionId = null,
        [FromQuery] string? topicId      = null,
        [FromQuery] string? subjectId    = null,
        [FromQuery] string? language     = null,
        [FromQuery] bool?   free         = null,
        [FromQuery] int?    fromYear     = null,
        [FromQuery] int?    toYear       = null,
        [FromQuery] long?   tenantId     = null)
    {
        var request = new EbookChunkSearchRequest
        {
            Q            = q,
            Page         = Math.Max(1, page),
            PageSize     = Math.Clamp(pageSize, 1, 50),
            CollectionId = collectionId,
            TopicId      = topicId,
            SubjectId    = subjectId,
            Language     = language,
            Free         = free,
            FromYear     = fromYear,
            ToYear       = toYear,
            TenantId     = tenantId,
        };

        var result = await _elastic.SearchChunksAsync(request);
        return Ok(ApiResponse<EbookSearchResponse>.Ok(result));
    }

    [HttpPost("SearchElastic")]
    public async Task<IActionResult> SearchElastic([FromBody] PublicEbookElasticSearchRequest request)
    {
        request.Page     = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 50);

        if (request.TenantId.HasValue && request.TenantId != Guid.Empty)
            request.ResolvedTenantId = await _db.Tenants
                .Where(t => t.PublicId == request.TenantId && t.IsDelete != 2)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();

        var result = await _elastic.SearchEbooksAsync(request);
        foreach (var item in result.Items)
            item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<PublicEbookElasticResponse>.Ok(result));
    }

    [HttpGet("search/suggest")]
    public async Task<IActionResult> Suggest(
        [FromQuery] string? q        = null,
        [FromQuery] long?   tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<List<string>>.Ok([]));

        var suggestions = await _elastic.SuggestAsync(q, tenantId);
        return Ok(ApiResponse<List<string>>.Ok(suggestions));
    }

    [HttpGet("search/{ebookId}")]
    public async Task<IActionResult> SearchInBook(
        string ebookId,
        [FromQuery] string? q = null)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<EbookSearchResponse>.Ok(new EbookSearchResponse()));

        var result = await _elastic.SearchInBookAsync(ebookId, q);
        return Ok(ApiResponse<EbookSearchResponse>.Ok(result));
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}
