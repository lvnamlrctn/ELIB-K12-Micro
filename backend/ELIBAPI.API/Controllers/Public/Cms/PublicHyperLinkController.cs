using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicHyperLinkController : PublicBaseController
{
    private readonly IPublicHyperLinkRepository _repo;
    private readonly IMinioService _minio;

    public PublicHyperLinkController(IPublicHyperLinkRepository repo, IMinioService minio)
    {
        _repo  = repo;
        _minio = minio;
    }

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicHyperLinkSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        foreach (var item in paged.Items)
            item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<PagedResult<PublicHyperLinkResponse>>.Ok(paged));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicHyperLinkSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        foreach (var item in items)
            item.Images = ResolveImageUrl(item.Images);
        return Ok(ApiResponse<List<PublicHyperLinkResponse>>.Ok(items));
    }

    private string? ResolveImageUrl(string? value) =>
        string.IsNullOrEmpty(value) || value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{_minio.PublicBaseUrl}/{value}";
}
