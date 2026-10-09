using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Map;

/// <summary>Màn thủ thư "Kiểm soát cửa": thiết bị (mã cửa → phòng, khoá API), thẻ quản trị, mở cửa từ web, nhật ký quẹt thẻ (port ELIB-LRC
/// 10-04). Tenant (K12): mọi thao tác trong phạm vi đơn vị — user thường theo đơn vị JWT, tài khoản đặc quyền chọn đơn vị qua
/// <c>tenantId</c> (Guid) trên query (nhật ký: trong body).</summary>
[Route("api/Map/AccessControl")]
public class AccessControlController(AccessControlService access, ELIBAPIDbContext db) : BaseApiController
{
    private Task<TenantScope> ScopeAsync(Guid? tenantId) =>
        TenantScopeHelper.ResolveScopeAsync(db, tenantId, GetTenantId(), IsPrivilegedRole());

    [HttpGet("Devices")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> Devices([FromQuery] Guid? tenantId) =>
        Ok(ApiResponse<List<AccessDevice>>.Ok(await access.DevicesAsync(await ScopeAsync(tenantId))));

    [HttpPost("Devices")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> AddDevice([FromQuery] Guid? tenantId, [FromBody] DeviceInput request) =>
        this.FromServiceResult(await access.SaveDeviceAsync(null, request, GetCurrentUserId(), await ScopeAsync(tenantId)), DeviceView);

    [HttpPut("Devices/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> UpdateDevice(Guid publicId, [FromQuery] Guid? tenantId, [FromBody] DeviceInput request) =>
        this.FromServiceResult(await access.SaveDeviceAsync(publicId, request, GetCurrentUserId(), await ScopeAsync(tenantId)), DeviceView);

    /// <summary>Cấp khoá API mới (khoá cũ hết hiệu lực ngay) — khoá gốc chỉ trả 1 lần.</summary>
    [HttpPut("Devices/{publicId:guid}/RegenerateKey")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> RegenerateKey(Guid publicId, [FromQuery] Guid? tenantId) =>
        this.FromServiceResult(await access.RegenerateKeyAsync(publicId, await ScopeAsync(tenantId)), DeviceView);

    [HttpDelete("Devices/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "delete")]
    public async Task<IActionResult> DeleteDevice(Guid publicId, [FromQuery] Guid? tenantId) =>
        this.FromServiceResult(await access.DeleteDeviceAsync(publicId, await ScopeAsync(tenantId)), x => x);

    private static object DeviceView(DeviceSaveResult r) => new { device = r.Device, apiKey = r.ApiKey };

    [HttpGet("StaffCards")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> StaffCards([FromQuery] Guid? tenantId) =>
        Ok(ApiResponse<List<AccessStaffCard>>.Ok(await access.StaffCardsAsync(await ScopeAsync(tenantId))));

    [HttpPost("StaffCards")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> AddStaffCard([FromQuery] Guid? tenantId, [FromBody] StaffCardInput request) =>
        this.FromServiceResult(await access.SaveStaffCardAsync(null, request, GetCurrentUserId(), await ScopeAsync(tenantId)), x => x);

    [HttpPut("StaffCards/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> UpdateStaffCard(Guid publicId, [FromQuery] Guid? tenantId, [FromBody] StaffCardInput request) =>
        this.FromServiceResult(await access.SaveStaffCardAsync(publicId, request, GetCurrentUserId(), await ScopeAsync(tenantId)), x => x);

    [HttpDelete("StaffCards/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "delete")]
    public async Task<IActionResult> DeleteStaffCard(Guid publicId, [FromQuery] Guid? tenantId) =>
        this.FromServiceResult(await access.DeleteStaffCardAsync(publicId, await ScopeAsync(tenantId)), x => x);

    /// <summary>Thủ thư mở cửa từ web (theo thiết bị, phòng hoặc lượt đặt) — phần mềm kiểm soát lấy lệnh trong vòng 2 phút.</summary>
    [HttpPost("Unlock")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> Unlock([FromQuery] Guid? tenantId, [FromBody] UnlockInput request) =>
        this.FromServiceResult(await access.UnlockAsync(request, GetCurrentUserId(), await ScopeAsync(tenantId)), n => new { devices = n });

    [HttpPost("Logs")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> Logs([FromBody] AccessLogFilter request)
    {
        request ??= new AccessLogFilter();
        return Ok(ApiResponse<PagedResult<AccessScanLog>>.Ok(await access.LogsAsync(request, await ScopeAsync(request.TenantId))));
    }
}
