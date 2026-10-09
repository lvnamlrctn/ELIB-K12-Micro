using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicPhotoAlbumController : PublicBaseController
{
    private readonly IPublicGenericRepository<PhotoAlbum, PublicPhotoAlbumSearchRequest> _repo;
    private readonly IMinioService _minio;

    public PublicPhotoAlbumController(
        IPublicGenericRepository<PhotoAlbum, PublicPhotoAlbumSearchRequest> repo,
        IMinioService minio)
    {
        _repo  = repo;
        _minio = minio;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicPhotoAlbumSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicPhotoAlbumResponse>>.Ok(new PagedResult<PublicPhotoAlbumResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicPhotoAlbumSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicPhotoAlbumResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private PublicPhotoAlbumResponse ToResponse(PhotoAlbum x) => new()
    {
        Id          = x.Id,
        PortalId    = x.PortalId,
        Name        = x.Name,
        Description = x.Description,
        Image       = ResolveImageUrl(x.Image),
        Code        = x.Code,
        Language    = x.Language,
        Status      = x.Status,
        SortOrder   = x.SortOrder,
        Types       = x.Types,
        Postions    = x.Postions,
        IsSpecial   = x.IsSpecial,
    };

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}
