using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class AdsController : GenericController<ADS, AdsSearchRequest, AdsRequest>
{
    public AdsController(IGenericRepository<ADS, AdsSearchRequest, AdsRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("ADS", "add")]
    public override async Task<IActionResult> Add([FromBody] AdsRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("ADS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AdsRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("ADS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("ADS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("ADS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ADS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ADS", "view")]
    public override async Task<IActionResult> Search([FromBody] AdsSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("ADS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AdsSearchRequest request) => await base.SearchAll(request);
}

