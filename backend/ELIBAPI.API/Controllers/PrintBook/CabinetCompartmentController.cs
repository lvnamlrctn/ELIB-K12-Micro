using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

[Route("api/PrintBook/[controller]")]
public class CabinetCompartmentController : GenericController<CabinetCompartment, CabinetCompartmentSearchRequest, CabinetCompartmentRequest>
{
    private readonly ICabinetCompartmentRepository _compartmentRepo;

    public CabinetCompartmentController(ICabinetCompartmentRepository repo) : base(repo)
        => _compartmentRepo = repo;

    [HttpPost("Add")]
    [Permission("CABINETS", "add")]
    public override async Task<IActionResult> Add([FromBody] CabinetCompartmentRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CABINETS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] CabinetCompartmentRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CABINETS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CABINETS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> Search([FromBody] CabinetCompartmentSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CABINETS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] CabinetCompartmentSearchRequest request) => await base.SearchAll(request);

    [HttpGet("ByCabinet/{cabinetPublicId:guid}")]
    [Permission("CABINETS", "view")]
    public async Task<IActionResult> ByCabinet(Guid cabinetPublicId)
    {
        var (cabinet, compartments, occupiedIds) = await _compartmentRepo.GetByCabinetAsync(cabinetPublicId);
        if (cabinet == null) return NotFound(ApiResponse<object>.Fail(Localizer["NotFound"], 404));

        var items = compartments.Select(c => new
        {
            c.Id,
            c.PublicId,
            c.RowIndex,
            c.ColIndex,
            c.Code,
            c.Name,
            c.Status,
            c.Note,
            IsOccupied = occupiedIds.Contains(c.Id)
        });

        return Ok(ApiResponse<object>.Ok(new { cabinetId = cabinet.Id, cabinet.Rows, cabinet.Cols, items }));
    }
}
