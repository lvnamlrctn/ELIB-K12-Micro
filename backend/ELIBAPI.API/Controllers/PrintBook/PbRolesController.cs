using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class PbRolesController : GenericController<PbRoles, PbRolesSearchRequest, PbRolesRequest>
{
    public PbRolesController(IGenericRepository<PbRoles, PbRolesSearchRequest, PbRolesRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("PB_ROLES", "add")]
    public override async Task<IActionResult> Add([FromBody] PbRolesRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("PB_ROLES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PbRolesRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("PB_ROLES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("PB_ROLES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("PB_ROLES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("PB_ROLES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("PB_ROLES", "view")]
    public override async Task<IActionResult> Search([FromBody] PbRolesSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("PB_ROLES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PbRolesSearchRequest request) => await base.SearchAll(request);
}
