using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class ContactController : GenericController<Contact, ContactSearchRequest, ContactRequest>
{
    public ContactController(IGenericRepository<Contact, ContactSearchRequest, ContactRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CONTACT", "add")]
    public override async Task<IActionResult> Add([FromBody] ContactRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CONTACT", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ContactRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CONTACT", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CONTACT", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("CONTACT", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CONTACT", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CONTACT", "view")]
    public override async Task<IActionResult> Search([FromBody] ContactSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CONTACT", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ContactSearchRequest request) => await base.SearchAll(request);
}

