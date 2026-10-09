using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

/// <summary>
/// Banner carousel trang chủ app mobile (port ELIB-LRC 09-23) — tái dùng Photo có Types = "quang_cao" ("Ảnh quảng
/// cáo" ở trang admin Quản lý ảnh), không tạo bảng riêng; client không tự chọn loại ảnh khác được.
/// targetType/targetValue suy từ Photo.Link theo tiền tố "collection:&lt;id&gt;" / "search:&lt;từ khóa&gt;" (còn lại
/// là link thường); topic/gradient không có trường tương ứng nên luôn null.
/// Đa đơn vị: khác LRC, TenantId của request được giữ lại (lọc ảnh theo đơn vị) — app mobile gửi TenantId,
/// gọi qua host đơn vị thì PublicHostTenantFilter tự khóa theo host.
/// </summary>
[Route("api/public/[controller]")]
public class PublicHomeBannerController : PublicBaseController
{
    private const string BannerTypes = "quang_cao";

    private readonly IPublicGenericRepository<Photo, PublicPhotoSearchRequest> _repo;
    private readonly IMinioService _minio;

    public PublicHomeBannerController(
        IPublicGenericRepository<Photo, PublicPhotoSearchRequest> repo,
        IMinioService minio)
    {
        _repo  = repo;
        _minio = minio;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicSearchRequest r)
    {
        var paged = await _repo.SearchAsync(ToPhotoRequest(r));
        return Ok(ApiResponse<PagedResult<PublicHomeBannerResponse>>.Ok(new PagedResult<PublicHomeBannerResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(ToPhotoRequest(r));
        return Ok(ApiResponse<List<PublicHomeBannerResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private static PublicPhotoSearchRequest ToPhotoRequest(PublicSearchRequest r) => new()
    {
        Keyword   = r.Keyword,
        PortalId  = r.PortalId,
        Language  = r.Language,
        TenantId  = r.TenantId,
        PageIndex = r.PageIndex,
        PageSize  = r.PageSize,
        Types     = BannerTypes
    };

    private PublicHomeBannerResponse ToResponse(Photo x)
    {
        var (targetType, targetValue) = ResolveTarget(x.Link);
        return new PublicHomeBannerResponse
        {
            Topic       = null,
            Title       = x.Name,
            Desc        = x.Brief,
            Image       = ResolveImageUrl(x.Image),
            Gradient    = null,
            TargetType  = targetType,
            TargetValue = targetValue,
            SortOrder   = x.SortOrder
        };
    }

    private static (string? targetType, string? targetValue) ResolveTarget(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return ("link", null);
        if (link.StartsWith("collection:", StringComparison.OrdinalIgnoreCase))
            return ("collection", link["collection:".Length..].Trim());
        if (link.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            return ("search", link["search:".Length..].Trim());
        return ("link", link);
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}
