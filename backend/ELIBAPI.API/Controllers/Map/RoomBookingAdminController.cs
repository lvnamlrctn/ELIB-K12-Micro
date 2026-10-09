using ELIBAPI.API.Filters;
using ELIBAPI.API.Infrastructure;
using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Map;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using ELIBAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.API.Controllers.Map;

/// <summary>
/// Quản lý đặt phòng học nhóm phía admin — danh sách/tìm kiếm/xuất Excel, duyệt/từ chối, check-in bằng QR, trả phòng; tham số, giờ mở
/// cửa, ngày đặc biệt, tạm ngưng phòng, danh sách chặn, mẫu thông báo, báo cáo, lịch tầng (port ELIB-LRC 09-28..10-04).
/// Tenant (K12): danh sách/duyệt/check-in/trả phòng lọc theo đơn vị JWT (repository); các màn cấu hình nhận <c>tenantId</c> (Guid) trên
/// query — user thường bị ép theo đơn vị JWT, tài khoản đặc quyền chọn đơn vị (không chọn = cấu hình dùng chung, xem
/// <see cref="ConfigLevel"/>).
/// </summary>
[Route("api/Map/RoomBookingAdmin")]
public class RoomBookingAdminController : GenericController<RoomBooking, RoomBookingSearchRequest, RoomBookingRequest>
{
    private readonly IRoomBookingRepository _roomBookingRepo;
    private readonly ELIBAPIDbContext _db;

    public RoomBookingAdminController(IRoomBookingRepository repo, ELIBAPIDbContext db) : base(repo)
    {
        _roomBookingRepo = repo;
        _db = db;
    }

    private Task<TenantScope> ScopeAsync(Guid? tenantId) =>
        TenantScopeHelper.ResolveScopeAsync(_db, tenantId, GetTenantId(), IsPrivilegedRole());

    /// <summary>Cấp cấu hình đang xem/sửa: đơn vị của user (hoặc đơn vị tài khoản đặc quyền chọn); null = cấu hình dùng chung.</summary>
    private async Task<long?> ConfigLevel(Guid? tenantId)
    {
        var scope = await ScopeAsync(tenantId);
        return scope.All ? null : scope.TenantId;
    }

