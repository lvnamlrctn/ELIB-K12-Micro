using ELIBAPI.API.Filters;
using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Cms;

/// <summary>Báo cáo chất lượng tìm kiếm OPAC (Đợt 9, port từ ELIB-LRC) — dùng chung quyền DASHBOARD.view
/// (không thêm module riêng, khớp LRC). Lọc TenantId của nhân viên xem (đa tenant, khác LRC) — super-admin
/// (không tenant) xem toàn hệ thống.</summary>
[ApiController]
[Authorize]
[Route("api/Cms/SearchQuality")]
public class SearchQualityController(SearchQualityService service) : ControllerBase
{
    [HttpGet]
    [Permission("DASHBOARD", "view")]
    public async Task<IActionResult> Get([FromQuery] int days = 30)
    {
        var tenantId = GetTenantId();
        return Ok(ApiResponse<object>.Ok(await service.ReportAsync(days, tenantId)));
    }

    private long? GetTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return long.TryParse(claim, out var id) ? id : null;
    }
}
