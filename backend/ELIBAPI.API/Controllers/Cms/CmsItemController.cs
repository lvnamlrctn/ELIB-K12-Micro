using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class CmsItemController : GenericController<CmsItem, CmsItemSearchRequest, CmsItemRequest>
{
    public CmsItemController(IGenericRepository<CmsItem, CmsItemSearchRequest, CmsItemRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("CMSITEM", "add")]
    public override async Task<IActionResult> Add([FromBody] CmsItemRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CMSITEM", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CmsItemRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CMSITEM", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CMSITEM", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("CMSITEM", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CMSITEM", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CMSITEM", "view")]
    public override async Task<IActionResult> Search([FromBody] CmsItemSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CMSITEM", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CmsItemSearchRequest request) => await base.SearchAll(request);
}

