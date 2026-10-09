using ELIBAPI.API.Filters;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class SystemParameterController : GenericController<SystemParameter, SystemParameterSearchRequest, SystemParameterRequest>
{
    private readonly ICacheInvalidator _cacheInvalidator;

    public SystemParameterController(IGenericRepository<SystemParameter, SystemParameterSearchRequest, SystemParameterRequest> repo, ICacheInvalidator cacheInvalidator)
        : base(repo) { _cacheInvalidator = cacheInvalidator; }

    [HttpPost("Add")]
    [Permission("SYSTEM_PARAMS", "add")]
    public override async Task<IActionResult> Add([FromBody] SystemParameterRequest request)
    {
        var result = await base.Add(request);
        _cacheInvalidator.Invalidate(nameof(PublicSystemParameterResponse));
        return result;
    }

    [HttpPut("Update/{publicId:guid}")]
    [Permission("SYSTEM_PARAMS", "edit")]
    public override async Task<IActionResult> Update(Guid publicId, [FromBody] SystemParameterRequest request)
    {
        var result = await base.Update(publicId, request);
        _cacheInvalidator.Invalidate(nameof(PublicSystemParameterResponse));
        return result;
    }

    [HttpDelete("Delete/{publicId:guid}")]
    [Permission("SYSTEM_PARAMS", "delete")]
    public override async Task<IActionResult> Delete(Guid publicId)
    {
        var result = await base.Delete(publicId);
        _cacheInvalidator.Invalidate(nameof(PublicSystemParameterResponse));
        return result;
    }

    [HttpPut("ChangeStatus")]
    [Permission("SYSTEM_PARAMS", "edit")]
    public override async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
    {
        var result = await base.ChangeStatus(request);
        _cacheInvalidator.Invalidate(nameof(PublicSystemParameterResponse));
        return result;
    }



    [HttpGet("{id:long}")]
    [Permission("SYSTEM_PARAMS", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("SYSTEM_PARAMS", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("SYSTEM_PARAMS", "view")]
    public override async Task<IActionResult> Search([FromBody] SystemParameterSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("SYSTEM_PARAMS", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] SystemParameterSearchRequest request) => await base.SearchAll(request);
}

