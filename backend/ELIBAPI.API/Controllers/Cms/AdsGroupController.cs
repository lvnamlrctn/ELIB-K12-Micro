using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class AdsGroupController : GenericController<ADSGroup, AdsGroupSearchRequest, AdsGroupRequest>
{
    public AdsGroupController(IGenericRepository<ADSGroup, AdsGroupSearchRequest, AdsGroupRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("ADSGROUP", "add")]
    public override async Task<IActionResult> Add([FromBody] AdsGroupRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("ADSGROUP", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] AdsGroupRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("ADSGROUP", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("ADSGROUP", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("ADSGROUP", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ADSGROUP", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ADSGROUP", "view")]
    public override async Task<IActionResult> Search([FromBody] AdsGroupSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("ADSGROUP", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] AdsGroupSearchRequest request) => await base.SearchAll(request);
}

