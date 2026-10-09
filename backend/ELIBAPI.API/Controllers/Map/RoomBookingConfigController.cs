using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Map;

[Route("api/Map/[controller]")]
public class RoomBookingConfigController : GenericController<RoomBookingConfig, RoomBookingConfigSearchRequest, RoomBookingConfigRequest>
{
    public RoomBookingConfigController(IGenericRepository<RoomBookingConfig, RoomBookingConfigSearchRequest, RoomBookingConfigRequest> repo) : base(repo) { }

    [HttpPost("Add")]
    [Permission("ROOM_BOOKING_CONFIG", "add")]
    public override async Task<IActionResult> Add([FromBody] RoomBookingConfigRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("ROOM_BOOKING_CONFIG", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] RoomBookingConfigRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("ROOM_BOOKING_CONFIG", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpGet("{id:long}")]
    [Permission("ROOM_BOOKING_CONFIG", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ROOM_BOOKING_CONFIG", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ROOM_BOOKING_CONFIG", "view")]
    public override async Task<IActionResult> Search([FromBody] RoomBookingConfigSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("ROOM_BOOKING_CONFIG", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] RoomBookingConfigSearchRequest request) => await base.SearchAll(request);
}
