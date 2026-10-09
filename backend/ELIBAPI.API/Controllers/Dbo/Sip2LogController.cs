using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class Sip2LogController : GenericController<Sip2Log, Sip2LogSearchRequest, Sip2LogRequest>
{
    public Sip2LogController(IGenericRepository<Sip2Log, Sip2LogSearchRequest, Sip2LogRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("SIP2LOG", "add")]
    public override async Task<IActionResult> Add([FromBody] Sip2LogRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SIP2LOG", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] Sip2LogRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SIP2LOG", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("SIP2LOG", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("SIP2LOG", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SIP2LOG", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SIP2LOG", "view")]
    public override async Task<IActionResult> Search([FromBody] Sip2LogSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SIP2LOG", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] Sip2LogSearchRequest request) => await base.SearchAll(request);
}

