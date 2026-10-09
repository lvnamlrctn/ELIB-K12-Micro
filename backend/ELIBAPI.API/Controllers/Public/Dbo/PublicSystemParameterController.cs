using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicSystemParameterController : PublicBaseController
{
    private readonly IPublicSystemParameterRepository _repo;
    private readonly IStringLocalizer<SharedResource> _loc;

    public PublicSystemParameterController(IPublicSystemParameterRepository repo, IStringLocalizer<SharedResource> loc)
    {
        _repo = repo;
        _loc  = loc;
    }

    [HttpGet("GetSystemPara")]
    public async Task<IActionResult> GetSystemPara([FromQuery] string code, [FromQuery] Guid? TenantId = null)
    {
        var param = await _repo.GetByCodeAsync(code, TenantId);

        if (param == null)
            return NotFound(ApiResponse<PublicSystemParameterResponse>.Fail(_loc["NotFound"]));

        return Ok(ApiResponse<PublicSystemParameterResponse>.Ok(param));
    }
}
