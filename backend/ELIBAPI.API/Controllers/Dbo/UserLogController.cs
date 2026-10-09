using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.DTOs.Response;
using ELIBAPI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

[Route("api/Dbo/[controller]")]
public class UserLogController(IUserLogRepository repo) : BaseApiController
{
    [HttpPost("Search")]
    [Permission("SYSTEM_LOG", "view")]
    public async Task<IActionResult> Search([FromBody] UserLogSearchRequest request)
    {
        var result = await repo.SearchAsync(request, GetTenantId(), IsPrivilegedRole());
        return Ok(ApiResponse<PagedResult<UserLogResponse>>.Ok(result));
    }

    [HttpPost("SearchAll")]
    [Permission("SYSTEM_LOG", "view")]
    public async Task<IActionResult> SearchAll([FromBody] UserLogSearchRequest request)
    {
        var items = await repo.SearchAllAsync(request, GetTenantId(), IsPrivilegedRole());
        return Ok(ApiResponse<List<UserLogResponse>>.Ok(items));
    }
}
