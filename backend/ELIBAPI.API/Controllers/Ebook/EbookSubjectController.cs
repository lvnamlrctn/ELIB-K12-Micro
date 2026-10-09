using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class EbookSubjectController : GenericController<EbookSubject, EbookSubjectSearchRequest, EbookSubjectRequest>
{
    private readonly IEbookSubjectRepository _subjectRepo;

    public EbookSubjectController(IEbookSubjectRepository repo) : base(repo) => _subjectRepo = repo;

    [HttpPost("Add")]    [Permission("EBOOK_SUBJECT", "add")]
    public override async Task<IActionResult> Add([FromBody] EbookSubjectRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("EBOOK_SUBJECT", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] EbookSubjectRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("EBOOK_SUBJECT", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("EBOOK_SUBJECT", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")] [Permission("EBOOK_SUBJECT", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("EBOOK_SUBJECT", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("EBOOK_SUBJECT", "view")]
    public override async Task<IActionResult> Search([FromBody] EbookSubjectSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")] [Permission("EBOOK_SUBJECT", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] EbookSubjectSearchRequest request) => await base.SearchAll(request);

    [HttpPost("GetTree")] [Permission("EBOOK_SUBJECT", "view")]
    public async Task<IActionResult> GetTree([FromBody] EbookSubjectSearchRequest request)
    {
        var tree = await _subjectRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<EbookSubjectTreeResponse>>.Ok(tree));
    }

    [HttpPut("UpdateOrder")] [Permission("EBOOK_SUBJECT", "edit")]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest request)
    {
        try
        {
            await _subjectRepo.UpdateOrderAsync(request.PublicId, request.NewOrder);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    [HttpPut("Move/{publicId:guid}")] [Permission("EBOOK_SUBJECT", "edit")]
    public async Task<IActionResult> Move(Guid publicId, [FromBody] MoveCategoryRequest request)
    {
        try
        {
            var result = await _subjectRepo.MoveAsync(publicId, request.NewParentId, request.NewOrder);
            return Ok(ApiResponse<EbookSubject>.Ok(result, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<EbookSubject>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<EbookSubject>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    [HttpDelete("DeleteWithChildren/{publicId:guid}")] [Permission("EBOOK_SUBJECT", "delete")]
    public async Task<IActionResult> DeleteWithChildren(Guid publicId)
    {
        try
        {
            await _subjectRepo.DeleteWithChildrenAsync(publicId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }
}
