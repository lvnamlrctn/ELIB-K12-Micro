using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class CircPlaceController : GenericController<CircPlace, CircPlaceSearchRequest, CircPlaceRequest>
{
    private readonly ICircPlaceRepository _circPlaceRepo;

    public CircPlaceController(ICircPlaceRepository repo) : base(repo)
        => _circPlaceRepo = repo;

    [HttpGet("GetMapping/{publicId:guid}")]
    [Permission("CIRC_PLACES", "view")]
    public async Task<IActionResult> GetMapping(Guid publicId)
    {
        var result = await _circPlaceRepo.GetMappingAsync(publicId);
        return Ok(ApiResponse<object>.Ok(new { storeIds = result.StoreIds, readerTypeIds = result.ReaderTypeIds }));
    }

    [HttpPost("ReplaceStoreMapping")]
    [Permission("CIRC_PLACES", "edit")]
    public async Task<IActionResult> ReplaceStoreMapping([FromBody] CircPlaceReplaceStoreMappingRequest request)
    {
        try
        {
            await _circPlaceRepo.ReplaceStoreMappingAsync(request.CircPlacePublicId, request.StoreIds);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    [HttpPost("ReplaceReaderTypeMapping")]
    [Permission("CIRC_PLACES", "edit")]
    public async Task<IActionResult> ReplaceReaderTypeMapping([FromBody] CircPlaceReplaceReaderTypeMappingRequest request)
    {
        try
        {
            await _circPlaceRepo.ReplaceReaderTypeMappingAsync(request.CircPlacePublicId, request.ReaderTypeIds);
            return Ok(ApiResponse<object>.Ok(null!, Localizer["UpdateSuccess"]));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));
        }
    }

    [HttpPost("Add")]
    [Permission("CIRC_PLACES", "add")]
    public override async Task<IActionResult> Add([FromBody] CircPlaceRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CIRC_PLACES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CircPlaceRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CIRC_PLACES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CIRC_PLACES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CIRC_PLACES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CIRC_PLACES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CIRC_PLACES", "view")]
    public override async Task<IActionResult> Search([FromBody] CircPlaceSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CIRC_PLACES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CircPlaceSearchRequest request) => await base.SearchAll(request);
}
