using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicCounterController(IPublicCounterRepository repo) : PublicBaseController
{
    [HttpGet("Stats")]
    public async Task<IActionResult> Stats([FromQuery] Guid? TenantId)
    {
        var result = await repo.GetStatsAsync(TenantId);
        return Ok(ApiResponse<PublicCounterStatsResponse>.Ok(result));
    }
}
