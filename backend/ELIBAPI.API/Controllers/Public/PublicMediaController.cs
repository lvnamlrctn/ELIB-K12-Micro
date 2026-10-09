using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers;

[AllowAnonymous]
[Route("api/public/media")]
[ApiController]
public class PublicMediaController(IMinioService minio) : ControllerBase
{
    [HttpGet("{**path}")]
    public async Task<IActionResult> Get(string path)
    {
        try
        {
            var (stream, contentType) = await minio.GetPublicObjectStreamAsync(path);
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            return NotFound(new { path, error = ex.Message });
        }
    }
}
