using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.PrintBook;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.PrintBook;

public record PolicyCircTreeItem(int Id, Guid PublicId, long? ReaderTypeId, string? ReaderTypeName, int? NumberOfBook, int? NumberOfDate, int? StoreId, string? StoreName);

[Route("api/PrintBook/Circulation/CircPolicy")]
public class CirculationCircPolicyController : GenericController<PolicyCirc, PolicyCircSearchRequest, PolicyCircRequest>
{
    private readonly IPolicyCircRepository _policyCircRepo;

    public CirculationCircPolicyController(IPolicyCircRepository repo) : base(repo)
        => _policyCircRepo = repo;

    [HttpGet("ByCircPlace/{circPlacePublicId:guid}")]
    [Permission("CIRC_POLICIES", "view")]
    public async Task<IActionResult> ByCircPlace(Guid circPlacePublicId)
    {
        var rows = await _policyCircRepo.GetByCircPlaceAsync(circPlacePublicId);
        var items = rows.Select(x => new PolicyCircTreeItem(
            x.Policy.Id, x.Policy.PublicId, x.Policy.ReaderType, x.ReaderTypeName, x.Policy.NumberOfBook, x.Policy.NumberOfDate,
            x.Policy.Store, x.StoreName)).ToList();
        return Ok(ApiResponse<List<PolicyCircTreeItem>>.Ok(items));
    }

    [HttpPost("Add")]
    [Permission("CIRC_POLICIES", "add")]
    public override async Task<IActionResult> Add([FromBody] PolicyCircRequest request) => await base.Add(request);

    [HttpPut("Update/{publicId:guid}")]
    [Permission("CIRC_POLICIES", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PolicyCircRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("CIRC_POLICIES", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("CIRC_POLICIES", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    [HttpGet("{id:long}")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> Search([FromBody] PolicyCircSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("CIRC_POLICIES", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PolicyCircSearchRequest request) => await base.SearchAll(request);
}
