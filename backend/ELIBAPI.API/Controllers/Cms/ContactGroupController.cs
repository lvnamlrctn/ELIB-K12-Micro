using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class ContactGroupController : GenericController<ContactGroup, ContactGroupSearchRequest, ContactGroupRequest>
{
    public ContactGroupController(IGenericRepository<ContactGroup, ContactGroupSearchRequest, ContactGroupRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CONTACTGROUP", "add")]
    public override async Task<IActionResult> Add([FromBody] ContactGroupRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CONTACTGROUP", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ContactGroupRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CONTACTGROUP", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CONTACTGROUP", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("CONTACTGROUP", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CONTACTGROUP", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CONTACTGROUP", "view")]
    public override async Task<IActionResult> Search([FromBody] ContactGroupSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CONTACTGROUP", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ContactGroupSearchRequest request) => await base.SearchAll(request);
}

