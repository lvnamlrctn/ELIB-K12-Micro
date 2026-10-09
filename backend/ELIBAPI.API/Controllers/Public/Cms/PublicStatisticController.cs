using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[Route("api/public/[controller]")]
public class PublicStatisticController(IPublicCounterRepository repo) : PublicBaseController
{
    [HttpPost("TrackVisit")]
    public async Task<IActionResult> TrackVisit([FromBody] PublicTrackVisitRequest r)
    {
        var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        await repo.TrackVisitAsync(r.TenantId, ip);
        return Ok(ApiResponse<string>.Ok("OK"));
    }
}

public class PublicTrackVisitRequest
{
    public Guid? TenantId { get; set; }
}
