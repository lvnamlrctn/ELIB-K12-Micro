using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class LinkGroupController : GenericController<LinkGroup, LinkGroupSearchRequest, LinkGroupRequest>
{
    public LinkGroupController(IGenericRepository<LinkGroup, LinkGroupSearchRequest, LinkGroupRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("LINK_GROUP", "add")]
    public override async Task<IActionResult> Add([FromBody] LinkGroupRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("LINK_GROUP", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] LinkGroupRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("LINK_GROUP", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("LINK_GROUP", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("LINK_GROUP", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("LINK_GROUP", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("LINK_GROUP", "view")]
    public override async Task<IActionResult> Search([FromBody] LinkGroupSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("LINK_GROUP", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] LinkGroupSearchRequest request) => await base.SearchAll(request);
}

