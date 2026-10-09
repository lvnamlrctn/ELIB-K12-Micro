using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class CategoryController : GenericController<Category, CategorySearchRequest, CategoryRequest>
{
    private readonly ICategoryRepository _categoryRepo;

    public CategoryController(ICategoryRepository repo) : base(repo)
        => _categoryRepo = repo;

    // ── Standard endpoints ────────────────────────────────────────────────────
    [HttpPost("Add")]    [Permission("NEWS_CATEGORIES", "add")]
    public override async Task<IActionResult> Add([FromBody] CategoryRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("NEWS_CATEGORIES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CategoryRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("NEWS_CATEGORIES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("NEWS_CATEGORIES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]                  [Permission("NEWS_CATEGORIES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]    [Permission("NEWS_CATEGORIES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("NEWS_CATEGORIES", "view")]
    public override async Task<IActionResult> Search([FromBody] CategorySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")] [Permission("NEWS_CATEGORIES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CategorySearchRequest request) => await base.SearchAll(request);

    // ── NEW: GetTree ─ POST /api/Cms/Category/GetTree ─────────────────────────
    [HttpPost("GetTree")]
    [Permission("NEWS_CATEGORIES", "view")]
    public async Task<IActionResult> GetTree([FromBody] CategorySearchRequest request)
    {
        var tree = await _categoryRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<CategoryTreeResponse>>.Ok(tree));
    }

    // ── NEW: UpdateOrder ─ PUT /api/Cms/Category/UpdateOrder ──────────────────
    [HttpPut("UpdateOrder")]
    [Permission("NEWS_CATEGORIES", "edit")]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest request)
    {
        try
        {
            await _categoryRepo.UpdateOrderAsync(request.PublicId, request.NewOrder);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── NEW: Move ─ PUT /api/Cms/Category/Move/{publicId} ─────────────────────
    [HttpPut("Move/{publicId:guid}")]
    [Permission("NEWS_CATEGORIES", "edit")]
    public async Task<IActionResult> Move(Guid publicId, [FromBody] MoveCategoryRequest request)
    {
        try
        {
            var result = await _categoryRepo.MoveAsync(publicId, request.NewParentId, request.NewOrder);
            return Ok(ApiResponse<Category>.Ok(result, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<Category>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<Category>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── NEW: DeleteWithChildren ─ DELETE /api/Cms/Category/DeleteWithChildren/{publicId}
    [HttpDelete("DeleteWithChildren/{publicId:guid}")]
    [Permission("NEWS_CATEGORIES", "delete")]
    public async Task<IActionResult> DeleteWithChildren(Guid publicId)
    {
        try
        {
            await _categoryRepo.DeleteWithChildrenAsync(publicId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }
}
