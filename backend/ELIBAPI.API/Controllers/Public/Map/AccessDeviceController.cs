using ELIBAPI.Core.Common;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELIBAPI.API.Controllers.Public.Map;

/// <summary>
/// API cho phần mềm kiểm soát truy cập (đầu đọc thẻ / bộ điều khiển cửa). Xác thực bằng header <c>X-Device-Code</c> (mã thiết bị =
/// địa chỉ cửa) và <c>X-Device-Key</c> (khoá API cấp khi khai báo thiết bị ở màn Đặt phòng → Kiểm soát cửa).
/// - POST Verify {uid, scannedAt?}: quẹt thẻ → {allowed, reason, ...}; cho mở thì phần mềm kiểm soát mở cửa.
/// - GET Commands: lệnh mở cửa thủ thư gửi từ web (gọi định kỳ, vd mỗi 2–5 giây); POST Commands/{id}/Ack: đã thực hiện.
/// Port ELIB-LRC 10-04. Tenant (K12): đơn vị suy ra từ thiết bị đã xác thực (không nhận từ request) — quẹt thẻ chỉ đối chiếu bạn đọc và
/// lượt đặt của đơn vị thiết bị.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/access")]
public class AccessDeviceController(AccessControlService access) : ControllerBase
{
    private async Task<ELIBAPI.Core.Entities.Map.AccessDevice?> DeviceAsync() =>
        await access.AuthenticateAsync(Request.Headers["X-Device-Code"].FirstOrDefault(), Request.Headers["X-Device-Key"].FirstOrDefault());

    private IActionResult DeviceUnauthorized() =>
        Unauthorized(ApiResponse<object>.Fail("Mã thiết bị hoặc khoá API không đúng, hoặc thiết bị đang tắt.", 401));

    [HttpPost("Verify")]
    public async Task<IActionResult> Verify([FromBody] AccessVerifyRequest request)
    {
        var device = await DeviceAsync();
        if (device == null) return DeviceUnauthorized();
        var d = await access.VerifyAsync(device, request?.Uid, request?.ScannedAt);
        return Ok(ApiResponse<object>.Ok(new
        {
            allowed = d.Allowed, reason = d.Reason, bookingId = d.BookingPublicId, holder = d.Holder, validUntil = d.ValidUntil,
            deviceCode = device.Code, mapObjectId = device.MapObjectId,
        }));
    }

    [HttpGet("Commands")]
    public async Task<IActionResult> Commands()
    {
        var device = await DeviceAsync();
        if (device == null) return DeviceUnauthorized();
        var list = await access.PendingCommandsAsync(device);
        return Ok(ApiResponse<object>.Ok(list.Select(c => new
        {
            id = c.PublicId, command = c.Command, note = c.Note,
            requestedAt = DateTime.SpecifyKind(c.RequestedAt, DateTimeKind.Utc), expiresAt = DateTime.SpecifyKind(c.ExpiresAt, DateTimeKind.Utc),
        })));
    }

    [HttpPost("Commands/{id:guid}/Ack")]
    public async Task<IActionResult> Ack(Guid id)
    {
        var device = await DeviceAsync();
        if (device == null) return DeviceUnauthorized();
        return await access.AckAsync(device, id)
            ? Ok(ApiResponse<object>.Ok(null))
            : NotFound(ApiResponse<object>.Fail("Không tìm thấy lệnh.", 404));
    }
}

public class AccessVerifyRequest
{
    /// <summary>UID chip thẻ (hệ 16, có/không dấu phân cách).</summary>
    public string? Uid { get; set; }
    /// <summary>Giờ quét theo thiết bị (chỉ để tra cứu; quyết định theo giờ máy chủ).</summary>
    public DateTime? ScannedAt { get; set; }
}
