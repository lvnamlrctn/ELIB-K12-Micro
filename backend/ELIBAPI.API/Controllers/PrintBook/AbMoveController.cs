using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class AbMoveController : GenericController<AbMove, AbMoveSearchRequest, AbMoveRequest>
{
    public AbMoveController(IGenericRepository<AbMove, AbMoveSearchRequest, AbMoveRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("AB_MOVES", "add")]
    public override async Task<IActionResult> Add([FromBody] AbMoveRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AB_MOVES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AbMoveRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AB_MOVES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AB_MOVES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> Search([FromBody] AbMoveSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AB_MOVES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AbMoveSearchRequest request) => await base.SearchAll(request);
}
