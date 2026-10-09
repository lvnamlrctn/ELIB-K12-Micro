using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class StoreController : GenericController<Store, StoreSearchRequest, StoreRequest>
{
    public StoreController(IGenericRepository<Store, StoreSearchRequest, StoreRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("STORES", "add")]
    public override async Task<IActionResult> Add([FromBody] StoreRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("STORES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] StoreRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("STORES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("STORES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("STORES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("STORES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("STORES", "view")]
    public override async Task<IActionResult> Search([FromBody] StoreSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("STORES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] StoreSearchRequest request) => await base.SearchAll(request);
}
