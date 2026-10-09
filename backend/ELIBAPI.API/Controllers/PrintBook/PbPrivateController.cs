using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class PbPrivateController : GenericController<PbPrivate, PbPrivateSearchRequest, PbPrivateRequest>
{
    public PbPrivateController(IGenericRepository<PbPrivate, PbPrivateSearchRequest, PbPrivateRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("PB_PRIVATE", "add")]
    public override async Task<IActionResult> Add([FromBody] PbPrivateRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("PB_PRIVATE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PbPrivateRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("PB_PRIVATE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("PB_PRIVATE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("PB_PRIVATE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("PB_PRIVATE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("PB_PRIVATE", "view")]
    public override async Task<IActionResult> Search([FromBody] PbPrivateSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("PB_PRIVATE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PbPrivateSearchRequest request) => await base.SearchAll(request);
}
