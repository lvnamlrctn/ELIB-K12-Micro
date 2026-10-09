using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class DocGroupController : GenericController<DocGroup, DocGroupSearchRequest, DocGroupRequest>
{
    public DocGroupController(IGenericRepository<DocGroup, DocGroupSearchRequest, DocGroupRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CIRC_POLICIES", "add")]
    public override async Task<IActionResult> Add([FromBody] DocGroupRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CIRC_POLICIES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] DocGroupRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CIRC_POLICIES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CIRC_POLICIES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> Search([FromBody] DocGroupSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] DocGroupSearchRequest request) => await base.SearchAll(request);
}
