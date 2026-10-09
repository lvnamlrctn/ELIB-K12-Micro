using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class PermissionController : GenericController<Permission, PermissionSearchRequest, PermissionRequest>
{
    public PermissionController(IGenericRepository<Permission, PermissionSearchRequest, PermissionRequest> repo) : base(repo) { }

    [HttpPost("Add")]    [Permission("MODULES", "add")]
    public override async Task<IActionResult> Add([FromBody] PermissionRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("MODULES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PermissionRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("MODULES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("MODULES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("MODULES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("MODULES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MODULES", "view")]
    public override async Task<IActionResult> Search([FromBody] PermissionSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MODULES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PermissionSearchRequest request) => await base.SearchAll(request);
}

