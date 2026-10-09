using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Ebook;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Ebook;

[Route("api/Ebook/[controller]")]
public class PolicyDigitalController : GenericController<PolicyDigital, PolicyDigitalSearchRequest, PolicyDigitalRequest>
{
    private readonly IPolicyDigitalRepository _policyRepo;

    public PolicyDigitalController(IPolicyDigitalRepository repo) : base(repo)
    {
        _policyRepo = repo;
    }

    [HttpPost("Add")]
    [Permission("ACCESS_POLICY", "add")]
    public override async Task<IActionResult> Add([FromBody] PolicyDigitalRequest request)
    {
        var result = await _policyRepo.UpsertAsync(request);
        return Ok(ApiResponse<PolicyDigital>.Ok(result, Localizer["AddSuccess"]));
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("ACCESS_POLICY", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] PolicyDigitalRequest request) => await base.Update(publicId, request);

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("ACCESS_POLICY", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId) => await base.Delete(publicId);

    [HttpPut("ChangeStatus")]
    [Permission("ACCESS_POLICY", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => await base.ChangeStatus(request);

    

    [HttpGet("{id:long}")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> Search([FromBody] PolicyDigitalSearchRequest request)
    {
        var result = await _policyRepo.SearchWithNameAsync(request);
        return Ok(ApiResponse<PagedResult<PolicyDigitalResponse>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("ACCESS_POLICY", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] PolicyDigitalSearchRequest request)
    {
        var result = await _policyRepo.SearchAllWithNameAsync(request);
        return Ok(ApiResponse<List<PolicyDigitalResponse>>.Ok(result));
    }
}

