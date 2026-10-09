using ELIBAPI.Core.Common;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Dbo;

/// <summary>Cảnh báo chất lượng dữ liệu (Đợt 21 — port từ ELIB-LRC <c>AdminDataQualityController</c>). Quyền theo
/// TỪNG quy tắc (module của màn hình sửa tương ứng): quy tắc không có quyền không xuất hiện trong summary — số 0
/// chỉ có nghĩa "đã kiểm tra, không có cảnh báo". Phạm vi = đơn vị JWT (tài khoản hệ thống: toàn bộ).</summary>
[Route("api/Dbo/DataQuality")]
public sealed class DataQualityController(ELIBAPIDbContext db, IPermissionService permissions) : BaseApiController
{
    private long Actor => User.FindFirst("Type")?.Value == "Reader" ? 0 : GetCurrentUserId();

    private async Task<bool> AllowedAsync(string rule)
        => Actor > 0 && await permissions.HasPermissionAsync(Actor, AdminDataQualityService.Module(rule), "view");

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        if (Actor <= 0) return Forbidden();
        var service = new AdminDataQualityService(db, GetTenantId());
        var counts = new Dictionary<string, int>();
        foreach (var rule in AdminDataQualityService.Rules)
            if (await AllowedAsync(rule)) counts[rule] = await service.CountAsync(rule, ct);
        return Ok(ApiResponse<object>.Ok(new { counts, checkedAt = DateTime.UtcNow }));
    }

    [HttpGet("issues")]
    public async Task<IActionResult> Issues(string rule, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        if (!AdminDataQualityService.Rules.Contains(rule)) return BadRequest(ApiResponse<object>.Fail("Quy tắc không được hỗ trợ."));
        if (!await AllowedAsync(rule)) return Forbidden();
        return Ok(ApiResponse<object>.Ok(await new AdminDataQualityService(db, GetTenantId()).ListAsync(rule, page, pageSize, ct)));
    }

    private static IActionResult Forbidden()
        => new ObjectResult(ApiResponse<object>.Fail("Bạn không có quyền xem cảnh báo này.", 403)) { StatusCode = 403 };
}
