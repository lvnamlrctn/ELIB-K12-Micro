using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Map;

[Route("api/Map/[controller]")]
public class MapFloorUtilityController : GenericController<MapFloorUtility, MapFloorUtilitySearchRequest, MapFloorUtilityRequest>
{
    public MapFloorUtilityController(IGenericRepository<MapFloorUtility, MapFloorUtilitySearchRequest, MapFloorUtilityRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("MAP_FLOOR", "add")]
    public override async Task<IActionResult> Add([FromBody] MapFloorUtilityRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("MAP_FLOOR", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] MapFloorUtilityRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("MAP_FLOOR", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("MAP_FLOOR", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("MAP_FLOOR", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("MAP_FLOOR", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("MAP_FLOOR", "view")]
    public override async Task<IActionResult> Search([FromBody] MapFloorUtilitySearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("MAP_FLOOR", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] MapFloorUtilitySearchRequest request) => await base.SearchAll(request);
}
