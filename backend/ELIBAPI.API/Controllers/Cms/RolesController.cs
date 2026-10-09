using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class RolesController : GenericController<Roles, RolesSearchRequest, RolesRequest>
{
    public RolesController(IGenericRepository<Roles, RolesSearchRequest, RolesRequest> repo) : base(repo) { }

    [HttpPost("Add")]    [Permission("ROLES", "add")]
    public override async Task<IActionResult> Add([FromBody] RolesRequest request) => await base.Add(request);

    [InvalidatePermissionStamps] [HttpPut("Update/{publicId:guid}")] [Permission("ROLES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] RolesRequest request) => await base.Update(publicId, request);

    [InvalidatePermissionStamps] [HttpDelete("Delete/{publicId:guid}")] [Permission("ROLES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [InvalidatePermissionStamps] [HttpPut("ChangeStatus")] [Permission("ROLES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> Search([FromBody] RolesSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] RolesSearchRequest request) => await base.SearchAll(request);
}