    [HttpGet("{id:long}")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public override async Task<IActionResult> GetById(long id) => await base.GetById(id);

    [HttpGet("GetById/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public override async Task<IActionResult> GetByPublicId(Guid publicId) => await base.GetByPublicId(publicId);

    [HttpPost("Search")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public override async Task<IActionResult> Search([FromBody] RoomBookingSearchRequest request) => await base.Search(request);

    [HttpPost("SearchAll")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public override async Task<IActionResult> SearchAll([FromBody] RoomBookingSearchRequest request) => await base.SearchAll(request);

    [HttpPut("Approve/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> Approve(Guid publicId)
    {
        var (ok, error) = await _roomBookingRepo.ApproveAsync(publicId, GetCurrentUserId());
        return ok ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(error!, 400));
    }

    [HttpPut("Reject/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> Reject(Guid publicId, [FromBody] RejectRoomBookingRequest request)
    {
        var (ok, error) = await _roomBookingRepo.RejectAsync(publicId, GetCurrentUserId(), request?.Reason);
        return ok ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(error!, 400));
    }

    /// <summary>Check-in bằng mã QR bạn đọc xuất trình (payload "ELIB-RB:{publicId}"). Luôn trả thông tin lượt
    /// đặt tìm được (kể cả khi từ chối) để màn quét hiện rõ phòng/bạn đọc/giờ.</summary>
    [HttpPut("CheckIn/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> CheckIn(Guid publicId, [FromBody] StaffCheckInRequest? request)
    {
        var (ok, error, booking) = await _roomBookingRepo.StaffCheckInAsync(publicId, request?.MapObjectId, GetCurrentUserId());
        var view = CheckInView(booking);
        return ok
            ? Ok(ApiResponse<object>.Ok(view!, "Check-in thành công."))
            : BadRequest(new ApiResponse<object> { Success = false, Message = error!, Data = view, StatusCode = 400 });
    }

    /// <summary>Kiosk check-in bằng khuôn mặt (port ELIB-LRC 09-30): chỉ so ảnh camera với bạn đọc có lượt đã duyệt đang tới giờ
    /// (đúng phòng kiosk nếu có chọn, trong đơn vị của tài khoản). Không nhận ra ai → 200 recognized=false để kiosk quét tiếp.</summary>
    [HttpPut("CheckInByFace")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> CheckInByFace([FromBody] FaceCheckInRequest request,
        [FromServices] IFaceRecognitionService faceRecognition, CancellationToken ct)
    {
        var image = FaceImage.StripDataUrl(request?.ImageBase64);
        if (image.Length == 0) return BadRequest(ApiResponse<object>.Fail("Thiếu ảnh chụp", 400));

        var candidates = await _roomBookingRepo.FaceCheckInCandidatesAsync(request!.MapObjectId);
        if (candidates.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("Không có lượt đặt phòng nào đang tới giờ check-in.", 400));

        var match = await faceRecognition.IdentifyReaderAsync(image, GetTenantId(), candidates, ct);
        if (match == null)
            return Ok(ApiResponse<object>.Ok(new { recognized = false }, "Không nhận diện được"));

        var (ok, error, booking) = await _roomBookingRepo.StaffCheckInByReaderAsync(match.ReaderId, request.MapObjectId, GetCurrentUserId());
        var view = new { recognized = true, confidence = match.Confidence, booking = CheckInView(booking) };
        return ok
            ? Ok(ApiResponse<object>.Ok(view, "Check-in thành công."))
            : BadRequest(new ApiResponse<object> { Success = false, Message = error!, Data = view, StatusCode = 400 });
    }

    /// <summary>Thủ thư trả phòng hộ bạn đọc (lượt đang sử dụng → hoàn tất), phòng được mở lại ngay.</summary>
    [HttpPut("CheckOut/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> CheckOut(Guid publicId)
    {
        var (ok, error, booking) = await _roomBookingRepo.CheckOutAsync(publicId, null, GetCurrentUserId());
        return ok
            ? Ok(ApiResponse<object>.Ok(CheckInView(booking)!, "Đã trả phòng."))
            : BadRequest(ApiResponse<object>.Fail(error!, 400));
    }

    private static object? CheckInView(RoomBooking? booking) => booking == null ? null : new
    {
        publicId     = booking.PublicId,
        mapObjectId  = booking.MapObjectId,
        roomName     = booking.RoomName,
        readerName   = booking.ReaderName,
        readerCardNo = booking.ReaderCardNo,
        startAt      = DateTime.SpecifyKind(booking.StartAt, DateTimeKind.Utc),
        endAt        = DateTime.SpecifyKind(booking.EndAt, DateTimeKind.Utc),
        partySize    = booking.PartySize,
        status       = booking.Status
    };

    // ── Tham số, giờ mở cửa, ngày đặc biệt, tạm ngưng phòng ─────────────────

    [HttpGet("Settings")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> GetSettings([FromQuery] Guid? tenantId, [FromServices] RoomBookingAdminService admin) =>
        Ok(ApiResponse<RoomBookingSettings>.Ok(await admin.GetSettingsAsync(await ConfigLevel(tenantId))));

    [HttpPut("Settings")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> SaveSettings([FromQuery] Guid? tenantId, [FromBody] RoomBookingSettings request, [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SaveSettingsAsync(request, GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    [HttpGet("OpeningHours")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> OpeningHours([FromQuery] Guid? tenantId, [FromServices] RoomBookingAdminService admin) =>
        Ok(ApiResponse<List<RoomOpeningHour>>.Ok(await admin.OpeningHoursAsync(await ConfigLevel(tenantId))));

    /// <summary>Thay giờ theo thứ của 1 loại cơ sở (category trống = mọi loại).</summary>
    [HttpPut("OpeningHours")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> SaveOpeningHours([FromQuery] int? category, [FromQuery] Guid? tenantId, [FromBody] List<OpeningHourInput> request,
        [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SaveOpeningHoursAsync(category, request ?? [], GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    [HttpGet("SpecialDays")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> SpecialDays([FromQuery] DateTime? from, [FromQuery] Guid? tenantId, [FromServices] RoomBookingAdminService admin) =>
        Ok(ApiResponse<List<RoomSpecialDay>>.Ok(await admin.SpecialDaysAsync(from, await ConfigLevel(tenantId))));

    /// <summary>Xem trước các lượt đã đặt bị ảnh hưởng nếu lưu ngày đặc biệt này (không ghi gì).</summary>
    [HttpPost("SpecialDays/Preview")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> PreviewSpecialDay([FromQuery] Guid? publicId, [FromQuery] Guid? tenantId, [FromBody] SpecialDayInput request,
        [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SaveSpecialDayAsync(publicId, request, true, GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    [HttpPost("SpecialDays")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> AddSpecialDay([FromQuery] Guid? tenantId, [FromBody] SpecialDayInput request, [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SaveSpecialDayAsync(null, request, false, GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    [HttpPut("SpecialDays/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> UpdateSpecialDay(Guid publicId, [FromQuery] Guid? tenantId, [FromBody] SpecialDayInput request,
        [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SaveSpecialDayAsync(publicId, request, false, GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    [HttpDelete("SpecialDays/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> DeleteSpecialDay(Guid publicId, [FromQuery] Guid? tenantId, [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.DeleteSpecialDayAsync(publicId, await ConfigLevel(tenantId)), x => x);

    /// <summary>Tạm ngưng / mở lại phòng (bảo trì). preview=true: chỉ trả lượt bị ảnh hưởng.</summary>
    [HttpPut("Maintenance/{configPublicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> Maintenance(Guid configPublicId, [FromQuery] bool preview, [FromBody] MaintenanceInput request,
        [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SetMaintenanceAsync(configPublicId, request, preview, GetCurrentUserId(), await ScopeAsync(null)), x => x);

    // ── Báo cáo, lịch tầng ───────────────────────────────────────────────────

    /// <summary>Xuất Excel danh sách lượt đặt theo đúng bộ lọc màn hình (tối đa 20.000 dòng).</summary>
    [HttpPost("Export")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> Export([FromBody] RoomBookingSearchRequest request, [FromServices] RoomBookingReportService reports)
    {
        var (file, _, _) = await reports.ExportAsync(request ?? new RoomBookingSearchRequest());
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"dat-phong-{LibraryClock.Now:yyyyMMdd-HHmm}.xlsx");
    }

    /// <summary>Báo cáo tổng hợp theo khoảng ngày (tối đa 366 ngày).</summary>
    [HttpPost("Report")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> Report([FromBody] RoomBookingReportRequest request, [FromServices] RoomBookingReportService reports)
    {
        request ??= new RoomBookingReportRequest();
        return this.FromServiceResult(await reports.ReportAsync(request, await ScopeAsync(request.TenantId)), x => x);
    }

    [HttpPost("Report/Export")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> ReportExport([FromBody] RoomBookingReportRequest request, [FromServices] RoomBookingReportService reports)
    {
        request ??= new RoomBookingReportRequest();
        var res = await reports.ReportExcelAsync(request, await ScopeAsync(request.TenantId));
        return res.IsOk
            ? File(res.Value!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"bao-cao-dat-phong-{LibraryClock.Now:yyyyMMdd}.xlsx")
            : BadRequest(ApiResponse<object>.Fail(res.Error!));
    }

    /// <summary>Sơ đồ / lịch tầng cho thủ thư: không che tên, kèm thành viên và mã lượt đặt (để duyệt, mở cửa, trả phòng ngay trên lịch).
    /// Tenant: tầng phải thuộc phạm vi đơn vị; lịch, giờ mở cửa theo đơn vị của tầng.</summary>
    [HttpGet("FloorBoard")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> FloorBoard([FromQuery] long floorId, [FromQuery] DateTime date, [FromServices] IRoomBookingPortalService portal)
    {
        var scope = await ScopeAsync(null);
        var floor = await _db.MapFloors.AsNoTracking()
            .Where(f => f.Id == floorId && f.IsDelete != 2 && (scope.All || f.TenantId == scope.TenantId || (scope.IncludeShared && f.TenantId == null)))
            .Select(f => new { f.TenantId }).FirstOrDefaultAsync();
        if (floor == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy tầng.", 404));
        return Ok(ApiResponse<RoomFloorBoard>.Ok(await portal.FloorBoardAsync(floorId, date, floor.TenantId, null, staffView: true)));
    }

    // ── Mẫu thông báo ────────────────────────────────────────────────────────

    [HttpGet("Templates")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> Templates([FromQuery] Guid? tenantId, [FromServices] RoomBookingAdminService admin) =>
        Ok(ApiResponse<object>.Ok(new { templates = await admin.TemplatesAsync(await ConfigLevel(tenantId)), tokens = RoomBookingNotifier.Tokens }));

    [HttpPut("Templates/{eventName}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> SaveTemplate(string eventName, [FromQuery] Guid? tenantId, [FromBody] TemplateInput request,
        [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SaveTemplateAsync(eventName, request ?? new TemplateInput(), false, GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    /// <summary>Khôi phục mẫu mặc định (của cấp đang sửa).</summary>
    [HttpDelete("Templates/{eventName}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> ResetTemplate(string eventName, [FromQuery] Guid? tenantId, [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SaveTemplateAsync(eventName, new TemplateInput(), true, GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    /// <summary>Gửi thử mẫu đang lưu (dữ liệu mẫu) tới email chỉ định hoặc email của tài khoản đang đăng nhập.</summary>
    [HttpPost("Templates/{eventName}/Test")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> TestTemplate(string eventName, [FromQuery] Guid? tenantId, [FromBody] TemplateInput request,
        [FromServices] RoomBookingAdminService admin) =>
        this.FromServiceResult(await admin.SendTestTemplateAsync(eventName, request?.To, GetCurrentUserId(), await ConfigLevel(tenantId)), x => x);

    // ── Danh sách chặn ───────────────────────────────────────────────────────

    [HttpGet("Bans")]
    [Permission("STUDY_ROOM_BOOKING", "view")]
    public async Task<IActionResult> Bans([FromQuery] bool activeOnly, [FromQuery] string? keyword, [FromQuery] int pageIndex,
        [FromQuery] int pageSize, [FromQuery] Guid? tenantId, [FromServices] RoomBookingBanService bans) =>
        Ok(ApiResponse<PagedResult<RoomBookingBan>>.Ok(await bans.ListAsync(await ScopeAsync(tenantId), activeOnly, keyword, pageIndex, pageSize == 0 ? 20 : pageSize)));

    [HttpPost("Bans")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> AddBan([FromBody] AddRoomBookingBanRequest request, [FromServices] RoomBookingBanService bans,
        [FromServices] RoomBookingNotifier notifier)
    {
        var result = await bans.AddAsync(request.ReaderId, request.Days, request.Reason, GetCurrentUserId(), await ScopeAsync(null));
        if (result.IsOk) await notifier.NotifyBanAsync(result.Value!);
        return this.FromServiceResult(result, x => x);
    }

    [HttpPut("Bans/Lift/{publicId:guid}")]
    [Permission("STUDY_ROOM_BOOKING", "edit")]
    public async Task<IActionResult> LiftBan(Guid publicId, [FromServices] RoomBookingBanService bans) =>
        this.FromServiceResult(await bans.LiftAsync(publicId, GetCurrentUserId(), await ScopeAsync(null)), x => x);

    // Đợt 7 — Giai đoạn 2: khoá các route CRUD chung kế thừa từ GenericController — đặt/duyệt/từ chối/
    // check-in/hủy phòng đều phải đi qua nghiệp vụ chuyên biệt (RoomBookingController phía OPAC,
    // Approve/Reject ở trên), không cho phép lách qua Add/Update/ChangeStatus/Delete.
    [HttpPost("Add")]
    public override Task<IActionResult> Add([FromBody] RoomBookingRequest request) => Blocked();

    [HttpPut("Update/{publicId:guid}")]
    public override Task<IActionResult> Update(Guid publicId, [FromBody] RoomBookingRequest request) => Blocked();

    [HttpPut("ChangeStatus")]
    public override Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request) => Blocked();

    [HttpDelete("Delete/{publicId:guid}")]
    public override Task<IActionResult> Delete(Guid publicId) => Blocked();

    private Task<IActionResult> Blocked() => Task.FromResult<IActionResult>(StatusCode(405,
        ApiResponse<object>.Fail("Vui lòng sử dụng nghiệp vụ đặt, duyệt hoặc từ chối phòng.", 405)));
}

public class AddRoomBookingBanRequest
{
    public long    ReaderId { get; set; }
    public int     Days     { get; set; }
    public string? Reason   { get; set; }
}

public class StaffCheckInRequest
{
    /// <summary>Phòng đặt kiosk (null = mọi phòng).</summary>
    public long? MapObjectId { get; set; }
}

public class FaceCheckInRequest
{
    /// <summary>Ảnh chụp camera, base64 (có hoặc không kèm prefix "data:image/...;base64,").</summary>
    public string? ImageBase64 { get; set; }
    /// <summary>Phòng đặt kiosk (null = mọi phòng).</summary>
    public long? MapObjectId { get; set; }
}
