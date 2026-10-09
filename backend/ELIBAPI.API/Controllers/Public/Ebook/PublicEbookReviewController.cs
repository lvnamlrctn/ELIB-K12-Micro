using ELIBAPI.API.DTOs;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/Ebook/EbookReview")]
public class PublicEbookReviewController : PublicBaseController
{
    private readonly IPublicEbookReviewRepository _repo;

    public PublicEbookReviewController(IPublicEbookReviewRepository repo)
        => _repo = repo;

    [HttpPost("Search")]
    public async Task<IActionResult> Search([FromBody] PublicEbookReviewSearchRequest r)
    {
        var paged = await _repo.SearchAsync(r);
        return Ok(ApiResponse<PagedResult<PublicEbookReviewResponse>>.Ok(new PagedResult<PublicEbookReviewResponse>
        {
            Items      = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            PageIndex  = paged.PageIndex,
            PageSize   = paged.PageSize
        }));
    }

    [HttpPost("SearchAll")]
    public async Task<IActionResult> SearchAll([FromBody] PublicEbookReviewSearchRequest r)
    {
        var items = await _repo.SearchAllAsync(r);
        return Ok(ApiResponse<List<PublicEbookReviewResponse>>.Ok(items.Select(ToResponse).ToList()));
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add([FromBody] PublicEbookReviewRequest r)
    {
        var entity = await _repo.AddReviewAsync(r);
        return Ok(ApiResponse<PublicEbookReviewResponse>.Ok(ToResponse(entity)));
    }

    private static PublicEbookReviewResponse ToResponse(EbookReview x) => new()
    {
        Id             = x.Id,
        ItemId         = x.ItemId,
        Rating         = x.Rating,
        DisplayName    = x.DisplayName,
        Content        = x.Content,
        Status         = x.Status,
        CreatedRowDate = x.CreatedRowDate
    };
}
