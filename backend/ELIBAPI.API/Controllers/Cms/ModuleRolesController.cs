using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class ModuleRolesController : GenericController<ModuleRoles, ModuleRolesSearchRequest, ModuleRolesRequest>
{
    public ModuleRolesController(IGenericRepository<ModuleRoles, ModuleRolesSearchRequest, ModuleRolesRequest> repo) : base(repo) { }

    [HttpPost("Add")]    [Permission("ROLES", "add")]
    public override async Task<IActionResult> Add([FromBody] ModuleRolesRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("ROLES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ModuleRolesRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("ROLES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("ROLES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> Search([FromBody] ModuleRolesSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("ROLES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ModuleRolesSearchRequest request) => await base.SearchAll(request);
}

