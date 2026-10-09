using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class StoreTypeController(IStoreTypeRepository storeTypeRepo)
    : GenericController<StoreType, StoreTypeSearchRequest, StoreTypeRequest>(storeTypeRepo)
{
    [HttpPost("Add")]    [Permission("STORE_TYPES", "add")]
    public override Task<IActionResult> Add([FromBody] StoreTypeRequest request) => base.Add(request);

    [HttpPut("Update/{publicId:guid}")] [Permission("STORE_TYPES", "edit")]
    public override Task<IActionResult> Update(Guid publicId, [FromBody] StoreTypeRequest request) => base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")] [Permission("STORE_TYPES", "delete")]
    public override Task<IActionResult> Delete(Guid publicId) => base.Delete(publicId);

    [HttpPut("ChangeStatus")] [Permission("STORE_TYPES", "edit")]
    public override Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => base.ChangeStatus(request);

    [HttpGet("{id:long}")]                  [Permission("STORE_TYPES", "view")]
    public override Task<IActionResult> GetById(long id) => base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]    [Permission("STORE_TYPES", "view")]
    public override Task<IActionResult> GetByPublicId(Guid publicId) => base.GetByPublicId(publicId);

    [HttpPost("Search")]    [Permission("STORE_TYPES", "view")]
    public override Task<IActionResult> Search([FromBody] StoreTypeSearchRequest request) => base.Search(request);

    [HttpPost("SearchAll")] [Permission("STORE_TYPES", "view")]
    public override Task<IActionResult> SearchAll([FromBody] StoreTypeSearchRequest request) => base.SearchAll(request);

    [HttpPost("GetTree")]
    [Permission("STORE_TYPES", "view")]
    public async Task<IActionResult> GetTree([FromBody] StoreTypeSearchRequest request)
    {
        var tree = await storeTypeRepo.GetTreeAsync(request);
        return Ok(ApiResponse<List<StoreTypeTreeResponse>>.Ok(tree));
    }
}
