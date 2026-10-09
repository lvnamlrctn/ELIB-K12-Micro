using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Cms;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

[Route("api/Cms/[controller]")]
public class ItemTypeController : GenericController<ItemType, ItemTypeSearchRequest, ItemTypeRequest>
{
    public ItemTypeController(IGenericRepository<ItemType, ItemTypeSearchRequest, ItemTypeRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("ITEMTYPE", "add")]
    public override async Task<IActionResult> Add([FromBody] ItemTypeRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("ITEMTYPE", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] ItemTypeRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("ITEMTYPE", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("ITEMTYPE", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("ITEMTYPE", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ITEMTYPE", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ITEMTYPE", "view")]
    public override async Task<IActionResult> Search([FromBody] ItemTypeSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("ITEMTYPE", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] ItemTypeSearchRequest request) => await base.SearchAll(request);
}

