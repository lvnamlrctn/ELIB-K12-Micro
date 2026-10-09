using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.EOffice;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.EOffice;

[Route("api/EOffice/[controller]")]
public class AgencyController : GenericController<Agency, AgencySearchRequest, AgencyRequest>
{
    public AgencyController(IGenericRepository<Agency, AgencySearchRequest, AgencyRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("AGENCY", "add")]
    public override async Task<IActionResult> Add([FromBody] AgencyRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("AGENCY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AgencyRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("AGENCY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("AGENCY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("AGENCY", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("AGENCY", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("AGENCY", "view")]
    public override async Task<IActionResult> Search([FromBody] AgencySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("AGENCY", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AgencySearchRequest request) => await base.SearchAll(request);
}

