using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class PageController : GenericController<Page, PageSearchRequest, PageRequest>
{
    public PageController(IGenericRepository<Page, PageSearchRequest, PageRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("PAGE", "add")]
    public override async Task<IActionResult> Add([FromBody] PageRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("PAGE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PageRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("PAGE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("PAGE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("PAGE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("PAGE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("PAGE", "view")]
    public override async Task<IActionResult> Search([FromBody] PageSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("PAGE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PageSearchRequest request) => await base.SearchAll(request);
}

