using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/Ebook/EbookFavorite")]
public class PublicEbookFavoriteController : PublicBaseController
{
    private readonly IPublicEbookFavoriteRepository _repo;
    private readonly IMinioService _minio;

    public PublicEbookFavoriteController(IPublicEbookFavoriteRepository repo, IMinioService minio)
    {
        _repo = repo;
        _minio = minio;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicEbookFavoriteSearchRequest r)
    {
        var result = await _repo.SearchWithDetailsAsync(r);
        foreach (var item in result.Items) item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<PagedResult<PublicEbookFavoriteResponse>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicEbookFavoriteSearchRequest r)
    {
        var items = await _repo.SearchAllWithDetailsAsync(r);
        foreach (var item in items) item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<List<PublicEbookFavoriteResponse>>.Ok(items));
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add([FromBody] PublicEbookFavoriteRequest r)
    {
        var entity = await _repo.AddFavoriteAsync(r);
        return Ok(ApiResponse<PublicEbookFavoriteResponse>.Ok(ToResponse(entity)));
    }

    [HttpPost("Remove")]
    public async Task<IActionResult> Remove([FromBody] PublicEbookFavoriteRequest r)
    {
        var removed = await _repo.RemoveFavoriteAsync(r);
        if (!removed)
            return NotFound(ApiResponse<object>.Fail("Favorite not found"));
        return Ok(ApiResponse<object>.Ok(null!));
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";

    private static PublicEbookFavoriteResponse ToResponse(EbookFavorite x) => new()
    {
        Id             = x.Id,
        ReaderId       = x.ReaderId,
        CreatedRowDate = x.CreatedRowDate
    };
}
