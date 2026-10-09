using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class AbMoveDetailController : GenericController<AbMoveDetail, AbMoveDetailSearchRequest, AbMoveDetailRequest>
{
    private readonly IStoreMoveService _moves;

    public AbMoveDetailController(IGenericRepository<AbMoveDetail, AbMoveDetailSearchRequest, AbMoveDetailRequest> repo,
        IStoreMoveService moves) : base(repo) => _moves = moves;

    // Dòng của phiếu đã hoàn thành (ĐKCB đã đổi kho) không thêm/sửa/xoá được — xem IStoreMoveService.CompleteAsync.
    private async Task<IActionResult?> LockedAsync(Guid? linePublicId = null, long? moveId = null) =>
        await _moves.LockedErrorAsync(linePublicId: linePublicId, moveId: moveId) is string error ? BadRequest(ApiResponse<string>.Fail(error)) : null;

    [HttpPost("Add")]
    [Permission("AB_MOVES", "add")]
    public override async Task<IActionResult> Add([FromBody] AbMoveDetailRequest request) =>
        await LockedAsync(moveId: request.Move_Id) ?? await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_MOVES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbMoveDetailRequest request) =>
        await LockedAsync(linePublicId: publicId) ?? await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_MOVES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) =>
        await LockedAsync(linePublicId: publicId) ?? await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_MOVES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) =>
        await LockedAsync(linePublicId: request.PublicId) ?? await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> Search([FromBody] AbMoveDetailSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbMoveDetailSearchRequest request) => await base.SearchAll(request);
}
