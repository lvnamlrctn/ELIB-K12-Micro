using System.Security.Claims;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Public.Map;

/// <summary>
/// Đặt phòng học nhóm cho bạn đọc (OPAC) — GET Rooms (danh sách phòng khả đặt) · GET Availability
/// (lưới giờ trống/bận trong 1 ngày) · GET FloorBoard (lịch tầng) · GET Member (tra thẻ thành viên nhóm) · POST Book · GET MyBookings ·
/// POST CheckIn/{publicId} · POST CheckOut/{publicId} (trả phòng) · DELETE Cancel/{publicId}. Tắt qua SystemParameter ROOM_BOOKING_ENABLED.
/// Phần đọc ở <see cref="IRoomBookingPortalService"/>, đặt/check-in/huỷ/trả phòng ở <see cref="IRoomBookingRepository"/> (port ELIB-LRC
/// 09-28..10-04). ELIB đa tenant: bạn đọc không có claim TenantId trong JWT (chỉ nhân viên mới có) — mọi thao tác resolve TenantId tường
/// minh từ Reader.TenantId, đúng pattern MyLibraryController.Hold (readerInfo.TenantId).
/// </summary>
[ApiController]
[Route("api/public/[controller]")]
[Authorize]
public class RoomBookingController(
    ELIBAPIDbContext db,
    IRoomBookingRepository roomBookingRepo,
    IRoomBookingPortalService portal) : ControllerBase
{
    // Cùng cách MyLibraryController xác định bạn đọc hiện tại: NameIdentifier = Reader.PublicId (Guid), chỉ token bạn đọc
    // (Type = "Reader") — token nhân viên mang Id số của Users, không được hiểu là bạn đọc.
    private async Task<(long Id, long? TenantId)?> GetCurrentReaderAsync()
    {
        if (User.FindFirstValue("Type") != "Reader") return null;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var readerPublicId)) return null;
        var info = await db.Readers
            .Where(r => r.PublicId == readerPublicId && r.IsDelete != 2)
            .Select(r => new { r.Id, r.TenantId })
            .FirstOrDefaultAsync();
        return info == null ? null : (info.Id, info.TenantId);
    }

    private IActionResult NotReader() => Unauthorized(ApiResponse<object>.Fail("Bạn cần đăng nhập bằng tài khoản bạn đọc.", 401));

    /// <summary>Người xem lịch (Rooms/Availability/FloorBoard xem công khai, port ELIB-LRC 09-28): bạn đọc đăng nhập → đơn vị của bạn
    /// đọc; chưa đăng nhập → đơn vị theo <paramref name="tenantPublicId"/> (OPAC gửi) hoặc theo tên miền (TenantContextMiddleware).
    /// Không xác định được đơn vị → null (chỉ dữ liệu dùng chung, thực tế rỗng).</summary>
    private async Task<(long? ReaderId, long? TenantId)> GetViewerAsync(Guid? tenantPublicId)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader != null) return (reader.Value.Id, reader.Value.TenantId);
        if (tenantPublicId.HasValue && tenantPublicId != Guid.Empty)
        {
            var id = await db.Tenants.Where(t => t.PublicId == tenantPublicId.Value && t.IsDelete != 2).Select(t => (long?)t.Id).FirstOrDefaultAsync();
            if (id != null) return (null, id);
        }
        return (null, HttpContext.Items.TryGetValue(ELIBAPI.Infrastructure.RateLimiting.TenantContextMiddleware.ItemKey, out var t) ? t as long? : null);
    }

    [AllowAnonymous]
    [HttpGet("Rooms")]
    public async Task<IActionResult> Rooms([FromQuery] Guid? tenantId)
    {
        var viewer = await GetViewerAsync(tenantId);
        var (enabled, rooms) = await portal.RoomsAsync(viewer.TenantId, viewer.ReaderId);
        return Ok(ApiResponse<object>.Ok(new { enabled, rooms }));
    }

    /// <summary>Lượt bận trong ngày + giờ mở cửa của riêng phòng ngày đó (ngày đặc biệt / giờ theo thứ / giờ chung, theo đơn vị) để
    /// OPAC dựng lưới giờ đúng — backend chỉ nhận lượt theo mốc <c>stepMinutes</c> và trong giờ mở cửa.</summary>
    [AllowAnonymous]
    [HttpGet("Availability")]
    public async Task<IActionResult> Availability([FromQuery] long mapObjectId, [FromQuery] DateTime date, [FromQuery] Guid? tenantId)
    {
        var viewer = await GetViewerAsync(tenantId);
        var tenant = viewer.TenantId;
        var category = await db.MapObjects.AsNoTracking().Where(o => o.Id == mapObjectId && o.TenantId == tenant)
            .Select(o => o.Category).FirstOrDefaultAsync();
        var hours = await RoomBookingHours.ResolveAsync(db, tenant, date.Date, category);
        return Ok(ApiResponse<object>.Ok(new
        {
            slots = await portal.AvailabilityAsync(mapObjectId, date, tenant),
            openTime = RoomBookingHours.Format(hours.Open),
            closeTime = RoomBookingHours.Format(hours.Close),
            closed = hours.Closed,
            closedReason = hours.Reason,
            stepMinutes = RoomBookingHours.StepMinutes,
        }));
    }

    /// <summary>Lịch tầng: phòng khả đặt + thiết bị + lượt bận trong ngày; tên/mã thẻ người khác bị che (xem
    /// <see cref="IRoomBookingPortalService.FloorBoardAsync"/>). Xem công khai — đặt phòng vẫn cần đăng nhập.</summary>
    [AllowAnonymous]
    [HttpGet("FloorBoard")]
    public async Task<IActionResult> FloorBoard([FromQuery] long floorId, [FromQuery] DateTime date, [FromQuery] Guid? tenantId)
    {
        var viewer = await GetViewerAsync(tenantId);
        return Ok(ApiResponse<RoomFloorBoard>.Ok(await portal.FloorBoardAsync(floorId, date, viewer.TenantId, viewer.ReaderId)));
    }

    /// <summary>Kiểm tra thẻ thành viên (cùng đơn vị) khi bạn đọc thêm vào nhóm: trả tên đã che để xác nhận đúng người.</summary>
    [HttpGet("Member")]
    public async Task<IActionResult> Member([FromQuery] string card)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return NotReader();
        var found = await portal.LookupMemberAsync(card, reader.Value.Id, reader.Value.TenantId);
        return found == null
            ? NotFound(ApiResponse<object>.Fail("Không tìm thấy bạn đọc có thẻ này.", 404))
            : Ok(ApiResponse<MemberCardLookup>.Ok(found));
    }

    [HttpPost("Book")]
    public async Task<IActionResult> Book([FromBody] CreateRoomBookingRequest request)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return NotReader();

        if (!await RoomBookingParams.IsEnabledAsync(db, "ROOM_BOOKING_ENABLED", reader.Value.TenantId))
            return BadRequest(ApiResponse<object>.Fail("Tính năng đặt phòng học nhóm hiện đang tắt.", 400));

        var (ok, error, statusCode, booking) = await roomBookingRepo.CreateBookingAsync(request, reader.Value.Id, reader.Value.TenantId);
        if (!ok) return StatusCode(statusCode, ApiResponse<object>.Fail(error!, statusCode));
        return Ok(ApiResponse<object>.Ok(new { publicId = booking!.PublicId, status = booking.Status }));
    }

    [HttpGet("MyBookings")]
    public async Task<IActionResult> MyBookings()
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return NotReader();
        return Ok(ApiResponse<List<MyRoomBooking>>.Ok(await portal.MyBookingsAsync(reader.Value.Id, reader.Value.TenantId)));
    }

    [HttpPost("CheckIn/{publicId:guid}")]
    public async Task<IActionResult> CheckIn(Guid publicId)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return NotReader();

        var (ok, error) = await roomBookingRepo.CheckInAsync(publicId, reader.Value.Id, reader.Value.TenantId);
        return ok ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(error!, 400));
    }

    /// <summary>Bạn đọc tự check-in, xác minh bằng khuôn mặt (port ELIB-LRC 09-30): ảnh camera chỉ so với ảnh của chính bạn đọc
    /// đang đăng nhập. Kiểm tra trạng thái/giờ trước để không gọi AI vô ích.</summary>
    [HttpPost("CheckInByFace/{publicId:guid}")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(ELIBAPI.API.Infrastructure.RateLimitingExtensions.ChatPublicPolicy)]
    public async Task<IActionResult> CheckInByFace(Guid publicId, [FromBody] ReaderFaceCheckInRequest request,
        [FromServices] IFaceRecognitionService faceRecognition, CancellationToken ct)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return NotReader();

        var image = FaceImage.StripDataUrl(request?.ImageBase64);
        if (image.Length == 0) return BadRequest(ApiResponse<object>.Fail("Thiếu ảnh chụp.", 400));

        var stateError = await roomBookingRepo.CheckInPrecheckAsync(publicId, reader.Value.Id, reader.Value.TenantId);
        if (stateError != null) return BadRequest(ApiResponse<object>.Fail(stateError, 400));

        if (!await faceRecognition.HasFacePhotoAsync(reader.Value.Id, ct))
            return BadRequest(ApiResponse<object>.Fail("Tài khoản chưa có ảnh khuôn mặt, vui lòng liên hệ thủ thư để cập nhật.", 400));

        var match = await faceRecognition.IdentifyReaderAsync(image, reader.Value.TenantId, [reader.Value.Id], ct);
        if (match == null || match.ReaderId != reader.Value.Id)
            return BadRequest(ApiResponse<object>.Fail("Khuôn mặt không khớp với ảnh đã đăng ký. Vui lòng thử lại.", 400));

        var (ok, error) = await roomBookingRepo.CheckInAsync(publicId, reader.Value.Id, reader.Value.TenantId);
        return ok ? Ok(ApiResponse<object>.Ok(null!, "Check-in thành công.")) : BadRequest(ApiResponse<object>.Fail(error!, 400));
    }

    /// <summary>Trả phòng (kết thúc sớm) lượt đang sử dụng — phòng được mở lại ngay cho bạn đọc khác.</summary>
    [HttpPost("CheckOut/{publicId:guid}")]
    public async Task<IActionResult> CheckOut(Guid publicId)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return NotReader();

        var (ok, error, _) = await roomBookingRepo.CheckOutAsync(publicId, reader.Value.Id, null, reader.Value.TenantId);
        return ok ? Ok(ApiResponse<object>.Ok(null!, "Đã trả phòng.")) : BadRequest(ApiResponse<object>.Fail(error!, 400));
    }

    [HttpDelete("Cancel/{publicId:guid}")]
    public async Task<IActionResult> Cancel(Guid publicId)
    {
        var reader = await GetCurrentReaderAsync();
        if (reader == null) return NotReader();

        var error = await roomBookingRepo.CancelAsync(publicId, reader.Value.Id, reader.Value.TenantId);
        return error == null ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(error, 400));
    }
}

public class ReaderFaceCheckInRequest
{
    /// <summary>Ảnh chụp camera, base64 (có hoặc không kèm prefix "data:image/...;base64,").</summary>
    public string? ImageBase64 { get; set; }
}
