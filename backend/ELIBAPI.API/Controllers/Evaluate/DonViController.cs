using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Evaluate;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Evaluate;

/// <summary>Đơn vị đào tạo (Khoa/Viện/Trường) — cây phân cấp sở hữu Chương trình đào tạo, dùng cho Cây cơ
/// sở môn học (Đợt 8). KHÁC với OrgController (api/Dbo/Org — phòng ban nội bộ hệ thống).</summary>
[Route("api/Evaluate/DonVi")]
public class DonViController : GenericController<DonVi, DonViSearchRequest, DonViRequest>
{
    private readonly IDonViRepository _donViRepo;

    public DonViController(IDonViRepository repo) : base(repo)
        => _donViRepo = repo;

    [HttpPost("Add")]    [Permission("DONVI", "add")]
    public override Task<IActionResult> Add([FromBody] DonViRequest request) => base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("DONVI", "edit")]
    public override Task<IActionResult> Update(Guid publicId, [FromBody] DonViRequest request) => base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("DONVI", "delete")]
    public override Task<IActionResult> Delete(Guid publicId) => base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("DONVI", "edit")]
    public override Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => base.ChangeStatus(request);

    [HttpGet("{id:long}")]               [Permission("DONVI", "view")]
    public override Task<IActionResult> GetById(long id) => base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")] [Permission("DONVI", "view")]
    public override Task<IActionResult> GetByPublicId(Guid publicId) => base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("DONVI", "view")]
    public override Task<IActionResult> Search([FromBody] DonViSearchRequest request) => base.Search(request);

    [HttpPost("SearchAll")] [Permission("DONVI", "view")]
    public override Task<IActionResult> SearchAll([FromBody] DonViSearchRequest request) => base.SearchAll(request);

    // ── GetTree ───────────────────────────────────────────────────────────────

    [HttpPost("GetTree")]
    [Permission("DONVI", "view")]
    public async Task<IActionResult> GetTree([FromBody] DonViSearchRequest request)
    {
        var tree = await _donViRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<DonViTreeResponse>>.Ok(tree));
    }

    // ── UpdateOrder ───────────────────────────────────────────────────────────

    [HttpPut("UpdateOrder")]
    [Permission("DONVI", "edit")]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest request)
    {
        try
        {
            await _donViRepo.UpdateOrderAsync(request.PublicId, request.NewOrder);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    [HttpPut("Move/{publicId:guid}")]
    [Permission("DONVI", "edit")]
    public async Task<IActionResult> Move(Guid publicId, [FromBody] MoveCategoryRequest request)
    {
        try
        {
            var result = await _donViRepo.MoveAsync(publicId, request.NewParentId, request.NewOrder);
            return Ok(ApiResponse<DonVi>.Ok(result, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<DonVi>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<DonVi>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }

    // ── DeleteWithChildren ────────────────────────────────────────────────────

    [HttpDelete("DeleteWithChildren/{publicId:guid}")]
    [Permission("DONVI", "delete")]
    public async Task<IActionResult> DeleteWithChildren(Guid publicId)
    {
        try
        {
            await _donViRepo.DeleteWithChildrenAsync(publicId);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["DeleteSuccess"]));
        }
        catch (KeyNotFoundException)        { return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.Fail(Localizer["ForbiddenDepartment"], 403)); }
    }
}
