using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/Media")]
[Authorize]
public class MediaController(IMinioService minio) : BaseApiController
{
    [HttpGet("List")]
    [Permission("NEWS_MANAGE", "view")]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 24)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, totalCount) = await minio.ListPublicImagesAsync(keyword, page, pageSize);
        return Ok(ApiResponse<object>.Ok(new { items, totalCount }));
    }
}
