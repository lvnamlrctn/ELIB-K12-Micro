using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicPhotoController : PublicBaseController
{
    private readonly IPublicGenericRepository<Photo, PublicPhotoSearchRequest> _repo;
    private readonly IMinioService _minio;

    public PublicPhotoController(
        IPublicGenericRepository<Photo, PublicPhotoSearchRequest> repo,
        IMinioService minio)
    {
        _repo  = repo;
        _minio = minio;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicPhotoSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicPhotoResponse>>.Ok(new PagedResult<PublicPhotoResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicPhotoSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicPhotoResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    private PublicPhotoResponse ToResponse(Photo x) => new()
    {
        Id           = x.Id,
        Name         = x.Name,
        Brief        = x.Brief,
        Image        = ResolveImageUrl(x.Image),
        Link         = x.Link,
        Postion      = x.Postion,
        PhotoAlbumId = x.PhotoAlbumId,
        Width        = x.Width,
        Height       = x.Height,
        Status       = x.Status,
        PortalId     = x.PortalId,
        SortOrder    = x.SortOrder,
        Language     = x.Language,
        Types        = x.Types,
    };

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}
