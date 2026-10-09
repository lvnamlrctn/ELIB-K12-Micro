using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicBannerController : PublicBaseController
{
    private readonly IPublicBannerRepository _repo;
    private readonly IMinioService _minio;

    public PublicBannerController(IPublicBannerRepository repo, IMinioService minio)
    {
        _repo  = repo;
        _minio = minio;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicBannerSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        foreach (var item in paged.Items)
            item.Url = ResolveImageUrl(item.Url);
        return Ok(ApiResponse<PagedResult<PublicBannerResponse>>.Ok(paged));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicBannerSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        foreach (var item in items)
            item.Url = ResolveImageUrl(item.Url);
        return Ok(ApiResponse<List<PublicBannerResponse>>.Ok(items));
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}
