using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicNewsController : PublicBaseController
{
    private readonly IPublicNewsRepository _repo;
    private readonly IMinioService _minio;
    private readonly IAttachFileService _attachFiles;

    public PublicNewsController(IPublicNewsRepository repo, IMinioService minio, IAttachFileService attachFiles)
    {
        _repo        = repo;
        _minio       = minio;
        _attachFiles = attachFiles;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicNewsSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicNewsResponse>>.Ok(new PagedResult<PublicNewsResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicNewsSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicNewsResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    [HttpGet("GetLastedNews")]
    public async Task<IActionResult> GetLastedNews([FromQuery] PublicNewsLatestRequest r)
    {
        var items = await _repo.GetLatestNewsAsync(r);
        return Ok(ApiResponse<List<PublicNewsResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    [HttpGet("GetNewsById")]
    public async Task<IActionResult> GetNewsById([FromQuery] Guid newsId, [FromQuery] Guid? tenantId = null)
    {
        var items = await _repo.GetNewsByIdAsync(newsId, tenantId);
        return Ok(ApiResponse<List<PublicNewsResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    /// <summary>File đính kèm của 1 tin đã xuất bản (port ELIB-LRC 10-04) — chỉ tên/đuôi/dung lượng + link tải qua API, không lộ
    /// đường dẫn kho. <paramref name="tenantId"/> bị PublicHostTenantFilter ghi đè theo tên miền: chỉ tin của đơn vị đó/dùng chung.</summary>
    [HttpGet("Attachments")]
    public async Task<IActionResult> Attachments([FromQuery] Guid newsId, [FromQuery] Guid? tenantId = null)
    {
        var files = await _attachFiles.PublishedFilesAsync(newsId, tenantId);
        return Ok(ApiResponse<List<PublicAttachFileResponse>>.Ok(files.Select(f => new PublicAttachFileResponse
        {
            Id          = f.PublicId,
            Name        = f.Name,
            Ext         = FileExtension.Normalize(null, f.Name),
            SizeKb      = f.FileSize,
            DownloadUrl = Url.Content($"~/api/public/PublicNews/Attachment/{f.PublicId}"),
        }).ToList()));
    }

    [HttpGet("Attachment/{publicId:guid}")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELIBAPI.API.Infrastructure.RateLimitingExtensions.OpacSearchPolicy)]
    public async Task<IActionResult> Attachment(Guid publicId, [FromQuery] Guid? tenantId = null)
    {
        var result = await _attachFiles.OpenPublicAsync(publicId, tenantId);
        if (!result.IsOk) return NotFound(ApiResponse<object>.Fail(result.Error ?? "Không tìm thấy file", 404));
        return File(result.Value!.Content, result.Value.ContentType, result.Value.DownloadName);
    }

    private PublicNewsResponse ToResponse(News x) => new()
    {
        PublicId        = x.PublicId,
        Title           = x.Title,
        Brief           = x.Brief,
        Content         = x.Content,
        Images          = ResolveImageUrl(x.Images),
        Thumb           = ResolveImageUrl(x.Thumb),
        StartTime       = x.StartTime,
        EndTime         = x.EndTime,
        CategoryId      = x.CategoryId,
        PortalId        = x.PortalId,
        Language        = x.Language,
        Keyword         = x.Keyword,
        Author          = x.Author,
        Source          = x.Source,
        Types           = x.Types,
        Status          = x.Status,
        AllowComment    = x.AllowComment,
        TotalView       = x.TotalView,
        MetaTitle       = x.MetaTitle,
        MetaKeyword     = x.MetaKeyword,
        MetaDescription = x.MetaDescription,
    };

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}

public class PublicAttachFileResponse
{
    public Guid    Id          { get; set; }
    public string? Name        { get; set; }
    public string? Ext         { get; set; }
    public double? SizeKb      { get; set; }
    public string? DownloadUrl { get; set; }
}
