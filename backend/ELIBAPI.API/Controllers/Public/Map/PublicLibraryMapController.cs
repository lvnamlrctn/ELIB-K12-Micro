using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

/// <summary>Sơ đồ 2D thư viện + tra vị trí bản sách cho OPAC công khai. Không cần JWT.</summary>
[Route("api/public/[controller]")]
public class PublicLibraryMapController(IPublicLibraryMapRepository repo) : PublicBaseController
{
    [HttpGet("Buildings")]
    public async Task<IActionResult> GetBuildings()
    {
        var buildings = await repo.GetBuildingsAsync();
        return Ok(ApiResponse<List<PublicMapBuildingResponse>>.Ok(buildings));
    }

    [HttpGet("Floors/{buildingId:long}")]
    public async Task<IActionResult> GetFloors(long buildingId)
    {
        var floors = await repo.GetFloorsAsync(buildingId);
        return Ok(ApiResponse<List<PublicMapFloorSummaryResponse>>.Ok(floors));
    }

    [HttpGet("Floor/{floorId:long}")]
    public async Task<IActionResult> GetFloor(long floorId)
    {
        var floor = await repo.GetFloorAsync(floorId);
        if (floor == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy tầng"));
        return Ok(ApiResponse<PublicMapFloorResponse>.Ok(floor));
    }

    [HttpGet("LocateBarcode/{barcodeId:long}")]
    public async Task<IActionResult> LocateBarcode(long barcodeId)
    {
        var location = await repo.LocateBarcodeAsync(barcodeId);
        return Ok(ApiResponse<PublicBookLocationResponse>.Ok(location));
    }
}
