using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookReviewController : GenericController<EbookReview, EbookReviewSearchRequest, EbookReviewRequest>
{
    private readonly IEbookReviewRepository _reviewRepo;

    public EbookReviewController(IEbookReviewRepository repo) : base(repo)
    {
        _reviewRepo = repo;
    }

    [HttpPost("Add")]
    [Permission("EBOOKREVIEW", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookReviewRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("EBOOKREVIEW", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookReviewRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("EBOOKREVIEW", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("EBOOKREVIEW", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("EBOOKREVIEW", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("EBOOKREVIEW", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("EBOOKREVIEW", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookReviewSearchRequest request)
    {
        var result = await _reviewRepo.SearchWithTitleAsync(request);
        return Ok(ApiResponse<PagedResult<EbookReviewResponse>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("EBOOKREVIEW", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookReviewSearchRequest request)
    {
        var result = await _reviewRepo.SearchAllWithTitleAsync(request);
        return Ok(ApiResponse<List<EbookReviewResponse>>.Ok(result));
    }
}
