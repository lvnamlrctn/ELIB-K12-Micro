using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookTopicController : GenericController<EbookTopic, EbookTopicSearchRequest, EbookTopicRequest>
{
    private readonly IEbookTopicRepository _topicRepo;

    public EbookTopicController(IEbookTopicRepository repo) : base(repo) => _topicRepo = repo;

    [HttpPost("Add")]    [Permission("EBOOK_TOPIC", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookTopicRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("EBOOK_TOPIC", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookTopicRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("EBOOK_TOPIC", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("EBOOK_TOPIC", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")] [Permission("EBOOK_TOPIC", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("EBOOK_TOPIC", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("EBOOK_TOPIC", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookTopicSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")] [Permission("EBOOK_TOPIC", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookTopicSearchRequest request) => await base.SearchAll(request);

    [HttpPost("GetTree")] [Permission("EBOOK_TOPIC", "view")]
    public async Task<IActionResult> GetTree([FromBody] EbookTopicSearchRequest request)
    {
        var tree = await _topicRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<EbookTopicTreeResponse>>.Ok(tree));
    }

    [HttpPut("UpdateOrder")] [Permission("EBOOK_TOPIC", "edit")]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest request)
    {
        try
        {
            await _topicRepo.UpdateOrderAsync(request.PublicId, request.NewOrder);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    [HttpPut("Move/{publicId:guid}")] [Permission("EBOOK_TOPIC", "edit")]
    public async Task<IActionResult> Move(Guid publicId, [FromBody] MoveCategoryRequest request)
    {
        try
        {
            var result = await _topicRepo.MoveAsync(publicId, request.NewParentId, request.NewOrder);
            return Ok(ApiResponse<EbookTopic>.Ok(result, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<EbookTopic>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<EbookTopic>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    [HttpDelete("DeleteWithChildren/{publicId:guid}")] [Permission("EBOOK_TOPIC", "delete")]
    public async Task<IActionResult> DeleteWithChildren(Guid publicId)
    {
        try
        {
            await _topicRepo.DeleteWithChildrenAsync(publicId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }
}
